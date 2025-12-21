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
        private int _lastFrameUpdated = -1;
        private RenderTexture _cameraFeedRT;
        private const int MAX_FEED_RES = 1920;
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
            int w = Screen.width;
            int h = Screen.height;
            if ( w > MAX_FEED_RES || h > MAX_FEED_RES )
            {
                float aspect = ( float ) w / h;
                if ( w > h ) { w = MAX_FEED_RES; h = ( int ) ( w / aspect ); }
                else { h = MAX_FEED_RES; w = ( int ) ( h * aspect ); }
            }

            // Force Default to ensure compatibility
            var format = RenderTextureFormat.Default;

            _cameraFeedRT = new RenderTexture( w, h, 0, format )
            {
                useMipMap = false,
                autoGenerateMips = false,
                filterMode = FilterMode.Bilinear
            };
            _cameraFeedRT.Create();
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
            for ( int i = 0; i < args.added.Count; i++ )
            {
                var img = args.added[ i ];
                BroadcastTargetAdded( img );
            }
        }

        public Camera GetARCamera()
        {
            return arCamera;
        }

        public void FillCameraTexture( RenderTexture destination )
        {
            if ( _arCameraBackground != null && _arCameraBackground.material != null )
            {
                Graphics.Blit( null, destination, _arCameraBackground.material );
            }
        }

        //TODO: Optimize to avoid creating multiple RTs
        //TODO: Rename method to GetCameraFeedRT
        public RenderTexture GetCameraFeedRT()
        {
            if ( _arCameraBackground == null || _arCameraBackground.material == null ) return null;
            if ( Time.frameCount != _lastFrameUpdated )
            {
                Graphics.Blit( null, _cameraFeedRT, _arCameraBackground.material );
                _lastFrameUpdated = Time.frameCount;

            }
            return _cameraFeedRT;
        }

        private void BroadcastTargetAdded( ARTrackedImage added )
        {
            ScanTarget target = new ScanTarget
            {
                Name = added.referenceImage.name,
                Position = added.transform.position,
                Rotation = added.transform.rotation,
                Size = added.size,
                Transform = added.transform,
                IsTracking = added.trackingState == TrackingState.Tracking
            };

            OnTargetAdded?.Invoke( target );
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