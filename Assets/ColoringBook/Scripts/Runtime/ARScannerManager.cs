using Cysharp.Threading.Tasks;
using Felina.ARColoringBook.Base;
using Felina.ARColoringBook.Bridges;
using Felina.ARColoringBook.Events;
using UnityEngine.XR.ARSubsystems;
using System;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Felina.ARColoringBook.DI;

namespace Felina.ARColoringBook.Runtime
{
    public class ARScannerManager : MonoBehaviour
    {
        public event Action<RenderTexture> OnTextureCaptured;

        [SerializeField] private Material _rotationMaterial;
        [SerializeField] private int _outputSize = 1024;
        [Header( "UI Feedback" )]
        [SerializeField] private int _feedbackIntervalMs = 100;

        [Header( "Dependencies" )]
        [Inject] private ARFoundationBridge _arFoundationBridge;

        [Header( "Shader Property Names" )]
        private readonly string _rotationTypeProperty = "_RotationType";
        private readonly string _rotationAngleProperty = "_RotationAngle";
        private readonly string _srcSizeProperty = "_SrcSize";
        private readonly string _dstSizeProperty = "_DstSize";

        private int _rotationTypeId;
        private int _rotationAngleId;
        private int _srcSizeId;
        private int _dstSizeId;

        private Camera _arCamera;
        private ScanTarget _target;
        private RenderTexture _dstRT;
        private Texture2D _cpuOutputTex;
        private Texture2D _srcTex;
        private CancellationTokenSource _cancellationToken;
        private readonly ScanFeedbackEvent _feedbackEvent = new ScanFeedbackEvent();
        private ScreenOrientation _currentScreenOrientation = ScreenOrientation.Portrait;
        private float3 _lastCamPos;
        private quaternion _lastCamRot;

        private void Awake()
        {
            _rotationTypeId = Shader.PropertyToID( _rotationTypeProperty );
            _rotationAngleId = Shader.PropertyToID( _rotationAngleProperty );
            _srcSizeId = Shader.PropertyToID( _srcSizeProperty );
            _dstSizeId = Shader.PropertyToID( _dstSizeProperty );
        }

        private void Start()
        {
            _currentScreenOrientation = Screen.orientation;

#if UNITY_2023_1_OR_NEWER
            var ui = FindFirstObjectByType<UIController>();
#else
            var ui = FindObjectOfType<UIController>();
#endif
            if ( ui )
                ui.OnCapture += ProcessCaptureCPU;

            _dstRT = CreateRT( _outputSize, _outputSize );

            _arFoundationBridge.OnTargetAdded += OnTargetAdded;

            EventManager.Subscribe<ToggleUIEvent>( OnToggleUIEvent );

            _arCamera = _arFoundationBridge.GetARCamera();
            if ( _arCamera != null )
            {
                _lastCamPos = _arCamera.transform.position;
                _lastCamRot = _arCamera.transform.rotation;
            }
        }
        private void Update() => _currentScreenOrientation = Screen.orientation;

        RenderTexture CreateRT( int width, int height )
        {
            var rt = new RenderTexture( width, height, 0, RenderTextureFormat.ARGB32 );
            rt.enableRandomWrite = true;
            rt.filterMode = FilterMode.Bilinear;
            rt.wrapMode = TextureWrapMode.Clamp;
            rt.Create();
            return rt;
        }

        private void OnToggleUIEvent( ToggleUIEvent args ) { if ( !args.State ) _cancellationToken?.Cancel(); }

        void OnDestroy()
        {
            _cancellationToken?.Cancel();
            _cancellationToken?.Dispose();
            Destroy( _srcTex );
            Destroy( _cpuOutputTex );
        }

        void OnEnable() => Start();
        void OnDisable()
        {
            _arFoundationBridge.OnTargetAdded -= OnTargetAdded;
        }

