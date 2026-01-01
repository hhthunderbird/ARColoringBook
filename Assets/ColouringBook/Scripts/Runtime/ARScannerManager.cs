using Cysharp.Threading.Tasks;
using Felina.ARColoringBook.Bridges;
using Felina.ARColoringBook.Events;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Felina.ARColoringBook
{
    [Serializable]
    public struct ReferencePair
    {
        public string referenceName; 
        public Texture2D originalTexture;
    }

    public class ARScannerManager : MonoBehaviour
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static unsafe extern void ComputeTransformMatrix( float a, float b, void* c, void* d );
#else        
        [DllImport( "Felina" )] private static unsafe extern void ComputeTransformMatrix( float a, float b, void* c, void* d );
#endif
        public static ARScannerManager Instance { get; private set; }
        private Camera _arCamera;
        private float3 _lastCamPos;
        private quaternion _lastCamRot;

        [Header( "Ground Truth Assets" )]
        [SerializeField]
        private List<ReferencePair> _referenceImages = new();
        private readonly Dictionary<string, Texture2D> _refLookup = new();

        [SerializeField] private Material _unwarpMaterial;

        private NativeArray<float2> _nativeScreenPoints;
        private NativeArray<float4x4> _nativeResultMatrix;

        // Cache material property IDs for better performance
        private static readonly int _refTexID = Shader.PropertyToID( "_RefTex" );
        private static readonly int _mainTexPropertyID = Shader.PropertyToID( "_MainTex" );
        private static readonly int _unwarpPropertyID = Shader.PropertyToID( "_Unwarp" );
        private static readonly int _displayMatrixPropertyID = Shader.PropertyToID( "_DisplayMatrix" );
        private Matrix4x4? _currentCameraMatrix;

        private ScanTarget _target;
        public event Action OnTextureCaptured;

        private CancellationTokenSource _cancellationToken;
        private ScanFeedbackEvent _feedbackEvent = new();

        private void Awake()
        {
            if ( Instance != null ) Destroy( Instance );
            Instance = this;
        }

        private void Start()
        {
            foreach ( var pair in _referenceImages )
            {
                if ( !string.IsNullOrEmpty( pair.referenceName ) )
                    _refLookup[ pair.referenceName ] = pair.originalTexture;
            }
            StartTask().Forget();
        }

        private async UniTaskVoid StartTask()
        {
            var ui = FindFirstObjectByType<UIController>();
            if ( ui )
                ui.OnCapture += ProcessRT;

            await UniTask.WaitUntil( () => ARFoundationBridge.Instance != null );

            ARFoundationBridge.Instance.OnTargetAdded += OnTargetAdded;
            ARFoundationBridge.Instance.OnDisplayMatrixUpdated -= OnDisplayMatrixUpdated;

            _arCamera = ARFoundationBridge.Instance.GetARCamera();

            if ( _arCamera != null )
            {
                _lastCamPos = _arCamera.transform.position;
                _lastCamRot = _arCamera.transform.rotation;
            }

            _nativeScreenPoints = new NativeArray<float2>( 4, Allocator.Persistent );
            _nativeResultMatrix = new NativeArray<float4x4>( 1, Allocator.Persistent );
        }

        void OnDestroy()
        {
            if ( _nativeScreenPoints.IsCreated ) _nativeScreenPoints.Dispose();
            if ( _nativeResultMatrix.IsCreated ) _nativeResultMatrix.Dispose();
            _cancellationToken?.Cancel();
            _cancellationToken?.Dispose();
        }

        void OnEnable() => Start();
        void OnDisable()
        {
            ARFoundationBridge.Instance.OnDisplayMatrixUpdated -= OnDisplayMatrixUpdated;
            ARFoundationBridge.Instance.OnTargetAdded -= OnTargetAdded;
        }

        private void OnDisplayMatrixUpdated( float4x4 m ) => _currentCameraMatrix = m;

        private unsafe void ProcessCaptureGPU()
        {
            ARFoundationBridge.Instance.UpdateCameraRT();

            if ( !_nativeScreenPoints.IsCreated || !_nativeResultMatrix.IsCreated ) return;

            var size = _target.Size;
            var hx = size.x * 0.5f;
            var hy = size.y * 0.5f;
            var t = _target.Transform;

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

            var resolution = Screen.currentResolution;

            ComputeTransformMatrix( resolution.width, resolution.height, _nativeScreenPoints.GetUnsafePtr(), _nativeResultMatrix.GetUnsafePtr() );

            var H = _nativeResultMatrix[ 0 ];

            var cameraSource = ARFoundationBridge.Instance.MasterCameraFeed;
            var tempRT = RenderTexture.GetTemporary( cameraSource.width, cameraSource.height, 0, cameraSource.format );

            if ( _target.Name == null ) return;

            _refLookup.TryGetValue( _target.Name, out var groundTruth );

            if ( groundTruth != null )
                _unwarpMaterial.SetTexture( _refTexID, groundTruth );
            else
                _unwarpMaterial.SetTexture( _refTexID, Texture2D.whiteTexture );

            _unwarpMaterial.SetTexture( _mainTexPropertyID, cameraSource );
            _unwarpMaterial.SetMatrix( _unwarpPropertyID, H );

            var matrixToUse = _currentCameraMatrix.GetValueOrDefault( Matrix4x4.identity );
            _unwarpMaterial.SetMatrix( _displayMatrixPropertyID, matrixToUse );

            Graphics.Blit( cameraSource, tempRT, _unwarpMaterial );

            Graphics.Blit( tempRT, cameraSource );

            RenderTexture.ReleaseTemporary( tempRT );

            OnTextureCaptured?.Invoke();
        }

        private float2 ToScreen( float3 worldPos )
        {
            var s = _arCamera.WorldToScreenPoint( worldPos );
            return new float2( s.x, s.y );
        }

        private void OnTargetAdded( ScanTarget incomingTarget )
        {
            _target = incomingTarget;
            _refLookup.TryGetValue( _target.Name, out var tx );
            EventManager.TriggerEvent( new ToggleUIEvent( true, tx ) );
            _cancellationToken = new CancellationTokenSource();
            UIFeedback( _cancellationToken.Token ).Forget();
        }

        private async UniTaskVoid UIFeedback( CancellationToken token )
        {
            //while ( !token.IsCancellationRequested )
            //{
            //    _feedbackEvent.Set( CalculateNativeStability(), GetNativeQuality() / Settings.Instance.CAPTURE_THRESHOLD );

            //    EventManager.TriggerEvent( _feedbackEvent );

            //    await UniTask.Delay( 100, cancellationToken: token );
            //}

            while ( !token.IsCancellationRequested )
            {
                // 1. Prepare Data
                _arCamera.transform.GetPositionAndRotation( out var camPos, out var camRot );
                var sPos3 = _arCamera.WorldToScreenPoint( _target.Transform.position );
                var sPos = ( sPos3.z > 0 ) ? new float2( sPos3.x, sPos3.y ) : new float2( -1, -1 );
                var settings = Settings.Instance;

                // 2. Create Containers for Results (Allocator.TempJob is fast/transient)
                NativeReference<bool> outStable = new NativeReference<bool>( Allocator.TempJob );
                NativeReference<float> outQuality = new NativeReference<float>( Allocator.TempJob );

                var maxMoveSpd = settings.MAX_MOVE_SPEED;
                var maxRotSpd = settings.MAX_ROTATE_SPEED;
                var minScanDist = settings.MIN_SCAN_DIST;
                var maxScanDist = settings.MAX_SCAN_DIST;
                var distPenalty = settings.DIST_PENALTY;
                var weightAngle = settings.WEIGHT_ANGLE;
                var weightCenter = settings.WEIGHT_CENTER;


                // 3. Create the Job
                var job = new ScannerJob
                {
                    // Dynamic Data
                    curPos = camPos,
                    curRot = camRot,
                    lastPos = _lastCamPos,
                    lastRot = _lastCamRot,
                    dt = Time.deltaTime,
                    camFwd = _arCamera.transform.forward,
                    imgPos = _target.Transform.position,
                    imgUp = _target.Transform.up,
                    imgScreenPos = sPos,
                    screenW = Screen.width,
                    screenH = Screen.height,

                    // Settings Data
                    maxMoveSpd = maxMoveSpd,
                    maxRotSpd = maxRotSpd,
                    minScanDist = minScanDist,
                    maxScanDist = maxScanDist,
                    distPenalty = distPenalty,
                    weightAngle = weightAngle,
                    weightCenter = weightCenter,

                    // Outputs
                    resultStability = outStable,
                    resultQuality = outQuality
                };

                // 4. Schedule & Complete
                // For a single item, immediate completion is fine and still gets Burst speedup.
                JobHandle handle = job.Schedule();
                handle.Complete(); // Force it to finish NOW so we can read results

                // 5. Read Results
                bool isStable = outStable.Value;
                float quality = outQuality.Value;

                // 6. Update State
                _lastCamPos = camPos;
                _lastCamRot = camRot;

                // 7. Clean up Memory
                outStable.Dispose();
                outQuality.Dispose();

                // 8. Fire Event
                _feedbackEvent.Set( isStable, quality / settings.CAPTURE_THRESHOLD );
                EventManager.TriggerEvent( _feedbackEvent );

                await UniTask.Delay( 100, cancellationToken: token );
            }
        }

        private async void ProcessRT()
        {
            _cancellationToken?.Cancel();
            await UniTask.WaitForEndOfFrame();
            ProcessCaptureGPU();
        }

        [BurstCompile]
        public struct ScannerJob : IJob
        {
            // Inputs
            [ReadOnly] public float3 curPos;
            [ReadOnly] public quaternion curRot;
            [ReadOnly] public float3 lastPos;
            [ReadOnly] public quaternion lastRot;
            [ReadOnly] public float dt;

            [ReadOnly] public float3 camFwd;
            [ReadOnly] public float3 imgPos;
            [ReadOnly] public float3 imgUp;
            [ReadOnly] public float2 imgScreenPos;
            [ReadOnly] public float screenW;
            [ReadOnly] public float screenH;

            // Settings
            [ReadOnly] public float maxMoveSpd;
            [ReadOnly] public float maxRotSpd;
            [ReadOnly] public float minScanDist;
            [ReadOnly] public float maxScanDist;
            [ReadOnly] public float distPenalty;
            [ReadOnly] public float weightAngle;
            [ReadOnly] public float weightCenter;

            // Outputs
            [WriteOnly] public NativeReference<bool> resultStability;
            [WriteOnly] public NativeReference<float> resultQuality;

            public void Execute()
            {
                float distSq = math.distancesq( curPos, lastPos );
                float _dt = dt <= 1e-5f ? 0.016f : dt;

                float maxDist = maxMoveSpd * _dt;
                bool isStable = true;

                if ( distSq > maxDist * maxDist )
                {
                    isStable = false;
                }
                else
                {
                    float dot = math.dot( curRot, lastRot );
                    float absDot = math.abs( dot );
                    float maxAngleDeg = maxRotSpd * _dt;
                    float maxAngleRad = math.radians( maxAngleDeg );
                    float minCos = math.cos( maxAngleRad * 0.5f );

                    if ( absDot < minCos ) isStable = false;
                }

                resultStability.Value = isStable;

                if ( isStable )
                {
                    // Angle Score
                    float3 negFwd = -camFwd;
                    float angleScore = math.saturate( math.dot( imgUp, negFwd ) );

                    // Center Score
                    float centerScore = 0.0f;
                    if ( imgScreenPos.x >= 0 && imgScreenPos.y >= 0 )
                    {
                        var screenCenter = new float2( screenW * 0.5f, screenH * 0.5f );
                        var sqrDistCenter = math.distancesq( imgScreenPos, screenCenter );
                        var halfH = screenH * 0.5f;
                        var sqrMaxDist = halfH * halfH;
                        centerScore = math.saturate( 1.0f - ( sqrDistCenter / sqrMaxDist ) );
                    }

                    // Distance Score
                    var sqrDistCam = math.distancesq( curPos, imgPos );
                    var distScore = 1.0f;
                    var minSq = minScanDist * minScanDist;
                    var maxSq = maxScanDist * maxScanDist;

                    if ( sqrDistCam < minSq || sqrDistCam > maxSq )
                    {
                        distScore = distPenalty;
                    }

                    resultQuality.Value = ( angleScore * weightAngle ) + ( centerScore * weightCenter * distScore );
                }
                else
                {
                    resultQuality.Value = 0.0f;
                }
            }
        }
    }
}