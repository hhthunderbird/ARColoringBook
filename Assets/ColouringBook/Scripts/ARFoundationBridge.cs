using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
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
        public static ARFoundationBridge Instance;

        [Header( "AR Foundation Dependencies" )]
        [SerializeField]
        private ARTrackedImageManager _aRTrackedImageManager;
        public ARTrackedImageManager ARTrackedImageManager => _aRTrackedImageManager;

        [SerializeField] private ARCameraManager _cameraManager;
        [SerializeField] private ARCameraBackground _arCameraBackground;
        [SerializeField] private Camera _arCamera;

        public RenderTexture MasterCameraFeed { get; private set; }

        private HashSet<TrackableId> _pendingAdds = new();

        public event Action<ScanTarget> OnTargetAdded;

        public event Action<float4x4> OnDisplayMatrixUpdated;

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
            if ( _aRTrackedImageManager ) _aRTrackedImageManager.trackablesChanged.AddListener( OnTrackablesChanged );
            if ( _cameraManager ) _cameraManager.frameReceived += OnFrameReceived;
        }


        private void OnDisable()
        {
            if ( _aRTrackedImageManager ) _aRTrackedImageManager.trackablesChanged.RemoveListener( OnTrackablesChanged );
            if ( _cameraManager ) _cameraManager.frameReceived -= OnFrameReceived;
            OnDestroy();
        }

        private void OnDestroy() => MasterCameraFeed?.Release();

        private void OnFrameReceived( ARCameraFrameEventArgs args )
        {
            OnDisplayMatrixUpdated?.Invoke( ( float4x4 ) args.displayMatrix );
        }


        private void OnTrackablesChanged( ARTrackablesChangedEventArgs<ARTrackedImage> args )
        {
            // AR Foundation 6.3.1+ FIX: args.added no longer contains referenceImage metadata
            // We must wait for the first updated event to get the full data

            // Track newly added images (without metadata yet)
            foreach ( var img in args.added )
                _pendingAdds.Add( img.trackableId );

            // Process updated images - this is where we get the metadata in 6.3.1+
            foreach ( var img in args.updated )
            {
                // If this was a pending add, now we have the metadata
                if ( _pendingAdds.Contains( img.trackableId ) )
                {
                    BroadcastTargetAdded( img );
                    _pendingAdds.Remove( img.trackableId );
                }
            }
        }

        private void BroadcastTargetAdded( ARTrackedImage img )
        {
            // FIX: AR Foundation sometimes provides empty referenceImage data
            // Try to get the name from referenceImage first, fallback to trackableId
            string targetName = img.referenceImage.name;

            if ( string.IsNullOrEmpty( targetName ) )
            {
                targetName = img.trackableId.ToString();

                // Try to lookup the name from the reference library by GUID
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

            var target = new ScanTarget
            {
                Name = targetName,
                Size = img.size,
                Transform = img.transform
            };

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
