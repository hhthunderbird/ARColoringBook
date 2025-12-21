using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Felina.ARColoringBook
{
    public class ARScannerManager : MonoBehaviour
    {
        public static ARScannerManager Instance { get; private set; }

        [Header( "Configuration" )]
        [SerializeField] private MonoBehaviour arBridgeComponent;
        private IARBridge _arBridge;

        [Header( "Scanner Settings" )]
        public Material unwarpMaterial;
        public int outputResolution = 1024;
        [Range( 0f, 1f )] public float captureThreshold = 0.85f;

        // Event signature matches our architecture
        public event Action<string, Texture2D, float> OnTextureCaptured;

        // Internal State
        private RenderTexture _cameraFeedRT;
        private RenderTexture _tempUnwarpRT;
        private Camera _arCamera;

        // Tracking Data
        private Dictionary<string, float> _bestScores = new Dictionary<string, float>();
        private Dictionary<string, bool> _isLocked = new Dictionary<string, bool>();
        private Dictionary<string, Texture2D> _cachedTextures = new Dictionary<string, Texture2D>();

        // Optimization: Track last frame updated to avoid redundant blits
        private int _lastFrameUpdated = -1;
        // Track pending async readbacks to avoid overlapping work per target
        private HashSet<string> _pendingReadbacks = new HashSet<string>();
        // Reusable buffers to avoid allocations during captures
        private List<Vector2> _cachedCorrectedUVs = new List<Vector2>(4);
        private List<Vector2> _cachedScreenPoints = new List<Vector2>(4);
        private Vector3[] _cachedLocalCorners = new Vector3[4];
        // Readback throttling
        private Dictionary<string, float> _lastReadbackTime = new Dictionary<string, float>();
        private float _readbackCooldown = 1.0f; // seconds
        private int _tempUnwarpResolution;

        private void Awake()
        {
            if ( Instance != null && Instance != this ) Destroy( gameObject );
            else Instance = this;

            if ( arBridgeComponent is IARBridge bridge ) _arBridge = bridge;
            else Debug.LogError( "[Felina] Assigned AR Bridge is invalid!" );
        }

        void Start()
        {
            if ( _arBridge != null ) _arCamera = _arBridge.GetARCamera();

            _cameraFeedRT = new RenderTexture( Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32 );
            _cameraFeedRT.Create();

            // Use configured resolution (this component runs only on mobile)
            _tempUnwarpResolution = outputResolution;
            _tempUnwarpRT = new RenderTexture( _tempUnwarpResolution, _tempUnwarpResolution, 0, RenderTextureFormat.ARGB32 );
            _tempUnwarpRT.Create();
        }

        void OnEnable() { if ( _arBridge != null ) _arBridge.OnTargetAdded += OnTargetAdded; }


        void OnDisable() 
        { 
            if ( _arBridge != null ) _arBridge.OnTargetAdded -= OnTargetAdded; 
        }

        public Texture2D GetCapturedTexture( string targetName )
        {
            if ( _cachedTextures.ContainsKey( targetName ) ) return _cachedTextures[ targetName ];
            return null;
        }

        private void EnsureCameraFeedIsFresh()
        {
            if ( Time.frameCount != _lastFrameUpdated && _arBridge != null )
            {
                _arBridge.FillCameraTexture( _cameraFeedRT );
                _lastFrameUpdated = Time.frameCount;
            }
        }

        private void OnTargetRemoved( ScanTarget target )
        {
            _arBridge.OnTargetAdded += OnTargetAdded;
            _arBridge.OnTargetRemoved -= OnTargetRemoved;
        }
        private void OnTargetAdded( ScanTarget target )
        {
            // 1. Safety Checks
            if ( string.IsNullOrEmpty( target.Name ) ) return;

            // 2. State Check: If tracking lost, just exit (or optionally reset lock)
            if ( !target.IsTracking ) return;

            // 3. Lock Check: If already captured successfully, stop processing to save CPU
            if ( _isLocked.ContainsKey( target.Name ) && _isLocked[ target.Name ] ) return;

            // 4. Quality Evaluation (Exact logic from ARUnwarper.cs)
            float currentScore = CalculateQualityScore( target );

            // Init score if missing
            if ( !_bestScores.ContainsKey( target.Name ) ) _bestScores[ target.Name ] = 0f;

            // 5. Compare against best score
            if ( currentScore > _bestScores[ target.Name ] )
            {
                _bestScores[ target.Name ] = currentScore;

                // 6. Only now do we incur the cost of grabbing the camera frame
                EnsureCameraFeedIsFresh();

                // 7. Start async unwarp + GPU readback to avoid blocking the main thread
                float lastTime = 0f;
                _lastReadbackTime.TryGetValue( target.Name, out lastTime );
                if ( !_pendingReadbacks.Contains( target.Name ) && Time.time - lastTime >= _readbackCooldown )
                {
                    _pendingReadbacks.Add( target.Name );
                    _lastReadbackTime[ target.Name ] = Time.time;
                    StartUnwarpAndReadback( target, currentScore );
                }
            }
        }

        private float CalculateQualityScore( ScanTarget target )
        {
            if ( _arCamera == null ) return 0f;

            // A. Angle Score (0.6 weight)
            float dot = Vector3.Dot( target.Transform.up, -_arCamera.transform.forward );
            float angleScore = Mathf.Clamp01( dot );

            // B. Center Score (0.3 weight)
            Vector3 screenPos = _arCamera.WorldToScreenPoint( target.Position );
            Vector2 screenCenter = new Vector2( Screen.width / 2f, Screen.height / 2f );
            float distFromCenter = Vector2.Distance( new Vector2( screenPos.x, screenPos.y ), screenCenter );
            float maxDist = Screen.height / 2f;
            float centerScore = Mathf.Clamp01( 1f - ( distFromCenter / maxDist ) );

            // C. Distance Score (0.1 weight)
            float distance = Vector3.Distance( _arCamera.transform.position, target.Position );
            float distanceScore = 1f;
            if ( distance < 0.2f ) distanceScore = 0.5f; // Too close
            if ( distance > 1.0f ) distanceScore = 0.5f; // Too far

            return ( angleScore * 0.6f ) + ( centerScore * 0.3f ) + ( distanceScore * 0.1f );
        }

        private Texture2D UnwarpImage( ScanTarget target )
        {
            // --- Aspect Ratio Correction (Exact math from ARUnwarper.cs) ---
            float physicalRatio = target.Size.x / target.Size.y;
            float uMin = 0f, uMax = 1f, vMin = 0f, vMax = 1f;

            if ( physicalRatio < 1.0f ) // Portrait
            {
                float heightScale = 1.0f / physicalRatio;
                vMin = ( 1.0f - heightScale ) / 2.0f;
                vMax = vMin + heightScale;
            }
            else if ( physicalRatio > 1.0f ) // Landscape
            {
                float widthScale = physicalRatio;
                uMin = ( 1.0f - widthScale ) / 2.0f;
                uMax = uMin + widthScale;
            }

            _cachedCorrectedUVs.Clear();
            _cachedCorrectedUVs.Add( new Vector2(uMin, vMin) );
            _cachedCorrectedUVs.Add( new Vector2(uMax, vMin) );
            _cachedCorrectedUVs.Add( new Vector2(uMax, vMax) );
            _cachedCorrectedUVs.Add( new Vector2(uMin, vMax) );

            Vector2 size = target.Size;
            _cachedLocalCorners[0] = new Vector3(-size.x / 2, 0, -size.y / 2);
            _cachedLocalCorners[1] = new Vector3(size.x / 2, 0, -size.y / 2);
            _cachedLocalCorners[2] = new Vector3(size.x / 2, 0, size.y / 2);
            _cachedLocalCorners[3] = new Vector3(-size.x / 2, 0, size.y / 2);

            _cachedScreenPoints.Clear();
            for ( int i = 0; i < 4; i++ )
            {
                Vector3 worldPos = target.Transform.TransformPoint( _cachedLocalCorners[ i ] );
                Vector3 screenPos = _arCamera.WorldToScreenPoint( worldPos );
                _cachedScreenPoints.Add( new Vector2( screenPos.x / Screen.width, screenPos.y / Screen.height ) );
            }

            Matrix4x4 H = HomographySolver.ComputeHomography( _cachedCorrectedUVs, _cachedScreenPoints );

            unwarpMaterial.SetTexture( "_MainTex", _cameraFeedRT );
            unwarpMaterial.SetMatrix( "_Homography", H );
            unwarpMaterial.SetMatrix( "_DisplayMatrix", Matrix4x4.identity );

            Graphics.Blit( null, _tempUnwarpRT, unwarpMaterial );

            // Fallback synchronous path kept for compatibility (not used when async is available)
            Texture2D result = new Texture2D( outputResolution, outputResolution, TextureFormat.RGB24, false );
            RenderTexture.active = _tempUnwarpRT;
            result.ReadPixels( new Rect( 0, 0, outputResolution, outputResolution ), 0, 0 );
            result.Apply();
            RenderTexture.active = null;

            return result;
        }

        private void StartUnwarpAndReadback( ScanTarget target, float score )
        {
            // Prepare homography and set material same as UnwarpImage
            float physicalRatio = target.Size.x / target.Size.y;
            float uMin = 0f, uMax = 1f, vMin = 0f, vMax = 1f;

            if ( physicalRatio < 1.0f ) // Portrait
            {
                float heightScale = 1.0f / physicalRatio;
                vMin = ( 1.0f - heightScale ) / 2.0f;
                vMax = vMin + heightScale;
            }
            else if ( physicalRatio > 1.0f ) // Landscape
            {
                float widthScale = physicalRatio;
                uMin = ( 1.0f - widthScale ) / 2.0f;
                uMax = uMin + widthScale;
            }

            List<Vector2> correctedUVs = new List<Vector2> {
                new Vector2(uMin, vMin), new Vector2(uMax, vMin),
                new Vector2(uMax, vMax), new Vector2(uMin, vMax)
            };

            Vector2 size = target.Size;
            Vector3[] localCorners = new Vector3[] {
                new Vector3(-size.x / 2, 0, -size.y / 2),
                new Vector3(size.x / 2, 0, -size.y / 2),
                new Vector3(size.x / 2, 0, size.y / 2),
                new Vector3(-size.x / 2, 0, size.y / 2)
            };

            List<Vector2> screenPoints = new List<Vector2>();
            for ( int i = 0; i < 4; i++ )
            {
                Vector3 worldPos = target.Transform.TransformPoint( localCorners[ i ] );
                Vector3 screenPos = _arCamera.WorldToScreenPoint( worldPos );
                screenPoints.Add( new Vector2( screenPos.x / Screen.width, screenPos.y / Screen.height ) );
            }

            Matrix4x4 H = HomographySolver.ComputeHomography( correctedUVs, screenPoints );

            unwarpMaterial.SetTexture( "_MainTex", _cameraFeedRT );
            unwarpMaterial.SetMatrix( "_Homography", H );
            unwarpMaterial.SetMatrix( "_DisplayMatrix", Matrix4x4.identity );

            // Blit into temp RT (use platform-scaled temp RT)
            Graphics.Blit( null, _tempUnwarpRT, unwarpMaterial );

            // Start async GPU readback
            AsyncGPUReadback.Request( _tempUnwarpRT, 0, TextureFormat.RGB24, (request) =>
            {
                try
                {
                    if ( request.hasError )
                    {
                        Debug.LogWarning( "[Felina] AsyncGPUReadback error when reading unwarp texture." );
                        return;
                    }
                    var data = request.GetData<byte>();
                    // Try to reuse an existing texture to avoid allocations
                    Texture2D tex;
                    int texRes = _tempUnwarpResolution;
                    if ( _cachedTextures.ContainsKey( target.Name ) && _cachedTextures[ target.Name ] != null )
                    {
                        tex = _cachedTextures[ target.Name ];
                        tex.Reinitialize( texRes, texRes, TextureFormat.RGB24, false );
                        tex.LoadRawTextureData( data );
                        tex.Apply();
                    }
                    else
                    {
                        tex = new Texture2D( texRes, texRes, TextureFormat.RGB24, false );
                        tex.LoadRawTextureData( data );
                        tex.Apply();
                    }

                    // Cache
                    if ( _cachedTextures.ContainsKey( target.Name ) )
                    {
                        if ( _cachedTextures[ target.Name ] != null ) Destroy( _cachedTextures[ target.Name ] );
                        _cachedTextures[ target.Name ] = tex;
                    }
                    else
                    {
                        _cachedTextures.Add( target.Name, tex );
                    }

                    // Notify listeners
                    OnTextureCaptured?.Invoke( target.Name, tex, score );

                    // Auto-lock
                    if ( score >= captureThreshold )
                    {
                        _isLocked[ target.Name ] = true;
                        _arBridge.OnTargetAdded -= OnTargetAdded;
                        _arBridge.OnTargetRemoved += OnTargetRemoved;
                        Debug.Log( $"[Felina] Locked '{target.Name}' Score: {score:F2}" );
                    }
                }
                finally
                {
                    _pendingReadbacks.Remove( target.Name );
                }
            } );
        }

        // Exposed for UI Buttons/Debug
        public void ResetTarget( string targetName )
        {
            if ( _isLocked.ContainsKey( targetName ) ) _isLocked[ targetName ] = false;
            if ( _bestScores.ContainsKey( targetName ) ) _bestScores[ targetName ] = 0f;
        }
    }
}

