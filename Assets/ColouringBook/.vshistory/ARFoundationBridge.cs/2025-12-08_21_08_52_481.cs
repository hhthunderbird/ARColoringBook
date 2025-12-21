using UnityEngine;
using System;
using System.Collections.Generic;

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
        public ARCameraBackground arCameraBackground;
        public Camera arCamera;

        public event Action<ScanTarget> OnTargetAdded;

        private void Awake()
        {
            if ( !imageManager ) imageManager = FindObjectOfType<ARTrackedImageManager>();
            if ( !cameraManager ) cameraManager = FindObjectOfType<ARCameraManager>();
            if ( !arCameraBackground ) arCameraBackground = FindObjectOfType<ARCameraBackground>();
            if ( !arCamera ) arCamera = Camera.main;
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
            if ( arCameraBackground != null && arCameraBackground.material != null )
            {
                Graphics.Blit( null, destination, arCameraBackground.material );
            }
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