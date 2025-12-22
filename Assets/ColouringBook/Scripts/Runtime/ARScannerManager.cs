using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Felina.ARColoringBook
{
    public class ARScannerManager : MonoBehaviour
    {
        public static ARScannerManager Instance { get; private set; }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern bool CheckStability( float3 f1, quaternion q1, float3 f2, quaternion q2, float f3, float f4, float f5 );
        [DllImport("__Internal")] private static extern float CalculateQuality( float3 f1, float3 f2, float3 f3, float3 f4, float2 f5, float f6, float f7 );
        [DllImport("__Internal")] private static unsafe extern void ComputeTransformMatrix( float f1, float f2, float f3, float f4, void* s, void* r );
#else
        [DllImport( "Felina" )] private static extern bool CheckStability( float3 f1, quaternion q1, float3 f2, quaternion q2, float f3, float f4, float f5 );
        [DllImport( "Felina" )] private static extern float CalculateQuality( float3 f1, float3 f2, float3 f3, float3 f4, float2 f5, float f6, float f7 );
        [DllImport( "Felina" )] private static unsafe extern void ComputeTransformMatrix( float f1, float f2, float f3, float f4, void* s, void* r );
#endif

        [Header( "Architecture" )]
        [SerializeField] private MonoBehaviour _arBridgeComponent;
        [SerializeField] private IARBridge _arBridge;

        [Header( "Scanner Settings" )]
        [SerializeField] private Material _unwarpMaterial;
        [SerializeField] private int _outputResolution = Internals.DEFAULT_OUTPUT_RESOLUTION;
        [Range( 0f, 1f ), SerializeField] private float _captureThreshold = Internals.DEFAULT_CAPTURE_THRESHOLD;
        [SerializeField] private bool _autoLock = true;
        [SerializeField] private float _maxMoveSpeed = Internals.DEFAULT_MAX_MOVE_SPEED;
        [SerializeField] private float _maxRotateSpeed = Internals.DEFAULT_MAX_ROTATE_SPEED;

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

        private void Awake()
        {
            Instance = this;
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
        }

        void OnDestroy()
        {
            if ( _nativeScreenPoints.IsCreated ) _nativeScreenPoints.Dispose();
            if ( _nativeResultMatrix.IsCreated ) _nativeResultMatrix.Dispose();
            foreach ( var rt in _capturedTextures.Values ) if ( rt != null ) rt.Release();
            _capturedTextures.Clear();
        }

        void OnEnable() { if ( _arBridge != null ) _arBridge.OnTargetAdded += OnTargetAdded; }
        void OnDisable() { if ( _arBridge != null ) _arBridge.OnTargetAdded -= OnTargetAdded; }

        void Update()
        {
            if ( _arCamera == null ) return;

            CalculateNativeStability();
            if ( !IsDeviceStable ) return;

            foreach ( var kvp in _activeTargets )
            {
                var target = kvp.Value;
                if ( _isLocked.ContainsKey( target.Name ) && _isLocked[ target.Name ] ) continue;
                if ( target.Transform == null ) continue;

                var score = GetNativeQuality( target.Transform );
                if ( !_bestScores.ContainsKey( target.Name ) ) _bestScores[ target.Name ] = 0f;

                if ( score > _bestScores[ target.Name ] )
                {
                    _bestScores[ target.Name ] = score;
                    ProcessCaptureGPU( target, score );
                }
            }
        }

        private unsafe void ProcessCaptureGPU( ScanTarget target, float score )
        {
            if ( !_nativeScreenPoints.IsCreated || !_nativeResultMatrix.IsCreated ) return;

            var source = _arBridge.GetCameraFeedRT();
            if ( source == null ) return;

            var size = target.Size;
            var hx = size.x * 0.5f; var hy = size.y * 0.5f;
            var t = target.Transform;

            _nativeScreenPoints[ 0 ] = ToScreen( t.TransformPoint( new Vector3( -hx, 0, -hy ) ) );
            _nativeScreenPoints[ 1 ] = ToScreen( t.TransformPoint( new Vector3( hx, 0, -hy ) ) );
            _nativeScreenPoints[ 2 ] = ToScreen( t.TransformPoint( new Vector3( hx, 0, hy ) ) );
            _nativeScreenPoints[ 3 ] = ToScreen( t.TransformPoint( new Vector3( -hx, 0, hy ) ) );

            ComputeTransformMatrix( size.x, size.y, Screen.width, Screen.height, _nativeScreenPoints.GetUnsafePtr(), _nativeResultMatrix.GetUnsafePtr() );

            var H = _nativeResultMatrix[ 0 ];

            if ( !_capturedTextures.TryGetValue( target.Name, out var destRT ) )
            {
                var chosenFormat = RenderTextureFormat.Default;

                if ( _arBridge != null ) chosenFormat = _arBridge.RenderTextureSettings.Format;

                if ( chosenFormat == RenderTextureFormat.Default )
                {
                    if ( SystemInfo.SupportsRenderTextureFormat( RenderTextureFormat.RGB565 ) )
                        chosenFormat = RenderTextureFormat.RGB565;
                    else if ( SystemInfo.SupportsRenderTextureFormat( RenderTextureFormat.ARGB32 ) )
                        chosenFormat = RenderTextureFormat.ARGB32;
                    else
                        chosenFormat = RenderTextureFormat.Default;
                }

                destRT = new RenderTexture( _outputResolution, _outputResolution, 0, chosenFormat )
                {
                    useMipMap = false,
                    autoGenerateMips = false
                };
                destRT.Create();
                _capturedTextures[ target.Name ] = destRT;
            }

            _unwarpMaterial.SetTexture( "_MainTex", source );
            _unwarpMaterial.SetMatrix( "_Homography", H );
            _unwarpMaterial.SetMatrix( "_DisplayMatrix", Matrix4x4.identity );
            Graphics.Blit( null, destRT, _unwarpMaterial );

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
            _arCamera.transform.GetPositionAndRotation( out var camPos, out var camRot );
            var camFwd = _arCamera.transform.forward;
            var imgPos = targetTransform.position;
            var imgUp = targetTransform.up;
            var sPos3 = _arCamera.WorldToScreenPoint( imgPos );
            var sPos = ( sPos3.z > 0 ) ? new float2( sPos3.x, sPos3.y ) : new float2( -1, -1 );
            return CalculateQuality( camPos, camFwd, imgPos, imgUp, sPos, Screen.width, Screen.height );
        }

        private float2 ToScreen( Vector3 worldPos )
        {
            var s = _arCamera.WorldToScreenPoint( worldPos );
            return new float2( s.x, s.y );
        }

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
    }
}