//using System;
//using System.Collections.Generic;
//using UnityEngine;

//namespace Felina.ARColoringBook
//{
//    public class ARScannerManager : MonoBehaviour
//    {
//        public static ARScannerManager Instance { get; private set; }

//        [Header( "Configuration" )]
//        [Tooltip( "Drag your specific AR Bridge here (e.g., ARFoundationBridge)" )]
//        [SerializeField] private MonoBehaviour arBridgeComponent;
//        private IARBridge _arBridge;

//        [Header( "Scanner Settings" )]
//        public Material unwarpMaterial;
//        public int outputResolution = 1024;
//        [Range( 0f, 1f )] public float captureThreshold = 0.85f;

//        public event Action<string, Texture2D, float> OnTextureCaptured;

//        // Internal State
//        private RenderTexture _cameraFeedRT;
//        private RenderTexture _tempUnwarpRT;
//        private Dictionary<string, float> _bestScores = new Dictionary<string, float>();
//        private Dictionary<string, bool> _isLocked = new Dictionary<string, bool>();

//        // Cache is correctly defined here
//        private Dictionary<string, Texture2D> _cachedTextures = new Dictionary<string, Texture2D>();
//        private Camera _arCamera;

//        private void Awake()
//        {
//            if ( Instance != null && Instance != this ) Destroy( gameObject );
//            else Instance = this;

