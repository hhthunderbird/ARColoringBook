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
        private List<TargetData> _targetData = new();

        private Dictionary<string, TargetData> _targetDataDictionary = new();

        private Dictionary<string, GameObject> _instantiated = new();
        private HashSet<TrackableId> _pendingAdds = new();

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
            if ( TryGetComponent<ARTrackedImageManager>( out var libraryManager ) )
            {
                libraryManager.trackedImagePrefab = null;

                for ( int i = 0; i < libraryManager.referenceLibrary.count; i++ )
                {
                    var imgRef = libraryManager.referenceLibrary[ i ];

                    var item = new TargetData
                    {
                        name = imgRef.name,
                        imageGuid = imgRef.guid.ToString()
                    };

                    if ( i >= _targetData.Count )
                        _targetData.Add( item );
                    else
                    {
                        item.prefab = _targetData[ i ].prefab;
                        item.blankMarker = _targetData[ i ].blankMarker;
                        item.materialIndex = _targetData[ i ].materialIndex;
                        _targetData[ i ] = item;
                    }
                }
            }
        }
        public void Reset() => OnValidate();

        private void Start()
        {
            ARScannerManager.Instance.OnTextureCaptured += UpdateModel;

            foreach ( var pair in _targetData )
            {
                _targetDataDictionary[ pair.imageGuid ] = pair;
            }
        }

        private void UpdateModel()
        {
            var target = _targetDataDictionary[ _lastObjectId ];

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
            GetComponent<ARTrackedImageManager>().trackablesChanged.AddListener( OnTrackablesChanged );
        }
        private void OnDisable()
        {
            GetComponent<ARTrackedImageManager>()?.trackablesChanged.RemoveListener( OnTrackablesChanged );
            if ( ARScannerManager.Instance != null )
            {
                ARScannerManager.Instance.OnTextureCaptured -= UpdateModel;
            }
        }

        private void OnTrackablesChanged( ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs )
        {
            // AR Foundation 6.3.1+ FIX: args.added no longer contains referenceImage metadata
            // We must wait for the first updated event to get the full data
            foreach ( var trackedImage in eventArgs.added )
            {
                _pendingAdds.Add( trackedImage.trackableId );
            }

            // Process updated images - spawn prefabs when we have metadata
            foreach ( var trackedImage in eventArgs.updated )
            {
                // Only spawn for newly tracked images
                if ( _pendingAdds.Contains( trackedImage.trackableId ) )
                {
                    SpawnPrefabForImage( trackedImage );
                    _pendingAdds.Remove( trackedImage.trackableId );
                }
                else
                {
                    _targetDataDictionary[ trackedImage.referenceImage.guid.ToString()].renderer.enabled =  trackedImage.trackingState == TrackingState.Tracking ;
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
                instance.transform.SetLocalPositionAndRotation( Vector3.zero, Quaternion.identity );
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