        private unsafe void ProcessCaptureCPU()
        {
            var img = _arFoundationBridge.GetXRCpuImage();

            int rotatedWidth = 0;
            int rotatedHeight = 0;
            float rotationAngleRad = 0;

            if (img == null || !img.HasValue || !img.Value.valid || _target.Transform == null ) return;

            using ( img.Value )
            {
                GetCpuImageTransform( img.Value, out rotatedWidth, out rotatedHeight, out rotationAngleRad );

                _srcTex = ConvertToTexture2DOrientationCorrect( img.Value, rotationAngleRad, rotatedWidth, rotatedHeight );
            }

            var worldCorners = GetWorldCorners();

            var imagePoints = WorldToXRCpuImageCoordinates( worldCorners, rotatedWidth, rotatedHeight );
            _cpuOutputTex = new Texture2D( _outputSize, _outputSize, TextureFormat.RGBAHalf, false );

            Solver.Solve(
                imagePoints,
                inlierThreshold: Settings.Instance.INLIER_THRESHOLD,
                maxIterations: Settings.Instance.MAX_ITERATIONS,
                refineWithLM: Settings.Instance.REFINE_WITH_LM,
                _outputSize,
                out float[] finalH,
                out float[] finalHinv
            );

            WarpPerspective( _srcTex, finalHinv );

            Graphics.Blit( _cpuOutputTex, _dstRT );

            OnTextureCaptured?.Invoke( _dstRT );
        }

        private void GetCpuImageTransform( XRCpuImage cpuImage, out int rotatedWidth, out int rotatedHeight, out float rotationAngleRad )
        {
            rotationAngleRad = 0f;
            rotatedWidth = cpuImage.width;
            rotatedHeight = cpuImage.height;

            switch ( _currentScreenOrientation )
            {
                case ScreenOrientation.Portrait:
                    rotationAngleRad = -90f * Mathf.Deg2Rad; // 90 CW
                    rotatedWidth = cpuImage.height;
                    rotatedHeight = cpuImage.width;
                    break;
                case ScreenOrientation.PortraitUpsideDown:
                    rotationAngleRad = 90f * Mathf.Deg2Rad; // 90 CCW
                    rotatedWidth = cpuImage.height;
                    rotatedHeight = cpuImage.width;
                    break;
                case ScreenOrientation.LandscapeRight:
                    rotationAngleRad = 180f * Mathf.Deg2Rad;
                    break;
            }
        }

        private float2[] WorldToXRCpuImageCoordinates( float3[] worldCorners, int textureWidth, int textureHeight )
        {
            float2[] imagePoints = new float2[ 4 ];

            for ( int i = 0; i < 4; i++ )
            {
                float3 viewportPoint = _arCamera.WorldToViewportPoint( worldCorners[ i ] );

                if ( viewportPoint.z < 0 )
                {
                    viewportPoint.x = 1 - viewportPoint.x;
                    viewportPoint.y = 1 - viewportPoint.y;
                    viewportPoint.z = -viewportPoint.z;
                }

                imagePoints[ i ] = ConvertViewportToXRCpuImageCoords(
                    new float2( viewportPoint.x, viewportPoint.y ),
                    textureWidth,
                    textureHeight );
            }

            return imagePoints;
        }

        private float2 ConvertViewportToXRCpuImageCoords( float2 viewportCoord, int textureWidth, int textureHeight )
        {
            // Aspect-correct mapping from viewport (0..1) to XRCpuImage pixels without rotating or flipping points.
            float screenW = Screen.width;
            float screenH = Screen.height;
            float screenAspect = screenW / screenH;
            float texAspect = ( float ) textureWidth / textureHeight;

            float x, y;
            if ( texAspect > screenAspect )
            {
                // Texture is wider: pillarbox in X
                float scale = textureHeight / screenH;
                float padX = ( textureWidth - screenW * scale ) * 0.5f;
                x = viewportCoord.x * screenW * scale + padX;
                y = viewportCoord.y * screenH * scale;
            }
            else
            {
                // Texture is taller: letterbox in Y
                float scale = textureWidth / screenW;
                float padY = ( textureHeight - screenH * scale ) * 0.5f;
                x = viewportCoord.x * screenW * scale;
                y = viewportCoord.y * screenH * scale + padY;
            }

            return new float2( x, y );
        }

