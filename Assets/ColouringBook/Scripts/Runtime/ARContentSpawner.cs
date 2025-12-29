using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Felina.ARColoringBook
{
    [RequireComponent( typeof( ARTrackedImageManager ) )]
    public class ARContentSpawner : MonoBehaviour
    {
        [SerializeField]
        private List<PrefabImagePair> _prefabImagePairs = new();

        [Serializable]
        private struct PrefabImagePair
        {
            public string name;
            public string imageGuid;  // Store as string for proper serialization
            public GameObject prefab;
        }


        private Dictionary<string, GameObject> _prefabDictionary = new();
        private Dictionary<string, GameObject> _instantiated = new();
        private HashSet<TrackableId> _pendingAdds = new();

        private void OnValidate()
        {
            Debug.Log( $"[Felina] ARContentSpawner: OnValidate called on '{gameObject.name}'" );
            if ( TryGetComponent<ARTrackedImageManager>( out var libraryManager ) )
            {
                libraryManager.trackedImagePrefab = null;

                for ( int i = 0; i < libraryManager.referenceLibrary.count; i++ )
                {
                    var imgRef = libraryManager.referenceLibrary[ i ];

                    var item = new PrefabImagePair
                    {
                        name = imgRef.name,
                        imageGuid = imgRef.guid.ToString()  // Convert GUID to string for serialization
                    };

                    if ( i >= _prefabImagePairs.Count )
                        _prefabImagePairs.Add( item );
                    else
                    {
                        item.prefab = _prefabImagePairs[ i ].prefab;
                        _prefabImagePairs[ i ] = item;
                    }
                }
            }
        }

        private void Start()
        {
            foreach ( var pair in _prefabImagePairs )
            {
                //this will overwrite duplicates, which is fine
                // Debug.Log( $"################## ref img 3 {pair.imageGuid} == {pair.prefab.name}" );
                _prefabDictionary[ pair.imageGuid ] = pair.prefab;
            }
        }

        public void Reset() => OnValidate();

        private void OnEnable()
        {
            GetComponent<ARTrackedImageManager>().trackablesChanged.AddListener( OnTrackablesChanged );
        }
        private void OnDisable()
        {
            GetComponent<ARTrackedImageManager>()?.trackablesChanged.RemoveListener( OnTrackablesChanged );
        }

        private void OnTrackablesChanged( ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs )
        {
            // AR Foundation 6.3.1+ FIX: args.added no longer contains referenceImage metadata
            // We must wait for the first updated event to get the full data

            // Track newly added images
            foreach ( var trackedImage in eventArgs.added )
            {
                _pendingAdds.Add( trackedImage.trackableId );
                // Debug.Log( $"[Felina] ARContentSpawner: Image added (pending metadata): trackableId={trackedImage.trackableId}" );
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
            }

            // Handle removed images
            foreach ( var pair in eventArgs.removed )
            {
                _pendingAdds.Remove( pair.Key );

                // Find and destroy the instantiated prefab
                var guid = pair.Value.referenceImage.guid;
                if ( _instantiated.TryGetValue( guid.ToString(), out var instance ) )
                {
                    Destroy( instance );
                    _instantiated.Remove( guid.ToString() );
                    // Debug.Log( $"[Felina] ARContentSpawner: Removed prefab for image '{pair.Value.referenceImage.name}'" );
                }
            }
        }


        private void SpawnPrefabForImage( ARTrackedImage trackedImage )
        {
            var guid = trackedImage.referenceImage.guid.ToString();
            // Debug.Log( $"################## ref img 4 {trackedImage.referenceImage.guid}" );

            if ( _instantiated.ContainsKey( guid ) ) return;

            // Find prefab for this image
            if ( _prefabDictionary.TryGetValue( guid, out var prefab ) && prefab != null )
            {
                var instance = Instantiate( prefab, trackedImage.transform );
                instance.transform.SetLocalPositionAndRotation( Vector3.zero, Quaternion.identity );
                instance.transform.localScale = Vector3.one;

                _instantiated[ guid ] = instance;

                // Verify ARPaintableObject exists
                var paintable = instance.GetComponentInChildren<ARPaintableObject>();
                if ( paintable != null )
                {
                    // Debug.Log( $"[Felina] ARContentSpawner: Spawned '{prefab.name}' for image '{trackedImage.referenceImage.name}' - ARPaintableObject found" );
                }
                else
                {
                    Debug.LogWarning( $"[Felina] ARContentSpawner: Spawned '{prefab.name}' for image '{trackedImage.referenceImage.name}' - ARPaintableObject NOT FOUND!" );
                }
            }
            else
            {
                Debug.LogWarning( $"[Felina] ARContentSpawner: No prefab assigned for image (GUID: {guid})" );
            }
        }
    }
}
