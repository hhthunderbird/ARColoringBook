using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Felina.ARColoringBook
{
    public class ARScannerManager : MonoBehaviour
    {
        public static ARScannerManager Instance { get; private set; }
        [Header( "Configuration" )]
        [SerializeField] private MonoBehaviour _arBridgeComponent;
        private IARBridge _arBridge;

        [SerializeField] private Camera _arCamera;

        [Header( "Output" )]
        [SerializeField] private Material _unwarpMaterial;
        [SerializeField] private RenderTexture _unwarpedTexture;
        [SerializeField] private Material _targetObjectMaterial;

        //[Header( "Settings" )]
        //[SerializeField] private int _outputResolution = 1024;
        [Range( 0f, 1f )]
        [SerializeField] private float _captureThreshold = 0.85f;
        [SerializeField] private bool _autoLock = true;

        public event Action<string, RenderTexture, float> OnTextureCaptured;

        [Header( "Stability" )]
        [SerializeField] private float _maxMoveSpeed = 0.05f;
        [SerializeField] private float _maxRotateSpeed = 5.0f;

        public bool IsDeviceStable { get; private set; } = false;

        // Internal State
        //private RenderTexture _cameraFeedRT;
        private float _highestQualityScore = 0.0f;
        private bool _isLocked = false;
        private int _lastFrameUpdated = -1;

        // Stability
        private float3 _lastCamPos;
        private quaternion _lastCamRot;

        // Native Memory
        private NativeArray<float2> _nativeScreenPoints;
        private NativeArray<float4x4> _nativeResultMatrix;



        // --- NATIVE PLUGIN IMPORTS ---
#if UNITY_EDITOR || UNITY_STANDALONE
        [System.Runtime.InteropServices.DllImport( "Felina" )]
#elif UNITY_ANDROID
    [System.Runtime.InteropServices.DllImport("Felina")]
#endif
        private static extern int GetDebugNumber();

#if UNITY_EDITOR || UNITY_STANDALONE
        [System.Runtime.InteropServices.DllImport( "Felina" )]
#elif UNITY_ANDROID
    [System.Runtime.InteropServices.DllImport("Felina")]
#endif
        private static extern bool CheckStability(
            float3 curPos, quaternion curRot,
            float3 lastPos, quaternion lastRot,
            float dt,
            float maxMoveSpeed, float maxRotSpeed
        );

#if UNITY_EDITOR || UNITY_STANDALONE
        [System.Runtime.InteropServices.DllImport( "Felina" )]
#elif UNITY_ANDROID
    [System.Runtime.InteropServices.DllImport("Felina")]
#endif
        private static extern float CalculateQuality(
            float3 camPos, float3 camFwd,
            float3 imgPos, float3 imgUp,
            float2 imgScreenPos,
            float screenWidth, float screenHeight
        );
#if UNITY_EDITOR || UNITY_STANDALONE
        [System.Runtime.InteropServices.DllImport( "Felina" )]
#elif UNITY_ANDROID
    [System.Runtime.InteropServices.DllImport("Felina")]
#endif
        private static unsafe extern void CalculateHomography(
            float imgW, float imgH,
            float screenW, float screenH,
            void* rawScreenPoints,
            void* result
        );


        void Start()
        {
            try
            {
                int magic = GetDebugNumber();
                if ( magic == 777 )
                {
                    Debug.Log( "<color=green>[Felina] Native Library Connected! Magic Number: 777</color>" );
                }
                else
                {
                    Debug.LogError( $"[Felina] Native Library returned wrong number: {magic}" );
                }
            }
            catch ( System.Exception e )
            {
                Debug.LogError( $"[Felina] FAILED to load Native Library: {e.Message}" );
            }

            if ( !_arCamera ) _arCamera = _arBridge.GetARCamera();

            if ( _unwarpedTexture != null ) _unwarpedTexture.Release();

            var rtSettings = _arBridge.RenderTextureSettings;

            _unwarpedTexture = new RenderTexture( rtSettings.Width, rtSettings.Height, 0, rtSettings.Format )
            {
                useMipMap = rtSettings.UseMipMap,
                autoGenerateMips = rtSettings.AutoGenerateMips,
                filterMode = rtSettings.FilterMode
            };
            _unwarpedTexture.Create();

            _nativeScreenPoints = new NativeArray<float2>( 4, Allocator.Persistent );
            _nativeResultMatrix = new NativeArray<float4x4>( 1, Allocator.Persistent );

            if ( _arCamera != null )
            {
                _lastCamPos = _arCamera.transform.position;
                _lastCamRot = _arCamera.transform.rotation;
            }
        }

        void OnDestroy()
        {
            if ( _nativeScreenPoints.IsCreated ) _nativeScreenPoints.Dispose();
            if ( _nativeResultMatrix.IsCreated ) _nativeResultMatrix.Dispose();
        }


        void OnEnable() { if ( _arBridge != null ) { _arBridge.OnTargetAdded += OnTargetAdded; } }

        void OnDisable() { if ( _arBridge != null ) { _arBridge.OnTargetAdded -= OnTargetAdded; } }

        void Update()
        {
            CalculateStability();
        }

        private void OnTargetAdded( ScanTarget target )
        {
            if ( _isLocked ) return;


            TrackAndPaint( target, this.GetCancellationTokenOnDestroy() ).Forget();

            //foreach ( var t in param.updated ) TrackAndPaint( t, this.GetCancellationTokenOnDestroy() ).Forget();



            //if ( _isLocked ) return;

            //foreach ( var t in param.added )
            //{
            //    TrackAndPaint( t, this.GetCancellationTokenOnDestroy() ).Forget();
            //}

            //foreach ( var t in param.updated ) TrackAndPaint( t, this.GetCancellationTokenOnDestroy() ).Forget();

            //if ( !_activeTargets.ContainsKey( target.Name ) )
            //    _activeTargets.Add( target.Name, target );
        }

        private async UniTaskVoid TrackAndPaint( ScanTarget target, CancellationToken token )
        {
            // Simple loop while tracking
            while ( ( !_isLocked ) && !token.IsCancellationRequested )
            {
                if ( target.IsTracking )
                {
                    //CalculateStability();

                    if ( IsDeviceStable )
                    {
                        CalculateQualityScore( ref target );

                        // Debug Log to see why it fails
                        // Debug.Log($"[Quality] Score: {currentScore} (Best: {_highestQualityScore})");

                        if ( target.Score > _highestQualityScore )
                        {
                            _highestQualityScore = target.Score;

                            var cameraFeedRT = _arBridge.GetCameraFeedRT();
                            //EnsureCameraFeedIsFresh();
                            ProcessImage( target, cameraFeedRT );
                        }

                        if ( _autoLock && target.Score >= _captureThreshold )
                        {
                            _isLocked = true;
                            Debug.Log( $"[Felina] Locked! Final Score: {_highestQualityScore}" );
                            return;
                        }
                    }
                }
                await UniTask.NextFrame( token );
            }
        }

        private void CalculateStability()
        {
            if ( _arCamera == null ) return;

            float3 currentPos = _arCamera.transform.position;
            quaternion currentRot = _arCamera.transform.rotation;
            float dt = Time.deltaTime;

            // CALL NATIVE C++
            // We pass the raw structs directly. C# structs (float3, quaternion) 
            // are "blittable", meaning they copy perfectly to C++ memory.
            IsDeviceStable = CheckStability(
                currentPos, currentRot,
                _lastCamPos, _lastCamRot,
                dt,
                _maxMoveSpeed, _maxRotateSpeed
            );

            // Update state for next frame
            _lastCamPos = currentPos;
            _lastCamRot = currentRot;
        }

        // --- FIX: VISIBLE QUALITY LOGIC ---
        private float CalculateQualityScore( ref ScanTarget target )
        {
            // 1. Gather Data
            float3 camPos = _arCamera.transform.position;
            float3 camFwd = _arCamera.transform.forward;
            float3 imgPos = target.Position;
            float3 imgUp = target.Transform.up;

            // 2. Screen Position (Still needs Unity Camera to calculate)
            float3 screenPos3 = _arCamera.WorldToScreenPoint( imgPos );

            // Handle "Behind Camera" case by passing invalid data to C++
            float2 screenPos = ( screenPos3.z > 0 ) ? new float2( screenPos3.x, screenPos3.y ) : new float2( -1, -1 );

            // 3. CALL NATIVE C++
            return CalculateQuality(
                camPos, camFwd,
                imgPos, imgUp,
                screenPos,
                Screen.width, Screen.height
            );
        }

        //private unsafe void ProcessImage( ARTrackedImage image ) //ScanTarget target, float score
        private unsafe void ProcessImage( ScanTarget target, RenderTexture cameraFeedRT )
        {
            if ( _unwarpMaterial == null || _unwarpedTexture == null || _arCamera == null ) return;

            // 1. Get Physical Size (No Aspect Ratio calculation needed here anymore!)
            float imgW = target.Size.x;
            float imgH = target.Size.y;

            // 2. Prepare Raw Screen Points (Pixels)
            float halfX = imgW * 0.5f;
            float halfY = imgH * 0.5f;
            Transform t = target.Transform;

            // Note: Using a helper to get Raw Pixels (not normalized)
            _nativeScreenPoints[ 0 ] = ScreenPoint( t.TransformPoint( new float3( -halfX, 0, -halfY ) ) );
            _nativeScreenPoints[ 1 ] = ScreenPoint( t.TransformPoint( new float3( halfX, 0, -halfY ) ) );
            _nativeScreenPoints[ 2 ] = ScreenPoint( t.TransformPoint( new float3( halfX, 0, halfY ) ) );
            _nativeScreenPoints[ 3 ] = ScreenPoint( t.TransformPoint( new float3( -halfX, 0, halfY ) ) );

            // 3. Call Native C++
            // We pass the raw image size and screen resolution. C++ does the rest.
            CalculateHomography(
                imgW, imgH,
                Screen.width, Screen.height,
                _nativeScreenPoints.GetUnsafePtr(),
                _nativeResultMatrix.GetUnsafePtr()
            );

            // 4. Apply Result (Same as before)
            float4x4 H = _nativeResultMatrix[ 0 ];
            if ( H.c3.w == 0 ) return;

            _unwarpMaterial.SetTexture( "_MainTex", cameraFeedRT );
            _unwarpMaterial.SetMatrix( "_Homography", H );
            _unwarpMaterial.SetMatrix( "_DisplayMatrix", float4x4.identity );

            Graphics.Blit( null, _unwarpedTexture, _unwarpMaterial );

            if ( _targetObjectMaterial != null )
            {
                _targetObjectMaterial.SetTexture( "_BaseMap", _unwarpedTexture );
                _targetObjectMaterial.SetTexture( "_MainTex", _unwarpedTexture );
                _targetObjectMaterial.SetTexture( "_DrawingTex", _unwarpedTexture );
            }
            OnTextureCaptured?.Invoke( target.Name, _unwarpedTexture, target.Score );
        }

        private float2 ScreenPoint( float3 worldPos )
        {
            float3 s = _arCamera.WorldToScreenPoint( worldPos );
            return new float2( s.x, s.y );
        }

        public void ResetCapture()
        {
            _isLocked = false;
            _highestQualityScore = 0f;
        }
    }
}