        private Texture2D ConvertToTexture2DOrientationCorrect( XRCpuImage img, float rotationAngleRad, int rotatedWidth, int rotatedHeight )
        {
            var conv = new XRCpuImage.ConversionParams
            {
                inputRect = new RectInt( 0, 0, img.width, img.height ),
                outputDimensions = new Vector2Int( img.width, img.height ),
                outputFormat = TextureFormat.RGBA32,
                transformation = XRCpuImage.Transformation.MirrorY
            };

            int size = img.GetConvertedDataSize( conv );
            var buffer = new NativeArray<byte>( size, Allocator.Temp );

            img.Convert( conv, buffer );


            var tex = new Texture2D( conv.outputDimensions.x, conv.outputDimensions.y, conv.outputFormat, false );

            tex.LoadRawTextureData( buffer );
            tex.Apply();

            var dstRT = new RenderTexture( rotatedWidth, rotatedHeight, 0, RenderTextureFormat.ARGB32 );

            ApplyRotation( dstRT, tex, rotationAngleRad );

            var tmp = RenderTexture.active;
            RenderTexture.active = dstRT;
            var rotatedTex = new Texture2D( rotatedWidth, rotatedHeight, TextureFormat.RGBA32, false );
            rotatedTex.ReadPixels( new Rect( 0, 0, rotatedWidth, rotatedHeight ), 0, 0 );
            rotatedTex.Apply();
            RenderTexture.active = tmp;
            dstRT.Release();
            buffer.Dispose();
            Destroy( tex );
            return rotatedTex;
        }

        private void ApplyRotation( RenderTexture dstTexture, Texture2D srcTexture, float rotationAngleRad )
        {
            int srcWidth = srcTexture.width;
            int srcHeight = srcTexture.height;

            int dstWidth, dstHeight;
            if ( mathx.approximately( math.abs( rotationAngleRad ), math.PI * 0.5f ) )
            {
                dstWidth = srcHeight;
                dstHeight = srcWidth;
            }
            else
            {
                dstWidth = srcWidth;
                dstHeight = srcHeight;
            }

            int rotationType;
            float absAngle = math.abs( rotationAngleRad );
            if ( mathx.approximately( absAngle, math.PI * 0.5f ) )
            {
                rotationType = rotationAngleRad < 0f ? 1 : 3;
            }
            else if ( mathx.approximately( absAngle, math.PI ) )
            {
                rotationType = 2;
            }
            else
            {
                rotationType = 0;
            }

            // Set shader properties using configurable property names
            _rotationMaterial.SetInt( _rotationTypeId, rotationType );
            _rotationMaterial.SetFloat( _rotationAngleId, rotationAngleRad );
            _rotationMaterial.SetVector( _srcSizeId, new Vector4( srcWidth, srcHeight, 0, 0 ) );
            _rotationMaterial.SetVector( _dstSizeId, new Vector4( dstWidth, dstHeight, 0, 0 ) );

            srcTexture.filterMode = FilterMode.Point;
            _rotationMaterial.mainTexture = srcTexture;

            Graphics.Blit( srcTexture, dstTexture, _rotationMaterial );
        }

