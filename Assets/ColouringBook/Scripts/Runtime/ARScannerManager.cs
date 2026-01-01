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
        public string referenceName; // Must match ReferenceLibrary name exactly
        public Texture2D originalTexture;
    }

    public class ARScannerManager : MonoBehaviour
    {
#if UNITY_IOS && !UNITY_EDITOR
[DllImport("__Internal")] private static extern bool CheckStability( float3 f1, quaternion q1, float3 f2, quaternion q2, float f3, float f4, float f5 );
        //[DllImport("__Internal")] private static extern float CalculateQuality( float3 f1, float3 f2, float3 f3, float3 f4, float2 f5, float f6, float f7 );
        //[DllImport("__Internal")] private static unsafe extern void ComputeTransformMatrix( float a, float b, void* c, void* d );
#else        
        //[DllImport( "Felina" )] private static extern bool CheckStability( float3 a, quaternion b, float3 c, quaternion d, float e, float f, float g );
        //[DllImport( "Felina" )] private static extern float CalculateQuality( float3 a, float3 b, float3 c, float3 d, float2 e, float f, float g );
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

        // --- INLINED JOB (No ScannerMath dependency) ---
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
                // --- 1. CHECK STABILITY ---
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

                // --- 2. CHECK QUALITY ---
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

        //private bool CalculateNativeStability()
        //{
        //    _arCamera.transform.GetPositionAndRotation( out var curPos, out var curRot );
        //    var dt = Time.deltaTime;

        //    // Store current before overwriting? 
        //    // NOTE: Your original code updated _lastCamPos AFTER the check. 
        //    // Ensure you pass the PREVIOUS frame's data, then update it.
        //    var isStable = ScannerMath.CheckStability(
        //        ( float3 ) curPos, curRot,
        //        _lastCamPos, _lastCamRot,
        //        dt,
        //        Settings.Instance.MAX_MOVE_SPEED,
        //        Settings.Instance.MAX_ROTATE_SPEED
        //    );

        //    _lastCamPos = ( float3 ) curPos;
        //    _lastCamRot = curRot;

        //    return isStable;
        //}

        //private float GetNativeQuality()
        //{
        //    _arCamera.transform.GetPositionAndRotation( out var camPosVec, out var camRot );
        //    var camPos = ( float3 ) camPosVec;
        //    var camFwd = ( float3 ) _arCamera.transform.forward;
        //    var imgPos = ( float3 ) _target.Transform.position;
        //    var imgUp = ( float3 ) _target.Transform.up;
        //    var sPos3 = _arCamera.WorldToScreenPoint( imgPos );
        //    var sPos = ( sPos3.z > 0 ) ? new float2( sPos3.x, sPos3.y ) : new float2( -1, -1 );

        //    var stg = Settings.Instance;
        //    // Use the C# version
        //    return ScannerMath.CalculateQuality(
        //        camPos, camFwd, imgPos, imgUp, sPos,
        //        Screen.width, Screen.height, stg.MIN_SCAN_DIST, stg.MAX_SCAN_DIST, stg.DIST_PENALTY, stg.WEIGHT_ANGLE, stg.WEIGHT_CENTER
        //    );
        //}

        ///// <summary>
        ///// Replaces Felina.cpp CheckStability
        ///// </summary>
        //[BurstCompile]
        //public bool CheckStability( float3 curPos, quaternion curRot, float3 lastPos, quaternion lastRot, float dt, float maxMoveSpeed, float maxRotSpeed )
        //{
        //    // --- 1. Position Check (Distance Squared) ---
        //    // Faster than Vector3.Distance because we avoid the Square Root
        //    float distSq = math.distancesq( curPos, lastPos );

        //    // Safety check for bad dt
        //    if ( dt <= 1e-5f ) dt = 0.016f;

        //    // Calculate max allowed distance squared once
        //    float maxDist = maxMoveSpeed * dt;
        //    float maxDistSq = maxDist * maxDist;

        //    // Fail early if position moved too much
        //    if ( distSq > maxDistSq ) return false;

        //    // --- 2. Rotation Check (Dot Product) ---
        //    // math.dot is the fastest possible way to compare rotations (4 muls, 3 adds).
        //    float dot = math.dot( curRot, lastRot );

        //    // "abs" handles the quaternion double-cover (q == -q)
        //    float absDot = math.abs( dot );

        //    // --- OPTIMIZATION: Compare Cosines instead of Angles ---
        //    // Instead of calculating the Angle (expensive 'acos'), 
        //    // we calculate the Cosine of the Allowed Angle (cheap).
        //    //
        //    // Logic: 
        //    // If Angle < Limit
        //    // Then Cos(Angle) > Cos(Limit)  (Because Cos decreases as Angle increases)
        //    // And since dot = cos(theta/2), we check: absDot > cos(Limit/2)

        //    float maxAngleDeg = maxRotSpeed * dt;
        //    float maxAngleRad = math.radians( maxAngleDeg );

        //    // This is the threshold. If alignment (dot) is stronger than this, we are stable.
        //    float minCos = math.cos( maxAngleRad * 0.5f );

        //    return absDot >= minCos;
        //}

        ///// <summary>
        ///// Replaces Felina.cpp CalculateQuality
        ///// </summary>
        //[BurstCompile]
        //public float CalculateQuality( float3 camPos, float3 camFwd, float3 imgPos, float3 imgUp, float2 imgScreenPos, float screenWidth, float screenHeight )
        //{
        //    var settings = Settings.Instance;

        //    // 1. Angle Score (Dot Product)
        //    // How much is the image facing the camera?
        //    var negFwd = -camFwd;
        //    var angleScore = math.saturate( math.dot( imgUp, negFwd ) );

        //    // 2. Center Score
        //    // Is the image in the center of the screen?
        //    if ( imgScreenPos.x < 0 || imgScreenPos.y < 0 ) return 0.0f; // Behind camera

        //    var screenCenter = new float2( screenWidth * 0.5f, screenHeight * 0.5f );
        //    var sqrDistCenter = math.distancesq( imgScreenPos, screenCenter );

        //    // Normalize by half-height squared (vertical radius)
        //    var halfH = screenHeight * 0.5f;
        //    var sqrMaxDist = halfH * halfH;

        //    var centerScore = math.saturate( 1.0f - ( sqrDistCenter / sqrMaxDist ) );

        //    // 3. Distance Score (The Penalty Logic)
        //    var sqrDistCam = math.distancesq( camPos, imgPos );
        //    var distScore = 1.0f;

        //    var minSq = settings.MIN_SCAN_DIST * settings.MIN_SCAN_DIST;
        //    var maxSq = settings.MAX_SCAN_DIST * settings.MAX_SCAN_DIST;

        //    // Apply penalty if out of range
        //    if ( sqrDistCam < minSq || sqrDistCam > maxSq )
        //    {
        //        distScore = settings.DIST_PENALTY;
        //    }

        //    // Final Weighted Score
        //    // Note: Now using the Settings values instead of hardcoded 0.6/0.4
        //    return ( angleScore * settings.WEIGHT_ANGLE ) + ( centerScore * settings.WEIGHT_CENTER * distScore );
        //}
        //[BurstCompile]
        //public struct ScannerJob : IJob
        //{
        //    // --- Inputs ---
        //    [ReadOnly] public float3 curPos;
        //    [ReadOnly] public quaternion curRot;
        //    [ReadOnly] public float3 lastPos;
        //    [ReadOnly] public quaternion lastRot;
        //    [ReadOnly] public float dt;

        //    [ReadOnly] public float3 camFwd;
        //    [ReadOnly] public float3 imgPos;
        //    [ReadOnly] public float3 imgUp;
        //    [ReadOnly] public float2 imgScreenPos;
        //    [ReadOnly] public float screenW;
        //    [ReadOnly] public float screenH;

        //    // --- Settings (Pass by Value) ---
        //    [ReadOnly] public float maxMoveSpd;
        //    [ReadOnly] public float maxRotSpd;
        //    [ReadOnly] public float minScanDist;
        //    [ReadOnly] public float maxScanDist;
        //    [ReadOnly] public float distPenalty;
        //    [ReadOnly] public float weightAngle;
        //    [ReadOnly] public float weightCenter;

        //    // --- Outputs (Must be NativeArray) ---
        //    // We use an array of length 1 to store the single result
        //    [WriteOnly] public NativeReference<bool> resultStability;
        //    [WriteOnly] public NativeReference<float> resultQuality;

        //    public void Execute()
        //    {
        //        // 1. Check Stability
        //        bool stable = ScannerMath.CheckStability(
        //            curPos, curRot, lastPos, lastRot, dt,
        //            maxMoveSpd, maxRotSpd
        //        );

        //        resultStability.Value = stable;

        //        // 2. Check Quality (Only if stable to save perf, or always if you prefer)
        //        if ( stable )
        //        {
        //            resultQuality.Value = ScannerMath.CalculateQuality(
        //                curPos, camFwd, imgPos, imgUp, imgScreenPos,
        //                screenW, screenH,
        //                minScanDist, maxScanDist, distPenalty,
        //                weightAngle, weightCenter
        //            );
        //        }
        //        else
        //        {
        //            resultQuality.Value = 0.0f;
        //        }
        //    }
        //}
    }


    ////[BurstCompile]
    //public static class ScannerMath
    //{
    //    /// <summary>
    //    /// Replaces Felina.cpp CheckStability
    //    /// </summary>
    //    //[BurstCompile]
    //    public static bool CheckStability( float3 curPos, quaternion curRot, float3 lastPos, quaternion lastRot, float dt, float maxMoveSpeed, float maxRotSpeed )
    //    {
    //        // --- 1. Position Check (Distance Squared) ---
    //        // Faster than Vector3.Distance because we avoid the Square Root
    //        float distSq = math.distancesq( curPos, lastPos );

    //        // Safety check for bad dt
    //        if ( dt <= 1e-5f ) dt = 0.016f;

    //        // Calculate max allowed distance squared once
    //        float maxDist = maxMoveSpeed * dt;
    //        float maxDistSq = maxDist * maxDist;

    //        // Fail early if position moved too much
    //        if ( distSq > maxDistSq ) return false;

    //        // --- 2. Rotation Check (Dot Product) ---
    //        // math.dot is the fastest possible way to compare rotations (4 muls, 3 adds).
    //        float dot = math.dot( curRot, lastRot );

    //        // "abs" handles the quaternion double-cover (q == -q)
    //        float absDot = math.abs( dot );

    //        // --- OPTIMIZATION: Compare Cosines instead of Angles ---
    //        // Instead of calculating the Angle (expensive 'acos'), 
    //        // we calculate the Cosine of the Allowed Angle (cheap).
    //        //
    //        // Logic: 
    //        // If Angle < Limit
    //        // Then Cos(Angle) > Cos(Limit)  (Because Cos decreases as Angle increases)
    //        // And since dot = cos(theta/2), we check: absDot > cos(Limit/2)

    //        float maxAngleDeg = maxRotSpeed * dt;
    //        float maxAngleRad = math.radians( maxAngleDeg );

    //        // This is the threshold. If alignment (dot) is stronger than this, we are stable.
    //        float minCos = math.cos( maxAngleRad * 0.5f );

    //        return absDot >= minCos;
    //    }

    //    /// <summary>
    //    /// Replaces Felina.cpp CalculateQuality
    //    /// </summary>
    //    //[BurstCompile]                          
    //    public static float CalculateQuality( float3 camPos, float3 camFwd, float3 imgPos, float3 imgUp, float2 imgScreenPos, float screenWidth, float screenHeight, float minScanDist, float maxScanDist, float distPenalty, float weightAngle, float weightCenter )
    //    {
    //        //var settings = Settings.Instance;

    //        // 1. Angle Score (Dot Product)
    //        // How much is the image facing the camera?
    //        var negFwd = -camFwd;
    //        var angleScore = math.saturate( math.dot( imgUp, negFwd ) );

    //        // 2. Center Score
    //        // Is the image in the center of the screen?
    //        if ( imgScreenPos.x < 0 || imgScreenPos.y < 0 ) return 0.0f; // Behind camera

    //        var screenCenter = new float2( screenWidth * 0.5f, screenHeight * 0.5f );
    //        var sqrDistCenter = math.distancesq( imgScreenPos, screenCenter );

    //        // Normalize by half-height squared (vertical radius)
    //        var halfH = screenHeight * 0.5f;
    //        var sqrMaxDist = halfH * halfH;

    //        var centerScore = math.saturate( 1.0f - ( sqrDistCenter / sqrMaxDist ) );

    //        // 3. Distance Score (The Penalty Logic)
    //        var sqrDistCam = math.distancesq( camPos, imgPos );
    //        var distScore = 1.0f;

    //        var minSq = minScanDist * minScanDist;
    //        var maxSq = maxScanDist * maxScanDist;

    //        // Apply penalty if out of range
    //        if ( sqrDistCam < minSq || sqrDistCam > maxSq )
    //        {
    //            distScore = distPenalty;
    //        }

    //        // Final Weighted Score
    //        // Note: Now using the Settings values instead of hardcoded 0.6/0.4
    //        return ( angleScore * weightAngle ) + ( centerScore * weightCenter * distScore );
    //    }
    //}
}