//using System;
//using System.Collections.Generic;
//using UnityEngine;
//using UnityEngine.Rendering;
//using Unity.Collections;
//using Unity.Collections.LowLevel.Unsafe;
//using Unity.Mathematics;

//namespace Felina.ARColoringBook
//{
//    public class ARScannerManager : MonoBehaviour
//    {
//        public static ARScannerManager Instance { get; private set; }

//        // --- NATIVE IMPORTS (The C++ Library we built) ---
//#if UNITY_EDITOR || UNITY_STANDALONE
//        [System.Runtime.InteropServices.DllImport( "Felina" )]
//#elif UNITY_ANDROID
//        [System.Runtime.InteropServices.DllImport("Felina")]
//#endif
//        private static extern float CalculateQuality( float3 camPos, float3 camFwd, float3 imgPos, float3 imgUp, float2 imgScreenPos, float sW, float sH );

//#if UNITY_EDITOR || UNITY_STANDALONE
//        [System.Runtime.InteropServices.DllImport( "Felina" )]
//#elif UNITY_ANDROID
//        [System.Runtime.InteropServices.DllImport("Felina")]
//#endif
//        private static unsafe extern void CalculateHomography( float imgW, float imgH, float sW, float sH, void* screenPts, void* result );

//        // --- COMPONENTS ---
//        [Header( "Configuration" )]
//        [SerializeField] private MonoBehaviour _arBridgeComponent;
//        private IARBridge _arBridge;