        //TODO: port to shader or to cpp
        public void WarpPerspective( Texture2D src, float[] Hinv )
        {
            if ( src.width == 0 || src.height == 0 || _cpuOutputTex.width == 0 || _cpuOutputTex.height == 0 )
                return;

            Color32[] srcPixels = src.GetPixels32();
            Color32[] dstPixels = new Color32[ _cpuOutputTex.width * _cpuOutputTex.height ];

            int dstW = _cpuOutputTex.width;
            int dstH = _cpuOutputTex.height;
            int srcW = src.width;
            int srcH = src.height;

            // Cache matrix elements for performance
            float h0 = Hinv[ 0 ], h1 = Hinv[ 1 ], h2 = Hinv[ 2 ];
            float h3 = Hinv[ 3 ], h4 = Hinv[ 4 ], h5 = Hinv[ 5 ];
            float h6 = Hinv[ 6 ], h7 = Hinv[ 7 ], h8 = Hinv[ 8 ];

            for ( int y = 0; y < dstH; y++ )
            {
                int yOffset = y * dstW;
                for ( int x = 0; x < dstW; x++ )
                {
                    // Apply inverse homography: Dst(x,y) -> Src(u,v)
                    float denom = h6 * x + h7 * y + h8;
                    if ( math.abs( denom ) < 1e-6f )
                        continue;

                    float u = ( h0 * x + h1 * y + h2 ) / denom;
                    float v = ( h3 * x + h4 * y + h5 ) / denom;

                    // Check bounds with small epsilon for numerical stability
                    if ( u < -0.5f || u > srcW - 0.501f || v < -0.5f || v > srcH - 0.501f )
                    {
                        dstPixels[ yOffset + x ] = new Color32( 0, 0, 0, 0 );
                        continue;
                    }

                    // Bilinear interpolation
                    int x0 = ( int ) math.floor( u );
                    int y0 = ( int ) math.floor( v );
                    int x1 = x0 + 1;
                    int y1 = y0 + 1;

                    // Clamp to texture boundaries
                    if ( x0 < 0 ) x0 = 0;
                    if ( x1 >= srcW ) x1 = srcW - 1;
                    if ( y0 < 0 ) y0 = 0;
                    if ( y1 >= srcH ) y1 = srcH - 1;

                    float wx = u - x0;
                    float wy = v - y0;

                    int idx00 = y0 * srcW + x0;
                    int idx10 = y0 * srcW + x1;
                    int idx01 = y1 * srcW + x0;
                    int idx11 = y1 * srcW + x1;

                    // Manual bilinear interpolation for performance
                    Color32 c00 = srcPixels[ idx00 ];
                    Color32 c10 = srcPixels[ idx10 ];
                    Color32 c01 = srcPixels[ idx01 ];
                    Color32 c11 = srcPixels[ idx11 ];

                    // Lerp Red channel
                    float r0 = c00.r + ( c10.r - c00.r ) * wx;
                    float r1 = c01.r + ( c11.r - c01.r ) * wx;
                    byte r = ( byte ) ( r0 + ( r1 - r0 ) * wy );

                    // Lerp Green channel
                    float g0 = c00.g + ( c10.g - c00.g ) * wx;
                    float g1 = c01.g + ( c11.g - c01.g ) * wx;
                    byte g = ( byte ) ( g0 + ( g1 - g0 ) * wy );

                    // Lerp Blue channel
                    float b0 = c00.b + ( c10.b - c00.b ) * wx;
                    float b1 = c01.b + ( c11.b - c01.b ) * wx;
                    byte b = ( byte ) ( b0 + ( b1 - b0 ) * wy );

                    // Lerp Alpha channel
                    float a0 = c00.a + ( c10.a - c00.a ) * wx;
                    float a1 = c01.a + ( c11.a - c01.a ) * wx;
                    byte a = ( byte ) ( a0 + ( a1 - a0 ) * wy );

                    dstPixels[ yOffset + x ] = new Color32( r, g, b, a );
                }
            }

            _cpuOutputTex.SetPixels32( dstPixels );
            _cpuOutputTex.Apply( false );
        }

        public float3[] GetWorldCorners()
        {
            float s = _target.Size.x * 0.5f;

            float3[] local =
            {
                new float3(-s, 0,  s),
                new float3( s, 0,  s),
                new float3( s, 0, -s),
                new float3(-s, 0, -s)
            };

            float3[] world = new float3[ 4 ];
            for ( int i = 0; i < 4; i++ )
            {
                world[ i ] = _target.Transform.TransformPoint( local[ i ] );
            }

            return world;
        }

        private void OnTargetAdded( ScanTarget incomingTarget )
        {
            _target = incomingTarget;
            _cancellationToken = new CancellationTokenSource();
            UIFeedback( _cancellationToken.Token ).Forget();
        }

