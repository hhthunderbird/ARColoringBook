using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Felina.ARColoringBook.Editor
{
    /// <summary>
    /// Shared helper class for refreshing ARContentSpawner when XRReferenceImageLibrary changes
    /// </summary>
    public static class ARContentSpawnerRefreshHelper
    {
        public static void RefreshSpawnerWithSerializedObject( ARContentSpawner spawner, UnityEngine.XR.ARFoundation.ARTrackedImageManager imageManager, XRReferenceImageLibrary library )
        {
            if ( library == null ) return;

            SerializedObject so = new SerializedObject( spawner );
            SerializedProperty pairsProp = so.FindProperty( "_prefabPairs" );

            if ( pairsProp == null )
            {
                Debug.LogError( $"[Felina] Could not find _prefabPairs property on {spawner.gameObject.name}" );
                return;
            }

            // Store existing prefab assignments by GUID
            Dictionary<string, Object> existingPrefabs = new Dictionary<string, Object>();
            for ( int i = 0; i < pairsProp.arraySize; i++ )
            {
                var element = pairsProp.GetArrayElementAtIndex( i );
                var guidProp = element.FindPropertyRelative( "imageGuid" );
                var prefabProp = element.FindPropertyRelative( "prefab" );

                if ( guidProp != null && prefabProp != null && !string.IsNullOrEmpty( guidProp.stringValue ) )
                {
                    existingPrefabs[ guidProp.stringValue ] = prefabProp.objectReferenceValue;
                }
            }

            // Rebuild the list to match library
            pairsProp.ClearArray();

            for ( int i = 0; i < library.count; i++ )
            {
                var imgRef = library[ i ];

                pairsProp.InsertArrayElementAtIndex( i );
                var element = pairsProp.GetArrayElementAtIndex( i );

                var nameProp = element.FindPropertyRelative( "imageName" );
                var guidProp = element.FindPropertyRelative( "imageGuid" );
                var prefabProp = element.FindPropertyRelative( "prefab" );

                if ( nameProp != null ) nameProp.stringValue = imgRef.name;
                if ( guidProp != null ) guidProp.stringValue = imgRef.guid.ToString();

                // Restore existing prefab assignment if it exists
                if ( prefabProp != null && existingPrefabs.TryGetValue( imgRef.guid.ToString(), out var existingPrefab ) )
                {
                    prefabProp.objectReferenceValue = existingPrefab;
                }
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty( spawner );
        }
    }

    /// <summary>
    /// Monitors XRReferenceImageLibrary changes and auto-refreshes ARContentSpawner
    /// </summary>
    [InitializeOnLoad]
    public class XRReferenceImageLibraryChangeDetector
    {
        private static Dictionary<string, int> _libraryHashes = new Dictionary<string, int>();
        private static XRReferenceImageLibrary _lastSelectedLibrary;

        static XRReferenceImageLibraryChangeDetector()
        {
            Selection.selectionChanged += OnSelectionChanged;
            InitializeLibraryHashes();
        }

        static void InitializeLibraryHashes()
        {
            string[] guids = AssetDatabase.FindAssets( "t:XRReferenceImageLibrary" );
            foreach ( string guid in guids )
            {
                string path = AssetDatabase.GUIDToAssetPath( guid );
                var library = AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>( path );
                if ( library != null )
                {
                    _libraryHashes[ AssetDatabase.AssetPathToGUID( path ) ] = ComputeLibraryHash( library );
                }
            }
        }

        static void OnSelectionChanged()
        {
            var selectedLibrary = Selection.activeObject as XRReferenceImageLibrary;

            if ( selectedLibrary != null )
            {
                if ( _lastSelectedLibrary != selectedLibrary )
                {
                    _lastSelectedLibrary = selectedLibrary;
                    EditorApplication.update -= OnEditorUpdate;
                    EditorApplication.update += OnEditorUpdate;
                }
            }
            else
            {
                var selectedGameObject = Selection.activeGameObject;
                if ( selectedGameObject != null )
                {
                    var imageManager = selectedGameObject.GetComponent<UnityEngine.XR.ARFoundation.ARTrackedImageManager>();
                    if ( imageManager != null && imageManager.referenceLibrary != null )
                    {
                        var library = imageManager.referenceLibrary as XRReferenceImageLibrary;
                        if ( library != null && _lastSelectedLibrary != library )
                        {
                            _lastSelectedLibrary = library;
                            EditorApplication.update -= OnEditorUpdate;
                            EditorApplication.update += OnEditorUpdate;
                        }
                    }
                    else
                    {
                        UnsubscribeFromEditorUpdate();
                    }
                }
                else
                {
                    UnsubscribeFromEditorUpdate();
                }
            }
        }

        static void UnsubscribeFromEditorUpdate()
        {
            if ( _lastSelectedLibrary != null )
            {
                _lastSelectedLibrary = null;
                EditorApplication.update -= OnEditorUpdate;
            }
        }

        static void OnEditorUpdate()
        {
            if ( _lastSelectedLibrary == null ) return;

            string assetPath = AssetDatabase.GetAssetPath( _lastSelectedLibrary );
            if ( string.IsNullOrEmpty( assetPath ) ) return;

            string guid = AssetDatabase.AssetPathToGUID( assetPath );
            int currentHash = ComputeLibraryHash( _lastSelectedLibrary );

            if ( !_libraryHashes.ContainsKey( guid ) )
            {
                _libraryHashes[ guid ] = currentHash;
            }
            else if ( _libraryHashes[ guid ] != currentHash )
            {
                _libraryHashes[ guid ] = currentHash;
                RefreshARContentSpawnersForLibrary( _lastSelectedLibrary );
            }
        }

        static int ComputeLibraryHash( XRReferenceImageLibrary library )
        {
            int hash = library.count;
            for ( int i = 0; i < library.count; i++ )
            {
                var image = library[ i ];
                hash = hash * 31 + ( image.name?.GetHashCode() ?? 0 );
                hash = hash * 31 + image.guid.GetHashCode();
                if ( image.texture != null )
                {
                    hash = hash * 31 + image.texture.GetInstanceID();
                }
            }
            return hash;
        }

        static void RefreshARContentSpawnersForLibrary( XRReferenceImageLibrary library )
        {
            var spawners = Object.FindObjectsOfType<ARContentSpawner>();
            foreach ( var spawner in spawners )
            {
                var imageManager = spawner.GetComponent<UnityEngine.XR.ARFoundation.ARTrackedImageManager>();
                if ( imageManager != null && imageManager.referenceLibrary == library )
                {
                    ARContentSpawnerRefreshHelper.RefreshSpawnerWithSerializedObject( spawner, imageManager, library );
                    Debug.Log( $"[Felina] Refreshed ARContentSpawner on '{spawner.gameObject.name}' - detected library change ({library.count} images)" );
                }
            }
        }
    }
}