//        [Header( "Scanner Settings" )]
//        public Material unwarpMaterial;
//        public int outputResolution = 1024;
//        [Range( 0f, 1f )] public float captureThreshold = 0.70f;

//        // Events
//        public event Action<string, Texture2D, float> OnTextureCaptured;

//        // Internal State
//        private RenderTexture _cameraFeedRT;
//        private RenderTexture _tempUnwarpRT;
//        private Camera _arCamera;
//        private Dictionary<string, ScanTarget> _activeTargets = new Dictionary<string, ScanTarget>();
//        private Dictionary<string, bool> _isLocked = new Dictionary<string, bool>();
//        private Dictionary<string, float> _bestScores = new Dictionary<string, float>();

//        // Native Buffers (Persistent)
//        private NativeArray<float2> _nativeScreenPoints;
//        private NativeArray<float4x4> _nativeResultMatrix;

//        private void Awake()
//        {
//            if ( Instance != null && Instance != this ) Destroy( gameObject );
//            else Instance = this;

//            if ( _arBridgeComponent is IARBridge bridge ) _arBridge = bridge;
//            else Debug.LogError( "[Felina] Assigned AR Bridge is invalid!" );
//        }

//        void Start()
//        {
//            if ( _arBridge != null ) _arCamera = _arBridge.GetARCamera();

