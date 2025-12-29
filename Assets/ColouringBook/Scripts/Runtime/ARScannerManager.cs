using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using Unity.Burst;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Felina.ARColoringBook
{
    /// <summary>
    /// GPU performance quality presets
    /// </summary>
    public enum QualityPreset
    {
        /// <summary>Ultra: Full resolution cache (highest quality, slowest)</summary>
        Ultra,
        /// <summary>High: 75% resolution cache (great quality, faster)</summary>
        High,
        /// <summary>Medium: 50% resolution cache (good quality, much faster)</summary>
        Medium,
        /// <summary>Low: 25% resolution cache (acceptable quality, fastest)</summary>
        Low
    }

    public class ARScannerManager : MonoBehaviour
    {
        public static ARScannerManager Instance { get; private set; }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern bool CheckStability( float3 f1, quaternion q1, float3 f2, quaternion q2, float f3, float f4, float f5 );
        [DllImport("__Internal")] private static extern float CalculateQuality( float3 f1, float3 f2, float3 f3, float3 f4, float2 f5, float f6, float f7 );
        [DllImport("__Internal")] private static unsafe extern void ComputeTransformMatrix( float f1, float f2, void* s, void* r );
#else
        [DllImport( "Felina" )] private static extern bool CheckStability( float3 f1, quaternion q1, float3 f2, quaternion q2, float f3, float f4, float f5 );
        [DllImport( "Felina" )] private static extern float CalculateQuality( float3 f1, float3 f2, float3 f3, float3 f4, float2 f5, float f6, float f7 );
        [DllImport( "Felina" )] private static unsafe extern void ComputeTransformMatrix( float f1, float f2, void* s, void* r );
#endif

        [Header( "Architecture" )]
        private IARBridge _arBridge;
        [SerializeField] private MonoBehaviour _arBridgeComponent;
        [Tooltip( "Reference to ARTrackedImageManager for accessing the image library" )]
        [SerializeField] private ARTrackedImageManager _trackedImageManager;

        /// <summary>
        /// Public accessor for the reference image library (reads from ARTrackedImageManager)
        /// </summary>
        [SerializeField]
        private XRReferenceImageLibrary _library;
        

        [Header( "Scanner Settings" )]
        [SerializeField] private int _outputResolution = Internals.DEFAULT_OUTPUT_RESOLUTION;
        [Range( 0f, 1f ), SerializeField] private float _captureThreshold = Internals.DEFAULT_CAPTURE_THRESHOLD;
        [SerializeField] private bool _autoLock = true;
        [SerializeField] private float _maxMoveSpeed = Internals.DEFAULT_MAX_MOVE_SPEED;
        [SerializeField] private float _maxRotateSpeed = Internals.DEFAULT_MAX_ROTATE_SPEED;

        [Header( "GPU Performance" )]
        [Tooltip( "Quality preset for camera feed processing" )]
        [SerializeField] private QualityPreset _qualityPreset = QualityPreset.High;
        [SerializeField] private Material _unwarpMaterial;
        [Tooltip( "Use direct blit (skip cache) - faster but no frame reuse" )]
        [SerializeField] private bool _useDirectBlit = false;
        [Tooltip( "Use combined shader (single-pass unwarp) - fastest, requires CombinedUnwarpShader" )]
        [SerializeField] private bool _useCombinedShader = false;
        [Tooltip( "Combined unwarp material (assign CombinedUnwarpShader material)" )]
        [SerializeField] private Material _combinedUnwarpMaterial;

        public event Action<string, RenderTexture, float> OnTextureCaptured;
        private Camera _arCamera;
        [SerializeField, HideInInspector] private List<ScanTarget> _targetList = new();
        private Dictionary<string, ScanTarget> _activeTargets = new();
        private Dictionary<string, bool> _isLocked = new();
        private Dictionary<string, float> _bestScores = new();
        private Dictionary<string, RenderTexture> _capturedTextures = new();

        private NativeArray<float2> _nativeScreenPoints;
        private NativeArray<float4x4> _nativeResultMatrix;
        private float3 _lastCamPos;
        private quaternion _lastCamRot;
        public bool IsDeviceStable { get; private set; } = false;

        private RenderTexture _cameraFeedCache;
        private int _screenWidth;
        private int _screenHeight;

        // Cache material property IDs for better performance
        private static readonly int MainTexPropertyID = Shader.PropertyToID( "_MainTex" );
        private static readonly int UnwarpPropertyID = Shader.PropertyToID( "_Unwarp" );
        private static readonly int DisplayMatrixPropertyID = Shader.PropertyToID( "_DisplayMatrix" );
        private static readonly Matrix4x4 IdentityMatrix = Matrix4x4.identity;

        private void Awake()
        {
            Instance = this;

            if ( _arBridgeComponent is IARBridge bridge ) _arBridge = bridge;
            else Debug.LogError( "[Felina] ARScannerManager: AR Bridge component does not implement IARBridge!" );

            _activeTargets.Clear();
            foreach ( var t in _targetList )
            {
                if ( !_activeTargets.ContainsKey( t.Name ) ) _activeTargets.Add( t.Name, t );
            }
        }

        void Start()
        {
            if ( _arBridge != null ) _arCamera = _arBridge.GetARCamera();
            _nativeScreenPoints = new NativeArray<float2>( 4, Allocator.Persistent );
            _nativeResultMatrix = new NativeArray<float4x4>( 1, Allocator.Persistent );

            if ( _arCamera != null )
            {
                _lastCamPos = _arCamera.transform.position;
                _lastCamRot = _arCamera.transform.rotation;
            }

            // Cache screen dimensions
            _screenWidth = Screen.width;
            _screenHeight = Screen.height;

            // Create camera feed cache with quality scaling
            if ( _arBridge != null )
            {
                var settings = _arBridge.RenderTextureSettings;

                // Apply quality preset to reduce resolution (huge GPU savings!)
                float scale = GetQualityScale( _qualityPreset );
                int cacheWidth = Mathf.Max( 64, Mathf.RoundToInt( settings.Width * scale ) );
                int cacheHeight = Mathf.Max( 64, Mathf.RoundToInt( settings.Height * scale ) );

                _cameraFeedCache = new RenderTexture( cacheWidth, cacheHeight, 0, settings.Format )
                {
                    useMipMap = false,  // Never use mipmaps for intermediate cache
                    autoGenerateMips = false,
                    filterMode = FilterMode.Bilinear  // Bilinear is fastest for downsampling
                };
                _cameraFeedCache.Create();

                // Only set target RT if not using direct blit mode
                if ( !_useDirectBlit )
                {
                    _arBridge.SetTargetRenderTexture( _cameraFeedCache );
                }

                // Pre-create output RTs for all known targets
                PreCreateOutputTextures();
            }
        }

        /// <summary>
        /// Get resolution scale factor for quality preset
        /// </summary>
        private float GetQualityScale( QualityPreset preset )
        {
            return preset switch
            {
                QualityPreset.Ultra => 1.0f,    // 100% (e.g., 1920×1080)
                QualityPreset.High => 0.75f,     // 75%  (e.g., 1440×810)
                QualityPreset.Medium => 0.5f,    // 50%  (e.g., 960×540)  ? Recommended
                QualityPreset.Low => 0.25f,      // 25%  (e.g., 480×270)
                _ => 0.5f
            };
        }

        private void PreCreateOutputTextures()
        {
            if ( _arBridge == null ) return;

            var chosenFormat = _arBridge.RenderTextureSettings.Format;

            if ( chosenFormat == RenderTextureFormat.Default )
            {
                if ( SystemInfo.SupportsRenderTextureFormat( RenderTextureFormat.RGB565 ) )
                    chosenFormat = RenderTextureFormat.RGB565;
                else if ( SystemInfo.SupportsRenderTextureFormat( RenderTextureFormat.ARGB32 ) )
                    chosenFormat = RenderTextureFormat.ARGB32;
            }

            // Get library from ARTrackedImageManager (single source of truth)
            var library = _library;
            if ( library != null )
            {
                for ( int i = 0; i < library.count; i++ )
                {
                    var imageName = library[ i ].name;

                    var rt = new RenderTexture( _outputResolution, _outputResolution, 0, chosenFormat )
                    {
                        useMipMap = false,
                        autoGenerateMips = false,
                        filterMode = FilterMode.Bilinear
                    };
                    rt.Create();
                    _capturedTextures[ imageName ] = rt;
                }
            }
            else
            {
                Debug.LogWarning( "[Felina] No reference image library found! Make sure ARTrackedImageManager is assigned and has a library." );
            }

            // Debug.Log( $"[Felina] Pre-created {_capturedTextures.Count} output textures at {_outputResolution}×{_outputResolution} resolution." );
        }

        void OnDestroy()
        {
            if ( _nativeScreenPoints.IsCreated ) _nativeScreenPoints.Dispose();
            if ( _nativeResultMatrix.IsCreated ) _nativeResultMatrix.Dispose();
            foreach ( var rt in _capturedTextures.Values ) if ( rt != null ) rt.Release();
            _capturedTextures.Clear();
            if ( _cameraFeedCache != null ) _cameraFeedCache.Release();
        }

        void OnEnable() { if ( _arBridge != null ) _arBridge.OnTargetAdded += OnTargetAdded; }
        void OnDisable() { if ( _arBridge != null ) _arBridge.OnTargetAdded -= OnTargetAdded; }

        private int _frameCounter = 0;
        private const int PROCESS_INTERVAL = 3; // Process every 3 frames instead of every frame
        private bool _pendingProcess = false;
        private struct PendingCapture
        {
            public ScanTarget Target;
            public float Score;
        }
        // Avoid Clear() allocations with pre-sized capacity
        private List<PendingCapture> _pendingCaptures = new( 10 ); // Clear() is O(1) for Stack vs List

        void Update()
        {
            if ( _arCamera == null ) return;

            CalculateNativeStability();
            if ( !IsDeviceStable ) return;

            // Throttle processing to reduce GPU/CPU load
            _frameCounter++;
            if ( _frameCounter < PROCESS_INTERVAL ) return;
            _frameCounter = 0;

            // Frame N: Update camera feed cache (or skip if using direct blit)
            if ( !_useDirectBlit )
            {
                _arBridge.GetCameraFeedRT();  // Blit camera ? cache

                if ( _cameraFeedCache == null ) return;
            }

            // Evaluate all targets and queue captures
            _pendingCaptures.Clear();
            foreach ( var kvp in _activeTargets )
            {
                var target = kvp.Value;

                // Use TryGetValue to avoid double dictionary lookup
                if ( _isLocked.TryGetValue( target.Name, out var isLocked ) && isLocked ) continue;
                if ( target.Transform == null ) continue;

                var score = GetNativeQuality( target.Transform );

                // Use TryGetValue for best scores too
                if ( !_bestScores.TryGetValue( target.Name, out var bestScore ) )
                {
                    bestScore = 0f;
                    _bestScores[ target.Name ] = bestScore;
                }

                // Add threshold to prevent processing for tiny improvements
                const float MIN_IMPROVEMENT = 0.02f;
                if ( score > bestScore + MIN_IMPROVEMENT )
                {
                    _bestScores[ target.Name ] = score;
                    _pendingCaptures.Add( new PendingCapture { Target = target, Score = score } );
                }
            }



            // Signal that we have captures to process next frame
            _pendingProcess = _pendingCaptures.Count > 0;
        }

        void LateUpdate()
        {
            // Frame N+1: Process queued captures (Blit #2: cache ? unwarp)
            if ( !_pendingProcess ) return;
            _pendingProcess = false;

            foreach ( var capture in _pendingCaptures )
            {
                ProcessCaptureGPU( capture.Target, capture.Score );
            }
        }

        private unsafe void ProcessCaptureGPU( ScanTarget target, float score )
        {
            if ( !_nativeScreenPoints.IsCreated || !_nativeResultMatrix.IsCreated ) return;

            // Direct blit mode requires valid cache, cached mode checks in Update()
            if ( !_useDirectBlit && _cameraFeedCache == null ) return;

            // RT should already exist; if not, something went wrong
            if ( !_capturedTextures.TryGetValue( target.Name, out var destRT ) )
            {
                Debug.LogError( $"[Felina] No output RT found for target '{target.Name}'! This should have been pre-created." );
                return;
            }

            var size = target.Size;
            var hx = size.x * 0.5f;
            var hy = size.y * 0.5f;
            var t = target.Transform;

            // Cache transform using Unity.Mathematics types for better performance
            var tPos = ( float3 ) t.position;
            var tRight = ( float3 ) t.right;
            var tForward = ( float3 ) t.forward;

            // Calculate corner positions using Unity.Mathematics
            var halfExtentX = tRight * hx;
            var halfExtentZ = tForward * hy;

            _nativeScreenPoints[ 0 ] = ToScreen( tPos - halfExtentX - halfExtentZ );
            _nativeScreenPoints[ 1 ] = ToScreen( tPos + halfExtentX - halfExtentZ );
            _nativeScreenPoints[ 2 ] = ToScreen( tPos + halfExtentX + halfExtentZ );
            _nativeScreenPoints[ 3 ] = ToScreen( tPos - halfExtentX + halfExtentZ );

            ComputeTransformMatrix( _screenWidth, _screenHeight, _nativeScreenPoints.GetUnsafePtr(), _nativeResultMatrix.GetUnsafePtr() );

            var H = _nativeResultMatrix[ 0 ];

            // GPU Optimization: Choose rendering path based on configuration
            if ( _useCombinedShader && _combinedUnwarpMaterial != null )
            {
                // COMBINED SHADER MODE: Single-pass from camera ? output (50% faster!)
                // This is the FASTEST option - eliminates intermediate cache entirely

                // Get AR camera background material
                var arCamBackground = _arBridge.GetARCameraBackground();
                if ( arCamBackground != null )
                {
                    // Copy camera texture to combined shader
                    var camTexture = arCamBackground.material.GetTexture( "_MainTex" );
                    _combinedUnwarpMaterial.SetTexture( MainTexPropertyID, camTexture );
                    _combinedUnwarpMaterial.SetMatrix( UnwarpPropertyID, H );
                    _combinedUnwarpMaterial.SetMatrix( DisplayMatrixPropertyID, IdentityMatrix );

                    // Single blit: Camera ? Output (with unwarp applied in shader)
                    Graphics.Blit( null, destRT, _combinedUnwarpMaterial );
                }
                else
                {
                    Debug.LogWarning( "[Felina] ARCameraBackground not found, falling back to cached mode" );
                    // Fallback to cached mode
                    _unwarpMaterial.SetTexture( MainTexPropertyID, _cameraFeedCache );
                    _unwarpMaterial.SetMatrix( UnwarpPropertyID, H );
                    _unwarpMaterial.SetMatrix( DisplayMatrixPropertyID, IdentityMatrix );
                    Graphics.Blit( null, destRT, _unwarpMaterial );
                }
            }
            else if ( _useDirectBlit )
            {
                // DIRECT BLIT MODE: Single pass from camera ? output
                // Saves 1 intermediate RT copy (30-40% faster!)
                _arBridge.GetCameraFeedRT();  // Update camera feed

                // Use AR camera background material as source
                var arCamMaterial = _arBridge.GetARCameraBackground()?.material;
                if ( arCamMaterial != null )
                {
                    // Apply homography to camera background shader
                    arCamMaterial.SetMatrix( UnwarpPropertyID, H );
                    Graphics.Blit( null, destRT, arCamMaterial );
                    // Note: This modifies the AR background temporarily, but it's reset next frame
                }
                else
                {
                    // Fallback: Use cached mode
                    _unwarpMaterial.SetTexture( MainTexPropertyID, _cameraFeedCache );
                    _unwarpMaterial.SetMatrix( UnwarpPropertyID, H );
                    _unwarpMaterial.SetMatrix( DisplayMatrixPropertyID, IdentityMatrix );
                    Graphics.Blit( null, destRT, _unwarpMaterial );
                }
            }
            else
            {
                // CACHED MODE: Two-pass blit (camera ? cache ? output)
                // Slower but allows frame reuse if multiple targets processed
                _unwarpMaterial.SetTexture( MainTexPropertyID, _cameraFeedCache );
                _unwarpMaterial.SetMatrix( UnwarpPropertyID, H );
                _unwarpMaterial.SetMatrix( DisplayMatrixPropertyID, IdentityMatrix );
                Graphics.Blit( null, destRT, _unwarpMaterial );
            }

            OnTextureCaptured?.Invoke( target.Name, destRT, score );

            if ( _autoLock && score >= _captureThreshold )
            {
                _isLocked[ target.Name ] = true;
            }
        }

        private void CalculateNativeStability()
        {
            _arCamera.transform.GetPositionAndRotation( out var curPos, out var curRot );
            var dt = Time.deltaTime;
            IsDeviceStable = CheckStability( curPos, curRot, _lastCamPos, _lastCamRot, dt, _maxMoveSpeed, _maxRotateSpeed );
            _lastCamPos = curPos;
            _lastCamRot = curRot;
        }

        private float GetNativeQuality( Transform targetTransform )
        {
            _arCamera.transform.GetPositionAndRotation( out var camPosVec, out var camRot );
            var camPos = ( float3 ) camPosVec;
            var camFwd = ( float3 ) _arCamera.transform.forward;
            var imgPos = ( float3 ) targetTransform.position;
            var imgUp = ( float3 ) targetTransform.up;
            var sPos3 = _arCamera.WorldToScreenPoint( imgPos );
            var sPos = ( sPos3.z > 0 ) ? new float2( sPos3.x, sPos3.y ) : new float2( -1, -1 );
            return CalculateQuality( camPos, camFwd, imgPos, imgUp, sPos, _screenWidth, _screenHeight );
        }

        private float2 ToScreen( float3 worldPos )
        {
            var s = _arCamera.WorldToScreenPoint( worldPos );
            return new float2( s.x, s.y );
        }

        // Burst-compiled vector math helpers for better performance
        [BurstCompile]
        private static float3 CalculateCornerOffset( float3 right, float3 forward, float hx, float hz, int cornerId )
        {
            // Corner pattern: 0=(-x,-z), 1=(+x,-z), 2=(+x,+z), 3=(-x,+z)
            var signX = ( cornerId & 1 ) == 0 ? -1f : 1f;
            var signZ = ( cornerId & 2 ) == 0 ? -1f : 1f;
            return right * ( hx * signX ) + forward * ( hz * signZ );
        }

        [BurstCompile]
        private static float3 Add( float3 a, float3 b ) => a + b;

        private void OnTargetAdded( ScanTarget incomingTarget )
        {
            if ( _activeTargets.ContainsKey( incomingTarget.Name ) )
            {
                var existing = _activeTargets[ incomingTarget.Name ];
                existing.Transform = incomingTarget.Transform;
                existing.IsTracking = true;
                _activeTargets[ incomingTarget.Name ] = existing;
            }
            else
            {
                _activeTargets.Add( incomingTarget.Name, incomingTarget );
            }
        }

        public RenderTexture GetCapturedTexture( string targetName ) => _capturedTextures.ContainsKey( targetName ) ? _capturedTextures[ targetName ] : null;
        public void AddScanTarget( ScanTarget newTarget ) { _targetList.Add( newTarget ); }

        /// <summary>
        /// Get image name by GUID (useful for ARTrackedImage.referenceImage.guid)
        /// </summary>
        public string GetImageName( System.Guid guid )
        {
            var library = _library;
            if ( library == null ) return null;

            for ( int i = 0; i < library.count; i++ )
            {
                if ( library[ i ].guid == guid )
                    return library[ i ].name;
            }
            return null;
        }

        /// <summary>
        /// Get image name by index
        /// </summary>
        public string GetImageName( int index )
        {
            var library = _library;
            if ( library == null || index < 0 || index >= library.count )
                return null;

            return library[ index ].name;
        }
    }
}