//            if ( arBridgeComponent is IARBridge bridge )
//            {
//                _arBridge = bridge;
//            }
//            else
//            {
//                Debug.LogError( "[Felina] Assigned AR Bridge does not implement IARBridge!" );
//            }
//        }

//        void Start()
//        {
//            if ( _arBridge != null ) _arCamera = _arBridge.GetARCamera();

//            _cameraFeedRT = new RenderTexture( Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32 );
//            _cameraFeedRT.Create();

//            _tempUnwarpRT = new RenderTexture( outputResolution, outputResolution, 0, RenderTextureFormat.ARGB32 );
//            _tempUnwarpRT.Create();
//        }

//        void OnEnable()
//        {
//            if ( _arBridge != null ) _arBridge.OnTargetUpdated += ProcessTarget;
//        }

//        void OnDisable()
//        {
//            if ( _arBridge != null ) _arBridge.OnTargetUpdated -= ProcessTarget;
//        }

//        void Update()
//        {
//            if ( _arBridge != null )
//            {
//                _arBridge.FillCameraTexture( _cameraFeedRT );
//            }
//        }

//        // Helper for objects to check "Did you see my image yet?"
//        public Texture2D GetCapturedTexture( string targetName )
//        {
//            if ( _cachedTextures.ContainsKey( targetName ) )
//            {
//                return _cachedTextures[ targetName ];
//            }
//            return null;
//        }

