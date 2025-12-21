using System;
using System.Collections.Generic;
using UnityEngine;

namespace Felina.ARColoringBook
{
    [Serializable]
    public class ARContentPair
    {
        [HideInInspector]
        public string ImageName; 
        public GameObject Prefab;
    }

    public class ARContentSpawner : MonoBehaviour
    {
        [Header( "Configuration" )]
        [Tooltip( "Assign your AR Bridge here." )]
        [SerializeField] private MonoBehaviour _arBridgeComponent;
        private IARBridge _arBridge;

        [SerializeField, HideInInspector]
        private List<ARContentPair> _contentLibrary = new();
        private HashSet<string> _spawnedTargets = new();

        private void Awake()
        {
            if ( _arBridgeComponent is IARBridge bridge )
            {
                _arBridge = bridge;
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
            GameObject prefabToSpawn = null; // Initialize prefab to null

            foreach ( var pair in _contentLibrary )
            {
                if ( pair.ImageName == target.Name )
                {
                    prefabToSpawn = pair.Prefab;
                    break;
                }
            }

            if ( prefabToSpawn == null ) return;

            var instance = Instantiate( prefabToSpawn, target.Transform );
            instance.transform.SetLocalPositionAndRotation( Vector3.zero, Quaternion.identity );
            instance.transform.localScale = Vector3.one;

            _spawnedTargets.Add( target.Name );
        }
    }
}
