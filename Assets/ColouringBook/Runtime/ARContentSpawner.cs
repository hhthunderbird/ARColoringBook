using System;
using System.Collections.Generic;
using UnityEngine;

namespace Felina.ARColoringBook
{
    [Serializable]
    public class ARContentPair
    {
        [HideInInspector]
        public string ImageName; // Controlled by Custom Editor (match editor field name)
        public GameObject Prefab;
    }

    public class ARContentSpawner : MonoBehaviour
    {
        [Header( "Configuration" )]
        [Tooltip( "Assign your AR Bridge here." )]
        [SerializeField] private MonoBehaviour _arBridgeComponent;
        private IARBridge _arBridge;

        // We use a List so we can customize the drawer in the Editor
        [SerializeField, HideInInspector]
        private List<ARContentPair> _contentLibrary = new List<ARContentPair>();

        // Track what we have already spawned
        private HashSet<string> _spawnedTargets = new HashSet<string>();

        private void Awake()
        {
            if ( _arBridgeComponent is IARBridge bridge )
            {
                _arBridge = bridge;
            }
            else
            {
                Debug.LogError( "[Felina] ARContentSpawner: Assigned Bridge is invalid!" );
            }
        }

        private void OnEnable()
        {
            if ( _arBridge != null ) _arBridge.OnTargetAdded += OnTargetAdded;
        }

        private void OnDisable()
        {
            if ( _arBridge != null ) _arBridge.OnTargetAdded -= OnTargetAdded;
        }

        private void OnTargetAdded( ScanTarget target )
        {
            if ( _spawnedTargets.Contains( target.Name ) ) return;

            SpawnContent( target );
        }

        private void SpawnContent( ScanTarget target )
        {
            GameObject prefabToSpawn = null;

            // Linear search is fine for small libraries (usually < 20 images)
            foreach ( var pair in _contentLibrary )
            {
                if ( pair.ImageName == target.Name )
                {
                    prefabToSpawn = pair.Prefab;
                    break;
                }
            }

            if ( prefabToSpawn == null ) return;

            // Instantiate as child of target so it inherits position/rotation
            var instance = Instantiate( prefabToSpawn, target.Transform );
            Debug.Log( $"[Felina] Instantiated prefab '{prefabToSpawn.name}' for target '{target.Name}'" );
            // Reset local transform so it aligns with the tracked image
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            _spawnedTargets.Add( target.Name );

            Debug.Log( $"[Felina] Spawned '{prefabToSpawn.name}' for marker '{target.Name}'" );
        }
    }
}