//        private void ProcessTarget( ScanTarget target )
//        {
//            if ( string.IsNullOrEmpty( target.Name ) ) return;


//            if ( _isLocked.ContainsKey( target.Name ) && _isLocked[ target.Name ] ) return;

//            if ( !target.IsTracking ) return;

//            float quality = CalculateQualityScore( target );

//            if ( !_bestScores.ContainsKey( target.Name ) ) _bestScores[ target.Name ] = 0f;

//            if ( quality > _bestScores[ target.Name ] )
//            {
//                _bestScores[ target.Name ] = quality;

//                Texture2D extractedTex = UnwarpImage( target );

//                // --- FIX: SAVE TO CACHE ---
//                if ( _cachedTextures.ContainsKey( target.Name ) )
//                {
//                    // (Optional) Destroy old texture to free memory?
//                    // Destroy(_cachedTextures[target.Name]); 
//                    _cachedTextures[ target.Name ] = extractedTex;
//                }
//                else
//                {
//                    _cachedTextures.Add( target.Name, extractedTex );
//                }
//                // --------------------------

//                OnTextureCaptured?.Invoke( target.Name, extractedTex, quality );

//                if ( quality >= captureThreshold )
//                {
//                    _isLocked[ target.Name ] = true;
//                    Debug.Log( $"[Felina] Locked '{target.Name}' Score: {quality:F2}" );
//                }
//            }
//        }

