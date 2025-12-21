using UnityEngine;
using System;
using System.Collections.Generic; // Required for HashSet

using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Felina.ARColoringBook.Bridges
{
    // 2. We keep the class definition so the file is valid, 
    // but we remove the 'IARBridge' interface if dependencies are missing.
    /// <summary>
    /// Bridge implementation that exposes ARFoundation tracked image events and a shared camera feed as a RenderTexture.
    /// Implements the IARBridge interface so the runtime code can be library-agnostic.
    /// </summary>
    public class ARFoundationBridge : MonoBehaviour, IARBridge
    {
        [Header( "AR Foundation Dependencies" )]
        [SerializeField]
        private ARTrackedImageManager _aRTrackedImageManager;
        public ARTrackedImageManager ARTrackedImageManager => _aRTrackedImageManager;

        public ARCameraManager cameraManager;
        [SerializeField] private ARCameraBackground _arCameraBackground;
        public Camera arCamera;

        private int _lastFrameBlitted = -1;
        private RenderTexture _sharedCameraRT;

        private HashSet<TrackableId> _pendingAdds = new HashSet<TrackableId>();

        [Header( "Settings" )]
        private const int MAX_FEED_RES = Internals.MAX_FEED_RES;
        [SerializeField] private int _outputResolution = Internals.DEFAULT_OUTPUT_RESOLUTION;

        private RenderTextureSettings _renderTextureSettings;
        public RenderTextureSettings RenderTextureSettings => _renderTextureSettings;

        public event Action<ScanTarget> OnTargetAdded;

        private void Awake()
        {
            if ( !_aRTrackedImageManager ) _aRTrackedImageManager = FindAnyObjectByType<ARTrackedImageManager>();
            if ( !cameraManager ) cameraManager = FindAnyObjectByType<ARCameraManager>();
            if ( !_arCameraBackground ) _arCameraBackground = FindAnyObjectByType<ARCameraBackground>();
            if ( !arCamera ) arCamera = Camera.main;
        }

        private void Start()
        {
            Application.targetFrameRate = Internals.DEFAULT_TARGET_FRAME_RATE;

            var w = Screen.width;
            var h = Screen.height;
            if ( w > MAX_FEED_RES || h > MAX_FEED_RES )
            {
                var aspect = (float) w / h;
                if ( w > h ) { w = MAX_FEED_RES; h = (int) ( w / aspect ); }
                else { h = MAX_FEED_RES; w = (int) ( h * aspect ); }
            }

            var format = RenderTextureFormat.RGB565;
            if ( !SystemInfo.SupportsRenderTextureFormat( format ) )
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
            if ( _aRTrackedImageManager ) _aRTrackedImageManager.trackablesChanged.AddListener( OnTrackablesChanged );
        }

        private void OnDisable()
        {
            if ( _aRTrackedImageManager ) _aRTrackedImageManager.trackablesChanged.RemoveListener( OnTrackablesChanged );
            OnDestroy();
        }

        private void OnDestroy() => _sharedCameraRT?.Release();

        private void OnTrackablesChanged( ARTrackablesChangedEventArgs<ARTrackedImage> args )
        {
            foreach ( var img in args.added )
                _pendingAdds.Add( img.trackableId );
            
            foreach ( var img in args.updated )
            {
                if ( _pendingAdds.Contains( img.trackableId ) )
                {
                    BroadcastTargetAdded( img );
                    _pendingAdds.Remove( img.trackableId );
                }
            }

            foreach ( var img in args.removed )
                _pendingAdds.Remove( img.Key );            
        }

        private void BroadcastTargetAdded( ARTrackedImage img )
        {
            var target = new ScanTarget
            {
                Name = img.referenceImage.name,
                Size = img.size,
                Transform = img.transform,
                IsTracking = img.trackingState == TrackingState.Tracking,
            };

            OnTargetAdded?.Invoke( target );
        }

        public Camera GetARCamera()
        {
            return arCamera;
        }

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
    }
}