//            // Setup Textures
//            _cameraFeedRT = new RenderTexture( Screen.width, Screen.height, 0, RenderTextureFormat.RGB565 );
//            _cameraFeedRT.Create();

//            _tempUnwarpRT = new RenderTexture( outputResolution, outputResolution, 0, RenderTextureFormat.RGB565 );
//            _tempUnwarpRT.Create();

//            // Setup Native Memory
//            _nativeScreenPoints = new NativeArray<float2>( 4, Allocator.Persistent );
//            _nativeResultMatrix = new NativeArray<float4x4>( 1, Allocator.Persistent );
//        }

//        void OnDestroy()
//        {
//            if ( _nativeScreenPoints.IsCreated ) _nativeScreenPoints.Dispose();
//            if ( _nativeResultMatrix.IsCreated ) _nativeResultMatrix.Dispose();
//            if ( _cameraFeedRT != null ) _cameraFeedRT.Release();
//            if ( _tempUnwarpRT != null ) _tempUnwarpRT.Release();
//        }

//        void OnEnable()
//        {
//            if ( _arBridge != null )
//            {
//                _arBridge.OnTargetAdded += OnTargetAdded;
//            }
//        }

//        void OnDisable()
//        {
//            if ( _arBridge != null )
//            {
//                _arBridge.OnTargetAdded -= OnTargetAdded;
//            }
//        }

//        // --- THE MISSING LOOP ---
//        void Update()
//        {
//            if ( _arCamera == null ) return;

//            // Iterate over all currently tracked targets
//            foreach ( var kvp in _activeTargets )
//            {
//                ScanTarget target = kvp.Value;

//                // 1. Skip if locked
//                if ( _isLocked.ContainsKey( target.Name ) && _isLocked[ target.Name ] ) continue;

//                // 2. Update Transform Data (Since ScanTarget is a struct, we must rely on the Transform ref)
//                if ( target.Transform == null ) continue;

//                // 3. Native Quality Check
//                float score = GetNativeQuality( target );

//                if ( !_bestScores.ContainsKey( target.Name ) ) _bestScores[ target.Name ] = 0f;

//                // 4. Capture if better
//                if ( score > _bestScores[ target.Name ] )
//                {
//                    _bestScores[ target.Name ] = score;
//                    ProcessCapture( target, score );
//                }
//            }
//        }