//        // --- MATH HELPERS ---

//        private float CalculateQualityScore( ScanTarget target )
//        {
//            if ( _arCamera == null ) return 0f;

//            float dot = Vector3.Dot( target.Transform.up, -_arCamera.transform.forward );
//            float angleScore = Mathf.Clamp01( dot );

//            Vector3 screenPos = _arCamera.WorldToScreenPoint( target.Position );
//            Vector2 screenCenter = new Vector2( Screen.width / 2f, Screen.height / 2f );
//            float distFromCenter = Vector2.Distance( new Vector2( screenPos.x, screenPos.y ), screenCenter );
//            float maxDist = Screen.height / 2f;
//            float centerScore = Mathf.Clamp01( 1f - ( distFromCenter / maxDist ) );

//            return ( angleScore * 0.7f ) + ( centerScore * 0.3f );
//        }

//        private Texture2D UnwarpImage( ScanTarget target )
//        {
//            float physicalRatio = target.Size.x / target.Size.y;
//            float uMin = 0f, uMax = 1f, vMin = 0f, vMax = 1f;

//            if ( physicalRatio < 1.0f )
//            {
//                float s = 1.0f / physicalRatio;
//                vMin = ( 1 - s ) / 2;
//                vMax = vMin + s;
//            }
//            else
//            {
//                float s = physicalRatio;
//                uMin = ( 1 - s ) / 2;
//                uMax = uMin + s;
//            }

//            List<Vector2> correctedUVs = new List<Vector2> {
//                new Vector2(uMin, vMin), new Vector2(uMax, vMin),
//                new Vector2(uMax, vMax), new Vector2(uMin, vMax)
//            };

//            Vector2 size = target.Size;
//            Vector3[] localCorners = new Vector3[] {
//                new Vector3(-size.x / 2, 0, -size.y / 2),
//                new Vector3(size.x / 2, 0, -size.y / 2),
//                new Vector3(size.x / 2, 0, size.y / 2),
//                new Vector3(-size.x / 2, 0, size.y / 2)
//            };

//            List<Vector2> screenPoints = new List<Vector2>();
//            for ( int i = 0; i < 4; i++ )
//            {
//                Vector3 worldPos = target.Transform.TransformPoint( localCorners[ i ] );
//                Vector3 screenPos = _arCamera.WorldToScreenPoint( worldPos );
//                screenPoints.Add( new Vector2( screenPos.x / Screen.width, screenPos.y / Screen.height ) );
//            }

//            Matrix4x4 H = HomographySolver.ComputeHomography( correctedUVs, screenPoints );

//            unwarpMaterial.SetTexture( "_MainTex", _cameraFeedRT );
//            unwarpMaterial.SetMatrix( "_Homography", H );
//            unwarpMaterial.SetMatrix( "_DisplayMatrix", Matrix4x4.identity );

//            Graphics.Blit( null, _tempUnwarpRT, unwarpMaterial );

//            Texture2D result = new Texture2D( outputResolution, outputResolution, TextureFormat.RGB24, false );
//            RenderTexture.active = _tempUnwarpRT;
//            result.ReadPixels( new Rect( 0, 0, outputResolution, outputResolution ), 0, 0 );
//            result.Apply();
//            RenderTexture.active = null;

//            return result;
//        }
//    }
//}