        private async UniTaskVoid UIFeedback( CancellationToken token )
        {
            while ( !token.IsCancellationRequested )
            {
#if UNITY_2023_1_OR_NEWER
                _arCamera.transform.GetPositionAndRotation( out var camPos, out var camRot );
#else
                var camPos = ( float3 ) _arCamera.transform.position;
                var camRot = ( quaternion ) _arCamera.transform.rotation;
#endif
                var sPos3 = _arCamera.WorldToScreenPoint( _target.Transform.position );
                var sPos = ( sPos3.z > 0 ) ? new float2( sPos3.x, sPos3.y ) : new float2( -1, -1 );
                var settings = Settings.Instance;

#if UNITY_2021_2_OR_NEWER
                NativeReference<bool> outStable = new NativeReference<bool>( Allocator.TempJob );
                NativeReference<float> outQuality = new NativeReference<float>( Allocator.TempJob );
#else
                NativeArray<bool> outStable = new NativeArray<bool>( 1, Allocator.TempJob );
                NativeArray<float> outQuality = new NativeArray<float>( 1, Allocator.TempJob );
#endif

                var maxMoveSpd = settings.MAX_MOVE_SPEED;
                var maxRotSpd = settings.MAX_ROTATE_SPEED;
                var minScanDist = settings.MIN_SCAN_DIST;
                var maxScanDist = settings.MAX_SCAN_DIST;
                var distPenalty = settings.DIST_PENALTY;
                var weightAngle = settings.WEIGHT_ANGLE;
                var weightCenter = settings.WEIGHT_CENTER;

                var job = new ScannerJob
                {
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

                    maxMoveSpd = maxMoveSpd,
                    maxRotSpd = maxRotSpd,
                    minScanDist = minScanDist,
                    maxScanDist = maxScanDist,
                    distPenalty = distPenalty,
                    weightAngle = weightAngle,
                    weightCenter = weightCenter,

                    resultStability = outStable,
                    resultQuality = outQuality
                };

                var handle = job.Schedule();
                handle.Complete();

#if UNITY_2021_2_OR_NEWER
                bool isStable = outStable.Value;
                float quality = outQuality.Value;
#else
                bool isStable = outStable[ 0 ];
                float quality = outQuality[ 0 ];
#endif
                _lastCamPos = camPos;
                _lastCamRot = camRot;

                outStable.Dispose();
                outQuality.Dispose();

                _feedbackEvent.Set( isStable, quality / settings.CAPTURE_THRESHOLD );
                EventManager.TriggerEvent( _feedbackEvent );

                await UniTask.Delay( _feedbackIntervalMs, cancellationToken: token );
            }
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
#if UNITY_2021_2_OR_NEWER
            [WriteOnly] public NativeReference<bool> resultStability;
            [WriteOnly] public NativeReference<float> resultQuality;
#else
            [WriteOnly] public NativeArray<bool> resultStability;
            [WriteOnly] public NativeArray<float> resultQuality;
#endif


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

#if UNITY_2021_2_OR_NEWER
                resultStability.Value = isStable;
#else
                resultStability[ 0 ] = isStable;
#endif

                if ( isStable )
                {
                    float3 negFwd = -camFwd;
                    float angleScore = math.saturate( math.dot( imgUp, negFwd ) );

                    float centerScore = 0.0f;
                    if ( imgScreenPos.x >= 0 && imgScreenPos.y >= 0 )
                    {
                        var screenCenter = new float2( screenW * 0.5f, screenH * 0.5f );
                        var sqrDistCenter = math.distancesq( imgScreenPos, screenCenter );
                        var halfH = screenH * 0.5f;
                        var sqrMaxDist = halfH * halfH;
                        centerScore = math.saturate( 1.0f - ( sqrDistCenter / sqrMaxDist ) );
                    }

                    var sqrDistCam = math.distancesq( curPos, imgPos );
                    var distScore = 1.0f;
                    var minSq = minScanDist * minScanDist;
                    var maxSq = maxScanDist * maxScanDist;

                    if ( sqrDistCam < minSq || sqrDistCam > maxSq )
                        distScore = distPenalty;

                    var quality = ( angleScore * weightAngle ) + ( centerScore * weightCenter * distScore );

#if UNITY_2021_2_OR_NEWER
                    resultQuality.Value = quality;
#else
                    resultQuality[ 0 ] = quality;
#endif
                }
                else
                {
#if UNITY_2021_2_OR_NEWER
                    resultQuality.Value = 0.0f;
#else
                    resultQuality[ 0 ] = 0.0f;
#endif
                }
            }
        }
    }
    public static class mathx
    {
        public const float EPSILON = 1E-05f;

        public static bool approximately( float a, float b )
        {
            return math.abs( a - b ) < EPSILON;
        }
    }
}

