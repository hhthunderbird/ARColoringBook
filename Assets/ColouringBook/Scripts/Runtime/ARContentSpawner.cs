using Felina.ARColoringBook.Bridges;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Felina.ARColoringBook
{
    [Serializable]
    public struct TargetData
    {
        public string name;
        public string imageGuid;
        public Renderer renderer;
        public GameObject prefab;
        public Texture2D blankMarker;
        public int materialIndex;
    }


    [RequireComponent( typeof( ARTrackedImageManager ) )]
    public class ARContentSpawner : MonoBehaviour
    {
        [SerializeField]
        private List<TargetData> _targetData = new List<TargetData>();

        private Dictionary<string, TargetData> _targetDataDictionary = new Dictionary<string, TargetData>();

        private Dictionary<string, GameObject> _instantiated = new Dictionary<string, GameObject>();
        private HashSet<TrackableId> _pendingAdds = new HashSet<TrackableId>();

        private MaterialPropertyBlock _propBlock;
        private readonly int _colorId = Shader.PropertyToID( "_Color" );
        private readonly int _baseColorId = Shader.PropertyToID( "_BaseColor" );
        private readonly int _tintColorId = Shader.PropertyToID( "_TintColor" );
        private int[] _candidates;

        private string _lastObjectId;

        private void Awake()
        {
            _candidates = new int[]
            {
                Shader.PropertyToID( "_BaseMap" ),
                Shader.PropertyToID( "_MainTex" ),
                Shader.PropertyToID( "_DrawingTex" )
        };
            _propBlock = new MaterialPropertyBlock();
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            // Don't run during build process or play mode
            if ( UnityEditor.BuildPipeline.isBuildingPlayer || Application.isPlaying )
            {
                return;
            }

            if ( TryGetComponent<ARTrackedImageManager>( out var libraryManager ) )
            {
                libraryManager.trackedImagePrefab = null;

                // Check if reference library exists
                if ( libraryManager.referenceLibrary == null )
                {
                    return; // Silently return during asset import/build
                }

                // Additional safety check for count access
                try
                {
                    if ( libraryManager.referenceLibrary.count == 0 )
                    {
                        return; // Silently return if empty
                    }
                }
                catch
                {
                    // Library not ready yet, skip
                    return;
                }

                // Store existing data to preserve user assignments
                var existingData = new Dictionary<string, TargetData>();
                foreach ( var data in _targetData )
                {
                    if ( !string.IsNullOrEmpty( data.imageGuid ) )
                    {
                        existingData[ data.imageGuid ] = data;
                    }
                }

                _targetData.Clear();

                for ( int i = 0; i < libraryManager.referenceLibrary.count; i++ )
                {
                    var imgRef = libraryManager.referenceLibrary[ i ];

                    var item = new TargetData
                    {
                        name = imgRef.name,
                        imageGuid = imgRef.guid.ToString()
                    };

                    // Restore existing assignments if available
                    if ( existingData.TryGetValue( item.imageGuid, out var existing ) )
                    {
                        item.prefab = existing.prefab;
                        item.blankMarker = existing.blankMarker;
                        item.materialIndex = existing.materialIndex;
                    }

                    _targetData.Add( item );
                }

                Debug.Log( $"[Felina] ARContentSpawner: Updated with {_targetData.Count} reference images" );
            }
#endif
        }
        public void Reset() => OnValidate();

        private void Start()
        {
            ARScannerManager.Instance.OnTextureCaptured += UpdateModel;

            // Ensure _targetData is populated from reference library at runtime
            if ( _targetData.Count == 0 )
            {
                InitializeTargetData();
            }

            foreach ( var pair in _targetData )
            {
                _targetDataDictionary[ pair.imageGuid ] = pair;
            }

            // Debug log to verify initialization
            Debug.Log( $"[Felina] ARContentSpawner initialized with {_targetDataDictionary.Count} targets" );
        }

        private void InitializeTargetData()
        {
            if ( TryGetComponent<ARTrackedImageManager>( out var libraryManager ) )
            {
                if ( libraryManager.referenceLibrary == null )
                {
                    Debug.LogError( "[Felina] ARContentSpawner: ARTrackedImageManager.referenceLibrary is NULL! Please assign an image library." );
                    return;
                }

                Debug.Log( $"[Felina] ARContentSpawner: Initializing from reference library with {libraryManager.referenceLibrary.count} images" );

                _targetData.Clear();
                for ( int i = 0; i < libraryManager.referenceLibrary.count; i++ )
                {
                    var imgRef = libraryManager.referenceLibrary[ i ];

                    var item = new TargetData
                    {
                        name = imgRef.name,
                        imageGuid = imgRef.guid.ToString()
                    };

                    _targetData.Add( item );
                }
            }
            else
            {
                Debug.LogError( "[Felina] ARContentSpawner: ARTrackedImageManager component not found!" );
            }
        }

        private void UpdateModel()
        {
            if ( string.IsNullOrEmpty( _lastObjectId ) )
            {
                Debug.LogWarning( "[Felina] ARContentSpawner.UpdateModel: _lastObjectId is null or empty" );
                return;
            }

            if ( !_targetDataDictionary.TryGetValue( _lastObjectId, out var target ) )
            {
                Debug.LogWarning( $"[Felina] ARContentSpawner.UpdateModel: Image GUID {_lastObjectId} not found in dictionary" );
                return;
            }

            var renderer = target.renderer;

            if ( renderer == null ) return;

            var sharedMats = renderer.sharedMaterials;

            var _materialIndex = target.materialIndex;

            if ( _materialIndex < 0 || _materialIndex >= sharedMats.Length ) return;

            var sharedMat = sharedMats[ _materialIndex ];
            if ( sharedMat == null ) return;

            renderer.GetPropertyBlock( _propBlock, _materialIndex );

            foreach ( var propId in _candidates )
            {
                if ( sharedMat.HasProperty( propId ) )
                {
                    _propBlock.SetTexture( propId, ARFoundationBridge.Instance.MasterCameraFeed );
                    break;
                }
            }

            _propBlock.SetColor( _colorId, Color.white );
            _propBlock.SetColor( _baseColorId, Color.white );
            _propBlock.SetColor( _tintColorId, Color.white );
            renderer.SetPropertyBlock( _propBlock, _materialIndex );
            _propBlock.Clear();
        }

        private void OnEnable()
        {
#if UNITY_2020_2_OR_NEWER
            GetComponent<ARTrackedImageManager>().trackablesChanged.AddListener( OnTrackablesChanged );
#else
            GetComponent<ARTrackedImageManager>().trackedImagesChanged += OnTrackedImagesChanged;
#endif
        }
        private void OnDisable()
        {
#if UNITY_2020_2_OR_NEWER
            GetComponent<ARTrackedImageManager>()?.trackablesChanged.RemoveListener( OnTrackablesChanged );
#else
            GetComponent<ARTrackedImageManager>().trackedImagesChanged -= OnTrackedImagesChanged;
#endif

            if ( ARScannerManager.Instance != null )
            {
                ARScannerManager.Instance.OnTextureCaptured -= UpdateModel;
            }
        }

#if UNITY_2020_2_OR_NEWER
        private void OnTrackablesChanged( ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs )
        {
            TrackableProcessing(eventArgs.added, eventArgs.updated, eventArgs.removed);
        }
#else
        private void OnTrackedImagesChanged( ARTrackedImagesChangedEventArgs args )
        {
            TrackableProcessing( args.added, args.updated, args.removed );
        }
#endif

        private void TrackableProcessing( List<ARTrackedImage> added, List<ARTrackedImage> updated, List<ARTrackedImage> removed )
        {
            // AR Foundation 6.3.1+ FIX: args.added no longer contains referenceImage metadata
            // We must wait for the first updated event to get the full data
            foreach ( var trackedImage in added )
            {
                _pendingAdds.Add( trackedImage.trackableId );
            }

            // Process updated images - spawn prefabs when we have metadata
            foreach ( var trackedImage in updated )
            {
                // Only spawn for newly tracked images
                if ( _pendingAdds.Contains( trackedImage.trackableId ) )
                {
                    SpawnPrefabForImage( trackedImage );
                    _pendingAdds.Remove( trackedImage.trackableId );
                }
                else
                {
                    string guid = trackedImage.referenceImage.guid.ToString();
                    if ( _targetDataDictionary.TryGetValue( guid, out var target ) && target.renderer != null )
                    {
                        target.renderer.enabled = trackedImage.trackingState == TrackingState.Tracking;
                    }
                }
            }
        }



        private void SpawnPrefabForImage( ARTrackedImage trackedImage )
        {
            _lastObjectId = trackedImage.referenceImage.guid.ToString();

            if ( _instantiated.ContainsKey( _lastObjectId ) ) return;

            // Find prefab for this image
            if ( _targetDataDictionary.TryGetValue( _lastObjectId, out var target ) && target.prefab != null )
            {
                var instance = Instantiate( target.prefab, trackedImage.transform );
#if UNITY_2021_2_OR_NEWER
                instance.transform.SetLocalPositionAndRotation( Vector3.zero, Quaternion.identity );
#else
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
#endif
                instance.transform.localScale = Vector3.one;

                _instantiated[ _lastObjectId ] = instance;
                var currentTarget = _targetDataDictionary[ _lastObjectId ];
                currentTarget.renderer = instance.GetComponentInChildren<Renderer>( true );
                _targetDataDictionary[ _lastObjectId ] = currentTarget;
            }
            else
            {
                Debug.LogWarning( $"[Felina] ARContentSpawner: No prefab assigned for image (GUID: {_lastObjectId})" );
            }
        }
    }
}
