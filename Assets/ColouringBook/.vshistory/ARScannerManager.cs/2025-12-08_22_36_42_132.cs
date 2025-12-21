using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
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

        private Dictionary<string, ScanTarget> _activeTargets = new ();
        private Dictionary<string, RenderTexture> _capturedTextures = new ();

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
                    if ( IsDeviceStable )
                    {
                        CalculateQualityScore( ref target );

                        if ( target.Score > _highestQualityScore )
                        {
                            _highestQualityScore = target.Score;

                            var cameraFeedRT = _arBridge.GetCameraFeedRT();
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
            float3 imgPos = target.Transform.position;
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

        private unsafe void ProcessCaptureGPU( ScanTarget target, float score )
        {
            // A. Ask Bridge for the Image
            // We only ask for this data when we are ready to process it.
            RenderTexture source = _arBridge.GetCameraFeedRT();
            if ( source == null ) return;

            // B. Calculate Screen Points
            Vector2 size = target.Size;
            float hx = size.x * 0.5f; float hy = size.y * 0.5f;
            Transform t = target.Transform;

            _nativeScreenPoints[ 0 ] = ToScreen( t.TransformPoint( new Vector3( -hx, 0, -hy ) ) );
            _nativeScreenPoints[ 1 ] = ToScreen( t.TransformPoint( new Vector3( hx, 0, -hy ) ) );
            _nativeScreenPoints[ 2 ] = ToScreen( t.TransformPoint( new Vector3( hx, 0, hy ) ) );
            _nativeScreenPoints[ 3 ] = ToScreen( t.TransformPoint( new Vector3( -hx, 0, hy ) ) );

            // C. Native Homography Solve
            CalculateHomography(
                size.x, size.y,
                Screen.width, Screen.height,
                _nativeScreenPoints.GetUnsafePtr(),
                _nativeResultMatrix.GetUnsafePtr()
            );

            float4x4 H = _nativeResultMatrix[ 0 ];
            if ( H.c3.w == 0 ) return; // Solver failed

            // D. Get/Create Persistent Output Texture for this Target
            RenderTexture destRT;
            if ( !_capturedTextures.TryGetValue( target.Name, out destRT ) )
            {
                destRT = new RenderTexture( outputResolution, outputResolution, 0, RenderTextureFormat.RGB565 );
                destRT.Create();
                _capturedTextures[ target.Name ] = destRT;
            }

            // E. Blit directly on GPU (Fastest possible method)
            unwarpMaterial.SetTexture( "_MainTex", source );
            unwarpMaterial.SetMatrix( "_Homography", H );
            unwarpMaterial.SetMatrix( "_DisplayMatrix", Matrix4x4.identity );

            Graphics.Blit( null, destRT, unwarpMaterial );

            // F. Notify Listeners
            OnTextureCaptured?.Invoke( target.Name, destRT, score );

            // G. Lock Logic
            if ( autoLock && score >= captureThreshold )
            {
                _isLocked[ target.Name ] = true;
                Debug.Log( $"[Felina] LOCKED {target.Name} (Score: {score:F2})" );
            }
        }

        private float2 ToScreen( Vector3 worldPos )
        {
            Vector3 s = _arCamera.WorldToScreenPoint( worldPos );
            return new float2( s.x, s.y );
        }

        public RenderTexture GetCapturedTexture( string targetName )
        {
            return _capturedTextures.ContainsKey( targetName ) ? _capturedTextures[ targetName ] : null;
        }

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
