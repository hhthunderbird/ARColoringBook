using UnityEngine;
using System;
using static UnityEngine.XR.ARSubsystems.XRCpuImage;


// 1. Wrap the NAMESPACES
#if FELINA_ARFOUNDATION
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
#endif

namespace Felina.ARColoringBook.Bridges
{
    // 2. We keep the class definition so the file is valid, 
    // but we remove the 'IARBridge' interface if dependencies are missing 
    // to avoid implementing empty methods.
#if FELINA_ARFOUNDATION
    public class ARFoundationBridge : MonoBehaviour, IARBridge
#else
    public class ARFoundationBridge : MonoBehaviour
#endif
    {
        // --------------------------------------------------------
        // ACTIVE VERSION (Compiled only when ARFoundation exists)
        // --------------------------------------------------------
#if FELINA_ARFOUNDATION

        [Header( "AR Foundation Dependencies" )]
        public ARTrackedImageManager imageManager;
        public ARCameraManager cameraManager;
        [SerializeField] private ARCameraBackground _arCameraBackground;
        public Camera arCamera;
        private int _lastFrameBlitted = -1;
        private RenderTexture _sharedCameraRT;

        [Header( "Settings" )]
        private const int MAX_FEED_RES = 1920;
        [SerializeField] private int _outputResolution = 1024;

        private RenderTextureSettings _renderTextureSettings;
        public RenderTextureSettings RenderTextureSettings => _renderTextureSettings;

        public event Action<ScanTarget> OnTargetAdded;

        private void Awake()
        {
            if ( !imageManager ) imageManager = FindAnyObjectByType<ARTrackedImageManager>();
            if ( !cameraManager ) cameraManager = FindAnyObjectByType<ARCameraManager>();
            if ( !_arCameraBackground ) _arCameraBackground = FindAnyObjectByType<ARCameraBackground>();
            if ( !arCamera ) arCamera = Camera.main;
        }

        private void Start()
        {
            Application.targetFrameRate = 60;

            int w = Screen.width;
            int h = Screen.height;
            if ( w > MAX_FEED_RES || h > MAX_FEED_RES )
            {
                float aspect = ( float ) w / h;
                if ( w > h ) { w = MAX_FEED_RES; h = ( int ) ( w / aspect ); }
                else { h = MAX_FEED_RES; w = ( int ) ( h * aspect ); }
            }

            var format = RenderTextureFormat.RGB565;
            if(!SystemInfo.SupportsRenderTextureFormat(format))
                format = RenderTextureFormat.Default;

            _renderTextureSettings = new RenderTextureSettings
            {
                Width = w,
                Height = h,
                UseMipMap = false,
                AutoGenerateMips = false,
                FilterMode = FilterMode.Bilinear,
                Format = format
            };

            _sharedCameraRT = new RenderTexture( w, h, 0, _renderTextureSettings.Format )
            {
                useMipMap = false,
                autoGenerateMips = false,
                filterMode = FilterMode.Bilinear
            };
            _sharedCameraRT.Create();
        }

        private void OnEnable()
        {
            if ( imageManager ) imageManager.trackablesChanged.AddListener( OnTrackablesChanged );
        }

        private void OnDisable()
        {
            if ( imageManager ) imageManager.trackablesChanged.RemoveListener( OnTrackablesChanged );
        }
        private void OnTrackablesChanged( ARTrackablesChangedEventArgs<ARTrackedImage> args )
        {
            var count = 0;

            foreach ( var img in args.added )
            {
                // Create the data packet
                var target = new ScanTarget
                {
                    Name = img.referenceImage.name,
                    Size = img.size,
                    Transform = img.transform, // Crucial: This reference stays alive!
                    IsTracking = true
                };
                OnTargetAdded?.Invoke( target );
            }
        }

        public Camera GetARCamera()
        {
            return arCamera;
        }


        //TODO: Optimize this method to avoid creating a new RT every frame
        public RenderTexture GetCameraFeedRT()
        {
            if ( _sharedCameraRT == null ) Start();

            if ( _arCameraBackground == null || _arCameraBackground.material == null ) return null;

            if ( Time.frameCount != _lastFrameBlitted )
            {
                Graphics.Blit( null, _sharedCameraRT, _arCameraBackground.material );
                _lastFrameBlitted = Time.frameCount;

            }
            return _sharedCameraRT;
        }


        // --------------------------------------------------------
        // DISABLED VERSION (Compiled when ARFoundation is missing)
        // --------------------------------------------------------
#else
        [Header( "Dependency Missing" )]
        [TextArea]
        public string message = "ARFoundation is not installed.\n\n" +
            "Please install 'AR Foundation' from the Package Manager to use this component.\n" +
            "Once installed, this script will automatically activate.";

        private void Start()
        {
            Debug.LogWarning( "[Felina] ARFoundationBridge is disabled because the ARFoundation package is missing." );
        }
#endif
    }
}