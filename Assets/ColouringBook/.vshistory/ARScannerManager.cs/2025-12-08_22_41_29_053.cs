using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Felina.ARColoringBook
{
    public class ARScannerManager : MonoBehaviour
    {
        public static ARScannerManager Instance { get; private set; }

        // --- NATIVE IMPORTS ---
#if UNITY_EDITOR || UNITY_STANDALONE
        [System.Runtime.InteropServices.DllImport( "Felina" )]
#elif UNITY_ANDROID
        [System.Runtime.InteropServices.DllImport("Felina")]
#endif
        private static extern bool CheckStability( float3 curPos, quaternion curRot, float3 lastPos, quaternion lastRot, float dt, float maxMove, float maxRot );

#if UNITY_EDITOR || UNITY_STANDALONE
        [System.Runtime.InteropServices.DllImport( "Felina" )]
#elif UNITY_ANDROID
        [System.Runtime.InteropServices.DllImport("Felina")]
#endif
        private static extern float CalculateQuality( float3 camPos, float3 camFwd, float3 imgPos, float3 imgUp, float2 imgScreenPos, float sW, float sH );

#if UNITY_EDITOR || UNITY_STANDALONE
        [System.Runtime.InteropServices.DllImport( "Felina" )]
#elif UNITY_ANDROID
        [System.Runtime.InteropServices.DllImport("Felina")]
#endif
        private static unsafe extern void CalculateHomography( float imgW, float imgH, float sW, float sH, void* screenPts, void* result );

        // --- COMPONENTS ---
        [Header( "Architecture" )]
        [SerializeField] private MonoBehaviour arBridgeComponent;
        private IARBridge _arBridge;

        [Header( "Scanner Settings" )]
        public Material unwarpMaterial;
        public int outputResolution = 1024;
        [Range( 0f, 1f )] public float captureThreshold = 0.70f;
        public bool autoLock = true;

        [Header( "Stability" )]
        public float maxMoveSpeed = 0.15f;
        public float maxRotateSpeed = 10.0f;

        // EVENT: Passes RenderTexture (GPU Handle) instead of Texture2D (CPU Memory)
        public event Action<string, RenderTexture, float> OnTextureCaptured;

        // State
        private Camera _arCamera;

        // Tracking Lists
        private Dictionary<string, ScanTarget> _activeTargets = new Dictionary<string, ScanTarget>();
        private Dictionary<string, bool> _isLocked = new Dictionary<string, bool>();
        private Dictionary<string, float> _bestScores = new Dictionary<string, float>();

        // GPU Cache: Keeps the textures alive on the GPU
        private Dictionary<string, RenderTexture> _capturedTextures = new Dictionary<string, RenderTexture>();

        // Native Buffers (Reusable)
        private NativeArray<float2> _nativeScreenPoints;
        private NativeArray<float4x4> _nativeResultMatrix;

        // Stability State
        private float3 _lastCamPos;
        private quaternion _lastCamRot;
        public bool IsDeviceStable { get; private set; } = false;

        private void Awake()
        {
            if ( Instance != null && Instance != this ) Destroy( gameObject );
            else Instance = this;

            if ( arBridgeComponent is IARBridge bridge ) _arBridge = bridge;
            else Debug.LogError( "[Felina] ARScannerManager: Bridge is invalid!" );
        }

        void Start()
        {
            if ( _arBridge != null ) _arCamera = _arBridge.GetARCamera();

            // Allocate Native Memory (Persistent)
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

            // Clean up GPU memory
            foreach ( var rt in _capturedTextures.Values )
            {
                if ( rt != null ) rt.Release();
            }
            _capturedTextures.Clear();
        }

        void OnEnable() { if ( _arBridge != null ) _arBridge.OnTargetAdded += OnTargetAdded; }
        void OnDisable() { if ( _arBridge != null ) _arBridge.OnTargetAdded -= OnTargetAdded; }

        // --- THE PHOTOGRAPHER'S LOOP ---
        void Update()
        {
            if ( _arCamera == null ) return;

            // 1. Check Conditions (Is the camera steady?)
            CalculateNativeStability();

            // 2. If Shaking -> Do Nothing (Save Battery)
            if ( !IsDeviceStable ) return;

            // 3. Evaluate "The Shot" for every visible target
            foreach ( var kvp in _activeTargets )
            {
                ScanTarget target = kvp.Value;

                // Ignore if already done or invalid
                if ( _isLocked.ContainsKey( target.Name ) && _isLocked[ target.Name ] ) continue;
                if ( target.Transform == null ) continue;

                // 4. Calculate Score (Is the angle good?)
                float score = GetNativeQuality( target.Transform );

                if ( !_bestScores.ContainsKey( target.Name ) ) _bestScores[ target.Name ] = 0f;

                // 5. If this is the best shot so far -> Capture it!
                if ( score > _bestScores[ target.Name ] )
                {
                    _bestScores[ target.Name ] = score;
                    ProcessCaptureGPU( target, score );
                }
            }
        }

        private void OnTargetAdded( ScanTarget target )
        {
            // Just add to the "Watch List". We don't capture yet.
            if ( !_activeTargets.ContainsKey( target.Name ) )
            {
                _activeTargets.Add( target.Name, target );
            }
        }

        private void CalculateNativeStability()
        {
            float3 curPos = _arCamera.transform.position;
            quaternion curRot = _arCamera.transform.rotation;
            float dt = Time.deltaTime;

            IsDeviceStable = CheckStability( curPos, curRot, _lastCamPos, _lastCamRot, dt, maxMoveSpeed, maxRotateSpeed );

            _lastCamPos = curPos;
            _lastCamRot = curRot;
        }

        private float GetNativeQuality( Transform targetTransform )
        {
            float3 camPos = _arCamera.transform.position;
            float3 camFwd = _arCamera.transform.forward;
            float3 imgPos = targetTransform.position;
            float3 imgUp = targetTransform.up;

            float3 sPos3 = _arCamera.WorldToScreenPoint( imgPos );
            // Handle "behind camera"
            float2 sPos = ( sPos3.z > 0 ) ? new float2( sPos3.x, sPos3.y ) : new float2( -1, -1 );

            return CalculateQuality( camPos, camFwd, imgPos, imgUp, sPos, Screen.width, Screen.height );
        }

        private unsafe void ProcessCaptureGPU( ScanTarget target, float score )
        {
            // A. NOW we ask the Bridge for the image data
            RenderTexture source = _arBridge.GetCameraFeedRT();
            if ( source == null ) return;

            // B. Calculate Math (Native)
            Vector2 size = target.Size;
            float hx = size.x * 0.5f; float hy = size.y * 0.5f;
            Transform t = target.Transform;

            _nativeScreenPoints[ 0 ] = ToScreen( t.TransformPoint( new Vector3( -hx, 0, -hy ) ) );
            _nativeScreenPoints[ 1 ] = ToScreen( t.TransformPoint( new Vector3( hx, 0, -hy ) ) );
            _nativeScreenPoints[ 2 ] = ToScreen( t.TransformPoint( new Vector3( hx, 0, hy ) ) );
            _nativeScreenPoints[ 3 ] = ToScreen( t.TransformPoint( new Vector3( -hx, 0, hy ) ) );

            CalculateHomography(
                size.x, size.y,
                Screen.width, Screen.height,
                _nativeScreenPoints.GetUnsafePtr(),
                _nativeResultMatrix.GetUnsafePtr()
            );

            float4x4 H = _nativeResultMatrix[ 0 ];
            if ( H.c3.w == 0 ) return;

            // C. Get Output Texture (Persistent on GPU)
            RenderTexture destRT;
            if ( !_capturedTextures.TryGetValue( target.Name, out destRT ) )
            {
                destRT = new RenderTexture( outputResolution, outputResolution, 0, RenderTextureFormat.RGB565 );
                destRT.Create();
                _capturedTextures[ target.Name ] = destRT;
            }

            // D. Blit (GPU Copy)
            unwarpMaterial.SetTexture( "_MainTex", source );
            unwarpMaterial.SetMatrix( "_Homography", H );
            unwarpMaterial.SetMatrix( "_DisplayMatrix", Matrix4x4.identity );

            Graphics.Blit( null, destRT, unwarpMaterial );

            // E. Notify Consumers (They will apply destRT to their materials)
            OnTextureCaptured?.Invoke( target.Name, destRT, score );

            // F. Lock if good enough
            if ( autoLock && score >= captureThreshold )
            {
                _isLocked[ target.Name ] = true;
                Debug.Log( $"[Felina] LOCKED {target.Name} (GPU)" );
            }
        }

        private float2 ToScreen( Vector3 worldPos )
        {
            Vector3 s = _arCamera.WorldToScreenPoint( worldPos );
            return new float2( s.x, s.y );
        }

        // Used by Late-Joining objects to get the texture immediately
        public RenderTexture GetCapturedTexture( string targetName )
        {
            return _capturedTextures.ContainsKey( targetName ) ? _capturedTextures[ targetName ] : null;
        }
    }
}