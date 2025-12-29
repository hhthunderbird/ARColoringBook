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

        [SerializeField] private ARCameraManager _cameraManager;
        [SerializeField] private ARCameraBackground _arCameraBackground;
        [SerializeField] private Camera _arCamera;

        private int _lastFrameBlitted = -1;
        private RenderTexture _sharedCameraRT;
        private RenderTexture _externalTargetRT; // External RT provided by consumer

        private HashSet<TrackableId> _pendingAdds = new();

        [Header( "Settings" )]
        private const int MAX_FEED_RES = Internals.MAX_FEED_RES;

        [SerializeField] private RenderTextureSettings _renderTextureSettings;
        public RenderTextureSettings RenderTextureSettings => _renderTextureSettings;

        public event Action<ScanTarget> OnTargetAdded;

        private void Awake()
        {
            if ( !_aRTrackedImageManager ) _aRTrackedImageManager = FindAnyObjectByType<ARTrackedImageManager>();
            if ( !_cameraManager ) _cameraManager = FindAnyObjectByType<ARCameraManager>();
            if ( !_arCameraBackground ) _arCameraBackground = FindAnyObjectByType<ARCameraBackground>();
            if ( !_arCamera ) _arCamera = Camera.main;
        }

        private void Start()
        {
            Application.targetFrameRate = Internals.DEFAULT_TARGET_FRAME_RATE;

            InitializeSharedRT();
        }

        private void InitializeSharedRT()
        {
            if ( _sharedCameraRT != null ) return; // Already initialized
            
            var w = Screen.width;
            var h = Screen.height;
            if ( w > MAX_FEED_RES || h > MAX_FEED_RES )
            {
                var aspect = ( float ) w / h;
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

        private void OnTrackablesChanged( ARTrackablesChangedEventArgs<ARTrackedImage> args )
        {
            // AR Foundation 6.3.1+ FIX: args.added no longer contains referenceImage metadata
            // We must wait for the first updated event to get the full data
            
            // Track newly added images (without metadata yet)
            foreach ( var img in args.added )
            {
                _pendingAdds.Add( img.trackableId );
                // Debug.Log( $"[Felina] ARFoundationBridge: Image added (pending metadata): trackableId={img.trackableId}" );
            }
            
            // Process updated images - this is where we get the metadata in 6.3.1+
            foreach ( var img in args.updated )
            {
                // If this was a pending add, now we have the metadata
                if ( _pendingAdds.Contains( img.trackableId ) )
                {
                    // Debug.Log( $"[Felina] ARFoundationBridge: Metadata received for trackableId={img.trackableId}, name='{img.referenceImage.name}'" );
                    BroadcastTargetAdded( img );
                    _pendingAdds.Remove( img.trackableId );
                }
            }

            // Clean up removed images
            foreach ( var img in args.removed )
            {
                _pendingAdds.Remove( img.Key );
                // Debug.Log( $"[Felina] ARFoundationBridge: Image removed: trackableId={img.Key}" );
            }
        }

        private void BroadcastTargetAdded( ARTrackedImage img )
        {
            // FIX: AR Foundation sometimes provides empty referenceImage data
            // Try to get the name from referenceImage first, fallback to trackableId
            string targetName = img.referenceImage.name;
            
            if ( string.IsNullOrEmpty( targetName ) )
            {
                Debug.LogWarning( $"[Felina] ARFoundationBridge: TrackedImage referenceImage.name is empty! Using trackableId: {img.trackableId}" );
                targetName = img.trackableId.ToString();
                
                // Try to lookup the name from the reference library by GUID
                if ( _aRTrackedImageManager != null && _aRTrackedImageManager.referenceLibrary != null )
                {
                    var guid = img.referenceImage.guid;
                    if ( guid != System.Guid.Empty )
                    {
                        for ( int i = 0; i < _aRTrackedImageManager.referenceLibrary.count; i++ )
                        {
                            var refImg = _aRTrackedImageManager.referenceLibrary[ i ];
                            if ( refImg.guid == guid )
                            {
                                targetName = refImg.name;
                                // Debug.Log( $"[Felina] ARFoundationBridge: Found name '{targetName}' for GUID {guid}" );
                                break;
                            }
                        }
                    }
                }
            }
            
            var target = new ScanTarget
            {
                Name = targetName,
                Size = img.size,
                Transform = img.transform,
                IsTracking = img.trackingState == TrackingState.Tracking,
            };

            // Debug.Log( $"[Felina] ARFoundationBridge: Broadcasting target added: name='{target.Name}', trackingState={img.trackingState}" );
            OnTargetAdded?.Invoke( target );
        }

        public Camera GetARCamera()
        {
            return _arCamera;
        }

        public ARCameraBackground GetARCameraBackground()
        {
            return _arCameraBackground;
        }

        public void SetTargetRenderTexture( RenderTexture targetRT )
        {
            if ( targetRT != null && !targetRT.IsCreated() )
            {
                Debug.LogError( "[ARFoundationBridge] Target RT must be created before setting!" );
                return;
            }
            _externalTargetRT = targetRT;
        }

        public void GetCameraFeedRT()
        {
            if ( _sharedCameraRT == null ) 
            {
                InitializeSharedRT();
            }

            if ( _arCameraBackground == null || _arCameraBackground.material == null ) return;

            // If external RT is provided, blit directly to it and return it
            if ( _externalTargetRT != null )
            {
                if ( Time.frameCount != _lastFrameBlitted )
                {
                    Graphics.Blit( null, _externalTargetRT, _arCameraBackground.material );
                    _lastFrameBlitted = Time.frameCount;
                }
                return;
            }

            // Fallback: use internal shared RT
            if ( Time.frameCount != _lastFrameBlitted )
            {
                Graphics.Blit( null, _sharedCameraRT, _arCameraBackground.material );
                _lastFrameBlitted = Time.frameCount;
            }
        }
    }
}
