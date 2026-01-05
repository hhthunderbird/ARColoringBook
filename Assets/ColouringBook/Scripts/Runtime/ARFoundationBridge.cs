using Cysharp.Threading.Tasks;
using Felina.ARColoringBook.Base;
using Felina.ARColoringBook.Events;
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Felina.ARColoringBook.Bridges
{
    public class ARFoundationBridge : MonoBehaviour, IARBridge
    {
        public static ARFoundationBridge Instance;

        [Header( "AR Foundation Dependencies" )]
        [SerializeField]
        private ARTrackedImageManager _aRTrackedImageManager;
        public ARTrackedImageManager ARTrackedImageManager => _aRTrackedImageManager;

        [SerializeField] private ARCameraManager _cameraManager;
        [SerializeField] private ARCameraBackground _arCameraBackground;
        [SerializeField] private Camera _arCamera;

        public RenderTexture MasterCameraFeed { get; private set; }

        private HashSet<TrackableId> _pendingAdds = new HashSet<TrackableId>();

        public event Action<ScanTarget> OnTargetAdded;

        public event Action<float4x4> OnDisplayMatrixUpdated;

        private string _lastTrackingImage;

        private ToggleUIEvent _toggleUIEvent = new ToggleUIEvent( true );

        private void Awake()
        {
            if ( Instance != null ) Destroy( Instance );
            Instance = this;
        }

        private void Start() => InitializeSharedRT().Forget();

        private async UniTaskVoid InitializeSharedRT()
        {
            await UniTask.WaitUntil( () => Settings.Instance.IsInitialized );

            if ( MasterCameraFeed != null ) return;

            var settings = Settings.Instance.RENDERTEXTURE_SETTINGS;

            MasterCameraFeed = new RenderTexture( settings.Width, settings.Height, 0, settings.Format )
            {
                useMipMap = settings.UseMipMap,
                autoGenerateMips = settings.AutoGenerateMips,
                filterMode = settings.FilterMode,
                anisoLevel = 9
            };
            MasterCameraFeed.Create();
        }

        private void OnEnable()
        {
#if UNITY_2020_2_OR_NEWER
            if ( _aRTrackedImageManager ) _aRTrackedImageManager.trackablesChanged.AddListener( OnTrackablesChanged );
#else
            if ( _aRTrackedImageManager ) _aRTrackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
#endif
            if ( _cameraManager ) _cameraManager.frameReceived += OnFrameReceived;
        }


        private void OnDisable()
        {
#if UNITY_2020_2_OR_NEWER
            if ( _aRTrackedImageManager ) _aRTrackedImageManager.trackablesChanged.RemoveListener( OnTrackablesChanged );
#else
            if ( _aRTrackedImageManager ) _aRTrackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
#endif
            if ( _cameraManager ) _cameraManager.frameReceived -= OnFrameReceived;
            OnDestroy();
        }


        private void OnDestroy() => MasterCameraFeed?.Release();

        private void OnFrameReceived( ARCameraFrameEventArgs args )
        {
#if UNITY_2020_1_OR_NEWER
            OnDisplayMatrixUpdated?.Invoke( ( float4x4 ) args.displayMatrix.GetValueOrDefault());
#else
            OnDisplayMatrixUpdated?.Invoke( ( float4x4 ) args.projectionMatrix.GetValueOrDefault() );
#endif
        }


#if UNITY_2020_2_OR_NEWER
        private void OnTrackablesChanged( ARTrackablesChangedEventArgs<ARTrackedImage> args )
        {
#if AR_FOUNDATION_6_OR_NEWER
var addedList = new List<ARTrackedImage>(args.added);
            var updatedList = new List<ARTrackedImage>(args.updated);
            var removedList = new List<ARTrackedImage>();
            foreach (var kvp in args.removed)
            {
                removedList.Add(kvp.Value);
            }
            TrackableProcessing(addedList, updatedList, removedList);
#else
TrackableProcessing(args.added, args.updated, args.removed);
#endif
        }
#else
        private void OnTrackedImagesChanged( ARTrackedImagesChangedEventArgs args )
        {
            TrackableProcessing( args.added, args.updated, args.removed );
        }
#endif

        private void TrackableProcessing( List<ARTrackedImage> added, List<ARTrackedImage> updated, List<ARTrackedImage> removed )
        {
            foreach ( var img in added )
                _pendingAdds.Add( img.trackableId );

            foreach ( var img in updated )
            {
                if ( img.referenceImage.guid.ToString().Equals( _lastTrackingImage ) )
                {
                    switch ( img.trackingState )
                    {
                        case TrackingState.None:
                            _toggleUIEvent.State = false;
                            break;
                        case TrackingState.Limited:
                            _toggleUIEvent.State = false;
                            break;
                        case TrackingState.Tracking:
                            _toggleUIEvent.State = true;
                            break;
                    }
                    EventManager.TriggerEvent( _toggleUIEvent );
                }

                if ( _pendingAdds.Contains( img.trackableId ) )
                {
                    BroadcastTargetAdded( img );
                    _pendingAdds.Remove( img.trackableId );
                }
            }
        }

        private void BroadcastTargetAdded( ARTrackedImage img )
        {
            string targetName = img.referenceImage.name;

            if ( string.IsNullOrEmpty( targetName ) )
            {
                targetName = img.trackableId.ToString();

                if ( _aRTrackedImageManager != null && _aRTrackedImageManager.referenceLibrary != null )
                {
                    var guid = img.referenceImage.guid;
                    if ( guid != Guid.Empty )
                    {
                        for ( int i = 0; i < _aRTrackedImageManager.referenceLibrary.count; i++ )
                        {
                            var refImg = _aRTrackedImageManager.referenceLibrary[ i ];
                            if ( refImg.guid == guid )
                            {
                                targetName = refImg.name;
                                break;
                            }
                        }
                    }
                }
            }

            _lastTrackingImage = img.referenceImage.guid.ToString();
            var target = new ScanTarget
            {
                Name = targetName,
                Size = img.size,
                Transform = img.transform
            };


            OnTargetAdded?.Invoke( target );
            _toggleUIEvent.State = true;
            EventManager.TriggerEvent( _toggleUIEvent );
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
            MasterCameraFeed = targetRT;
        }

        public void UpdateCameraRT()
        {
            if ( MasterCameraFeed == null )
            {
                InitializeSharedRT();
            }

            if ( _arCameraBackground == null || _arCameraBackground.material == null ) return;

            if ( MasterCameraFeed != null )
                Graphics.Blit( null, MasterCameraFeed, _arCameraBackground.material );
        }

        public string GetImageName( Guid guid )
        {
            var library = _aRTrackedImageManager.referenceLibrary;
            if ( library == null ) return null;

            for ( int i = 0; i < library.count; i++ )
            {
                if ( library[ i ].guid == guid )
                    return library[ i ].name;
            }
            return null;
        }
    }
}
