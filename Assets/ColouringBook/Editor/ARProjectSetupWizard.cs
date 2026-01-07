// 1. Wrap namespaces in the Define to prevent "Namespace not found" errors
#if AR_FOUNDATION_READY
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
#endif

using Felina.ARColoringBook.Bridges;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Felina.ARColoringBook
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

    // 2. If ARFoundation is missing, we remove the RequireComponent attribute
#if AR_FOUNDATION_READY
    [RequireComponent( typeof( ARTrackedImageManager ) )]
#endif
    public class ARContentSpawner : MonoBehaviour
    {
        // 3. We keep the variables that DON'T depend on ARFoundation visible
        // This ensures Serialized Data isn't lost if the package is temporarily removed.
        [SerializeField]
        private List<TargetData> _targetData = new List<TargetData>();

        // 4. Wrap the rest of the logic
#if AR_FOUNDATION_READY
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
            if ( UnityEditor.BuildPipeline.isBuildingPlayer || Application.isPlaying ) return;

            if ( TryGetComponent<ARTrackedImageManager>( out var libraryManager ) )
            {
                // Legacy support for older ARF versions
#if !UNITY_2020_2_OR_NEWER
                // In old versions, just ensure logic doesn't break
#else
                libraryManager.trackedImagePrefab = null;
#endif

                if ( libraryManager.referenceLibrary == null ) return;

                try { if ( libraryManager.referenceLibrary.count == 0 ) return; } catch { return; }

                var existingData = new Dictionary<string, TargetData>();
                foreach ( var data in _targetData )
                {
                    if ( !string.IsNullOrEmpty( data.imageGuid ) )
                        existingData[ data.imageGuid ] = data;
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
            }
#endif
        }

        public void Reset() => OnValidate();

        private void Start()
        {
            // Bridge/Manager check
            if ( ARScannerManager.Instance != null )
                ARScannerManager.Instance.OnTextureCaptured += UpdateModel;

            if ( _targetData.Count == 0 ) InitializeTargetData();

            foreach ( var pair in _targetData )
            {
                if ( !string.IsNullOrEmpty( pair.imageGuid ) )
                    _targetDataDictionary[ pair.imageGuid ] = pair;
            }
        }

        private void InitializeTargetData()
        {
            if ( TryGetComponent<ARTrackedImageManager>( out var libraryManager ) )
            {
                if ( libraryManager.referenceLibrary == null ) return;

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
        }

        private void UpdateModel()
        {
            if ( string.IsNullOrEmpty( _lastObjectId ) ) return;
            if ( !_targetDataDictionary.TryGetValue( _lastObjectId, out var target ) ) return;

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
                    if ( ARFoundationBridge.Instance != null )
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
            var manager = GetComponent<ARTrackedImageManager>();
            if ( manager != null )
            {
#if UNITY_2020_2_OR_NEWER
                manager.trackablesChanged.AddListener( OnTrackablesChanged );
#else
                manager.trackedImagesChanged += OnTrackedImagesChanged;
#endif
            }
        }

        private void OnDisable()
        {
            var manager = GetComponent<ARTrackedImageManager>();
            if ( manager != null )
            {
#if UNITY_2020_2_OR_NEWER
                manager.trackablesChanged.RemoveListener( OnTrackablesChanged );
#else
                manager.trackedImagesChanged -= OnTrackedImagesChanged;
#endif
            }

            if ( ARScannerManager.Instance != null )
                ARScannerManager.Instance.OnTextureCaptured -= UpdateModel;
        }

#if UNITY_2020_2_OR_NEWER
        private void OnTrackablesChanged( ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs )
        {
            var addedList = new List<ARTrackedImage>( eventArgs.added );
            var updatedList = new List<ARTrackedImage>( eventArgs.updated );
            var removedList = new List<ARTrackedImage>();
            foreach(var removed in eventArgs.removed) removedList.Add(removed);
            
            TrackableProcessing( addedList, updatedList, removedList );
        }
#else
        private void OnTrackedImagesChanged( ARTrackedImagesChangedEventArgs args )
        {
            TrackableProcessing( new List<ARTrackedImage>( args.added ), new List<ARTrackedImage>( args.updated ), new List<ARTrackedImage>( args.removed ) );
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
        }
#else
        // 5. FALLBACK: If ARFoundation is missing, this runs instead
        public void Reset() { Debug.Log("ARContentSpawner: Install AR Foundation to enable features."); }
        private void Start() {}
#endif
    }
}