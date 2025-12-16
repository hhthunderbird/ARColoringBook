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

        // --- NEW: Track Pending Additions ---
        // We store IDs of images that were 'Added' but haven't been 'Updated' yet.
        private HashSet<TrackableId> _pendingAdds = new HashSet<TrackableId>();

        [Header( "Settings" )]
        private const int MAX_FEED_RES = 1920;
        [SerializeField] private int _outputResolution = 1024;

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

        // --- CORE LOGIC UPDATE ---
        private void OnTrackablesChanged( ARTrackablesChangedEventArgs<ARTrackedImage> args )
        {
            // 1. ADDED: Don't broadcast yet. Mark as pending.
            foreach ( var img in args.added )
            {
                // We just store the ID. We will process it when it gets its first update.
                _pendingAdds.Add( img.trackableId );
            }

            // 2. UPDATED: Check if any pending images have arrived here.
            foreach ( var img in args.updated )
            {
                // If this image is in our pending list, it means this is its FIRST update.
                if ( _pendingAdds.Contains( img.trackableId ) )
                {
                    // Now the data is stable (Transform, Size, etc.)
                    BroadcastTargetAdded( img );

                    // Remove from pending so we don't broadcast "Added" again on future updates
                    _pendingAdds.Remove( img.trackableId );
                }
            }

            // 3. REMOVED: Clean up to prevent memory leaks
            foreach ( var img in args.removed )
            {
                _pendingAdds.Remove( img.Key );
            }
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
