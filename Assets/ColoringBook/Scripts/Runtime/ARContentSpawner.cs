using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Felina.ARColoringBook.Runtime
{
    [Serializable]
    public struct TargetData
    {
        public string name;
        public string imageGuid;
        [HideInInspector]
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

        [Header( "Shader Property Names" )]
        [SerializeField] private string _baseMapProperty = "_BaseMap";
        [SerializeField] private string _mainTexProperty = "_MainTex";
        [SerializeField] private string _drawingTexProperty = "_DrawingTex";
        [SerializeField] private string _colorProperty = "_Color";
        [SerializeField] private string _baseColorProperty = "_BaseColor";
        [SerializeField] private string _tintColorProperty = "_TintColor";

        private readonly Dictionary<string, TargetData> _targetDataDictionary = new Dictionary<string, TargetData>();
        private readonly Dictionary<string, GameObject> _instantiated = new Dictionary<string, GameObject>();
        private readonly HashSet<TrackableId> _pendingAdds = new HashSet<TrackableId>();

        private MaterialPropertyBlock _propBlock;
        private int[] _candidates;
        private int _colorId;
        private int _baseColorId;
        private int _tintColorId;
        private string _lastObjectId;

        private void Awake()
        {
            _candidates = new int[]
            {
                Shader.PropertyToID(_baseMapProperty),
                Shader.PropertyToID(_mainTexProperty),
                Shader.PropertyToID(_drawingTexProperty)
            };
            _colorId = Shader.PropertyToID( _colorProperty );
            _baseColorId = Shader.PropertyToID( _baseColorProperty );
            _tintColorId = Shader.PropertyToID( _tintColorProperty );
            _propBlock = new MaterialPropertyBlock();
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            if ( UnityEditor.BuildPipeline.isBuildingPlayer || Application.isPlaying )
            {
                return;
            }

            if ( TryGetComponent<ARTrackedImageManager>( out var libraryManager ) )
            {
                libraryManager.trackedImagePrefab = null;

                if ( libraryManager.referenceLibrary == null )
                {
                    return;
                }

                try
                {
                    if ( libraryManager.referenceLibrary.count == 0 )
                    {
                        return;
                    }
                }
                catch
                {
                    return;
                }

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

                    if ( existingData.TryGetValue( item.imageGuid, out var existing ) )
                    {
                        item.prefab = existing.prefab;
                        item.blankMarker = existing.blankMarker;
                        item.materialIndex = existing.materialIndex;
                    }

                    _targetData.Add( item );
                }

                Debug.Log( $"ARContentSpawner: Updated with {_targetData.Count} reference images" );
            }
#endif
        }
        public void Reset() => OnValidate();

        private void Start()
        {
            ARScannerManager.Instance.OnTextureCaptured += UpdateModel;

            if ( _targetData.Count == 0 )
            {
                InitializeTargetData();
            }

            foreach ( var pair in _targetData )
            {
                _targetDataDictionary[ pair.imageGuid ] = pair;
            }

            Debug.Log( $"ARContentSpawner initialized with {_targetDataDictionary.Count} targets" );
        }

        private void InitializeTargetData()
        {
            if ( TryGetComponent<ARTrackedImageManager>( out var libraryManager ) )
            {
                if ( libraryManager.referenceLibrary == null )
                {
                    Debug.LogError( "ARContentSpawner: ARTrackedImageManager.referenceLibrary is NULL! Please assign an image library." );
                    return;
                }

                Debug.Log( $"ARContentSpawner: Initializing from reference library with {libraryManager.referenceLibrary.count} images" );

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
                Debug.LogError( "ARContentSpawner: ARTrackedImageManager component not found!" );
            }
        }

        private void UpdateModel( RenderTexture capturedTexture )
        {
            if ( string.IsNullOrEmpty( _lastObjectId ) )
            {
                Debug.LogWarning( "ARContentSpawner.UpdateModel: _lastObjectId is null or empty" );
                return;
            }

            if ( !_targetDataDictionary.TryGetValue( _lastObjectId, out var target ) )
            {
                Debug.LogWarning( $"ARContentSpawner.UpdateModel: Image GUID {_lastObjectId} not found in dictionary" );
                return;
            }

            var renderer = target.renderer;

            if ( renderer == null )
            {
                Debug.LogWarning( "ARContentSpawner.UpdateModel: Renderer is null!" );
                return;
            }

            var sharedMats = renderer.sharedMaterials;

            var _materialIndex = target.materialIndex;

            if ( _materialIndex < 0 || _materialIndex >= sharedMats.Length )
            {
                Debug.LogWarning( $"ARContentSpawner.UpdateModel: Material index {_materialIndex} out of range (0-{sharedMats.Length - 1})" );
                return;
            }

            var sharedMat = sharedMats[ _materialIndex ];
            if ( sharedMat == null )
            {
                Debug.LogWarning( "ARContentSpawner.UpdateModel: SharedMaterial is null!" );
                return;
            }

            renderer.GetPropertyBlock( _propBlock, _materialIndex );

            bool textureSet = false;
            foreach ( var propId in _candidates )
            {
                if ( sharedMat.HasProperty( propId ) )
                {
                    _propBlock.SetTexture( propId, capturedTexture );
                    textureSet = true;
                    break;
                }
            }

            if ( !textureSet )
            {
                Debug.LogError( $"[ARContentSpawner] ? Material '{sharedMat.name}' has NONE of the candidate properties: _BaseMap, _MainTex, _DrawingTex!" );
                for ( int i = 0; i < sharedMat.shader.GetPropertyCount(); i++ )
                {
                    if ( sharedMat.shader.GetPropertyType( i ) == UnityEngine.Rendering.ShaderPropertyType.Texture )
                    {
                        Debug.Log( $"  - {sharedMat.shader.GetPropertyName( i )}" );
                    }
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
#if AR_FOUNDATION_6_OR_NEWER
var addedList = new List<ARTrackedImage>(eventArgs.added);
            var updatedList = new List<ARTrackedImage>(eventArgs.updated);
            var removedList = new List<ARTrackedImage>();
            foreach (var kvp in eventArgs.removed)
            {
                removedList.Add(kvp.Value);
            }
            TrackableProcessing(addedList, updatedList, removedList);
#else
TrackableProcessing(eventArgs.added, eventArgs.updated, eventArgs.removed);
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
            foreach ( var trackedImage in added )
            {
                _pendingAdds.Add( trackedImage.trackableId );
            }

            foreach ( var trackedImage in updated )
            {
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
                Debug.LogWarning( $"ARContentSpawner: No prefab assigned for image (GUID: {_lastObjectId})" );
            }
        }
    }
}