//        private void OnTargetAdded( ScanTarget target )
//        {
//            if ( !_activeTargets.ContainsKey( target.Name ) )
//            {
//                _activeTargets.Add( target.Name, target );
//                Debug.Log( $"[Felina] Tracking started for: {target.Name}" );
//            }
//        }

//        private void OnTargetRemoved( ScanTarget target )
//        {
//            if ( _activeTargets.ContainsKey( target.Name ) )
//            {
//                _activeTargets.Remove( target.Name );
//            }
//        }

//        private float GetNativeQuality( ScanTarget target )
//        {
//            float3 camPos = _arCamera.transform.position;
//            float3 camFwd = _arCamera.transform.forward;
//            float3 imgPos = target.Transform.position;
//            float3 imgUp = target.Transform.up;

//            float3 screenPos3 = _arCamera.WorldToScreenPoint( imgPos );
//            // Handle "behind camera"
//            float2 screenPos = ( screenPos3.z > 0 ) ? new float2( screenPos3.x, screenPos3.y ) : new float2( -1, -1 );

//            return CalculateQuality( camPos, camFwd, imgPos, imgUp, screenPos, Screen.width, Screen.height );
//        }

//        private unsafe void ProcessCapture( ScanTarget target, float score )
//        {
//            // 1. Get Camera Feed
//            _arBridge.FillCameraTexture( _cameraFeedRT );

//            // 2. Calculate Screen Points
//            Vector2 size = target.Size;
//            float hx = size.x * 0.5f; float hy = size.y * 0.5f;
//            Transform t = target.Transform;

//            _nativeScreenPoints[ 0 ] = ToScreen( t.TransformPoint( new Vector3( -hx, 0, -hy ) ) );
//            _nativeScreenPoints[ 1 ] = ToScreen( t.TransformPoint( new Vector3( hx, 0, -hy ) ) );
//            _nativeScreenPoints[ 2 ] = ToScreen( t.TransformPoint( new Vector3( hx, 0, hy ) ) );
//            _nativeScreenPoints[ 3 ] = ToScreen( t.TransformPoint( new Vector3( -hx, 0, hy ) ) );

//            // 3. Call Native Homography
//            CalculateHomography(
//                size.x, size.y,
//                Screen.width, Screen.height,
//                _nativeScreenPoints.GetUnsafePtr(),
//                _nativeResultMatrix.GetUnsafePtr()
//            );

//            float4x4 H = _nativeResultMatrix[ 0 ];
//            if ( H.c3.w == 0 ) return; // Native solver rejected it

//            // 4. Apply & Blit
//            unwarpMaterial.SetTexture( "_MainTex", _cameraFeedRT );
//            unwarpMaterial.SetMatrix( "_Homography", H );
//            unwarpMaterial.SetMatrix( "_DisplayMatrix", Matrix4x4.identity );

//            Graphics.Blit( null, _tempUnwarpRT, unwarpMaterial );

//            // 5. Async Readback
//            AsyncGPUReadback.Request( _tempUnwarpRT, 0, TextureFormat.RGB24, ( req ) =>
//            {
//                if ( req.hasError ) return;

//                Texture2D tex = new Texture2D( outputResolution, outputResolution, TextureFormat.RGB24, false );
//                tex.LoadRawTextureData( req.GetData<byte>() );
//                tex.Apply();

//                // Fire Event
//                OnTextureCaptured?.Invoke( target.Name, tex, score );

//                // Lock?
//                if ( score >= captureThreshold )
//                {
//                    _isLocked[ target.Name ] = true;
//                    Debug.Log( $"[Felina] LOCKED {target.Name} (Score: {score:F2})" );
//                }
//            } );
//        }

//        private float2 ToScreen( Vector3 worldPos )
//        {
//            Vector3 s = _arCamera.WorldToScreenPoint( worldPos );
//            return new float2( s.x, s.y );
//        }

//        public Texture2D GetCapturedTexture( string targetName )
//        {
//            // Implement simple caching logic here if needed
//            return null;
//        }
//    }
//}