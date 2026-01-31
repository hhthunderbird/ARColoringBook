using Felina.ARColoringBook.Base;
using Felina.ARColoringBook.DI;
using Felina.ARColoringBook.Events;
using Felina.ARColoringBook.Runtime;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Felina.ARColoringBook.Bridges
{
    public class ARFoundationBridge : MonoBehaviour, IARBridge
    {
        public event Action<ScanTarget> OnTargetAdded;

        [Header( "Dependencies" )]
        [Inject] private ARTrackedImageManager _aRTrackedImageManager;
        [Inject] private ARCameraManager _cameraManager;
        [Inject] private Camera _arCamera;

        private readonly HashSet<TrackableId> _pendingAdds = new HashSet<TrackableId>();
        private readonly ToggleUIEvent _toggleUIEvent = new ToggleUIEvent( true );
        private string _lastTrackingImage;

        private void Start() 
        {
#if UNITY_2020_2_OR_NEWER
            if ( _aRTrackedImageManager ) _aRTrackedImageManager.trackablesChanged.AddListener( OnTrackablesChanged );
#else
            if ( _aRTrackedImageManager ) _aRTrackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
#endif
        }

        private void OnDisable()
        {
#if UNITY_2020_2_OR_NEWER
            if ( _aRTrackedImageManager ) _aRTrackedImageManager.trackablesChanged.RemoveListener( OnTrackablesChanged );
#else
            if ( _aRTrackedImageManager ) _aRTrackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
#endif
        }

        

#if UNITY_2020_2_OR_NEWER
        private void OnTrackablesChanged( ARTrackablesChangedEventArgs<ARTrackedImage> args )
        {
#if AR_FOUNDATION_6_OR_NEWER
            var addedList = new List<ARTrackedImage>( args.added );
            var updatedList = new List<ARTrackedImage>( args.updated );
            var removedList = new List<ARTrackedImage>();
            foreach ( var kvp in args.removed )
            {
                removedList.Add( kvp.Value );
            }
            TrackableProcessing( addedList, updatedList, removedList );
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

        public XRCameraIntrinsics? GetCameraIntrinsics()
        {
            if ( _cameraManager == null )
                return null;
            if ( _cameraManager.TryGetIntrinsics( out XRCameraIntrinsics intrinsics ) )
            {
                return intrinsics;
            }
            return null;
        }

        public XRCpuImage? GetXRCpuImage()
        {
            if ( _cameraManager == null ) return null;

            if ( _cameraManager.TryAcquireLatestCpuImage( out XRCpuImage image ) )
                return image;

            return null;
        }
    }
}
