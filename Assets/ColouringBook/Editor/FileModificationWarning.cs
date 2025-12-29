using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

// Shared helper class for refreshing ARContentSpawner
public static class ARContentSpawnerRefreshHelper
{
    public static void RefreshSpawnerWithSerializedObject( Felina.ARColoringBook.ARContentSpawner spawner, UnityEngine.XR.ARFoundation.ARTrackedImageManager imageManager, XRReferenceImageLibrary library )
    {
        if ( library == null ) return;

        SerializedObject so = new SerializedObject( spawner );
        SerializedProperty pairsProp = so.FindProperty( "_prefabImagePairs" );

        if ( pairsProp == null )
        {
            Debug.LogError( $"[Felina] Could not find _prefabImagePairs property on {spawner.gameObject.name}" );
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

            var nameProp = element.FindPropertyRelative( "name" );
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

public class FileModificationWarning : AssetModificationProcessor
{
    static bool IsOpenForEdit( string[] paths, List<string> outNotEditablePaths, StatusQueryOptions statusQueryOptions )
    {
        //foreach ( var path in paths )
        //    if()
        return true;
    }

    static string[] OnWillSaveAssets( string[] paths )
    {
        foreach ( var path in paths )
        {
            if ( path.EndsWith( ".asset" ) )
            {
                var asset = AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>( path );
                if ( asset != null )
                {
                    // Schedule refresh for next frame to ensure asset is fully saved
                    EditorApplication.delayCall += () => RefreshARContentSpawners( asset );
                }
            }
        }
        return paths;
    }

    static void RefreshARContentSpawners( XRReferenceImageLibrary library )
    {
        if ( library == null ) return;

        var spawners = Object.FindObjectsOfType<Felina.ARColoringBook.ARContentSpawner>();
        foreach ( var spawner in spawners )
        {
            var imageManager = spawner.GetComponent<UnityEngine.XR.ARFoundation.ARTrackedImageManager>();
            if ( imageManager != null && imageManager.referenceLibrary == library )
            {
                ARContentSpawnerRefreshHelper.RefreshSpawnerWithSerializedObject( spawner, imageManager, library );
                Debug.Log( $"[Felina] Refreshed ARContentSpawner on '{spawner.gameObject.name}' due to library save in '{AssetDatabase.GetAssetPath( library )}'" );
            }
        }
    }
}

public class XRReferenceImageLibraryPostprocessor : AssetPostprocessor
{
    static void OnPostprocessAllAssets( string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths )
    {
        foreach ( var path in importedAssets )
        {
            var asset = AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>( path );
            if ( asset != null )
            {
                EditorApplication.delayCall += () => 
                {
                    if ( asset != null )
                    {
                        RefreshARContentSpawnersForLibrary( asset );
                    }
                };
            }
        }
    }

    static void RefreshARContentSpawnersForLibrary( XRReferenceImageLibrary library )
    {
        var spawners = Object.FindObjectsOfType<Felina.ARColoringBook.ARContentSpawner>();
        foreach ( var spawner in spawners )
        {
            var imageManager = spawner.GetComponent<UnityEngine.XR.ARFoundation.ARTrackedImageManager>();
            if ( imageManager != null && imageManager.referenceLibrary == library )
            {
                ARContentSpawnerRefreshHelper.RefreshSpawnerWithSerializedObject( spawner, imageManager, library );
                Debug.Log( $"[Felina] Refreshed ARContentSpawner on '{spawner.gameObject.name}' after library import: '{AssetDatabase.GetAssetPath( library )}'" );
            }
        }
    }
}

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
        // Check if the selected object is an XRReferenceImageLibrary
        var selectedLibrary = Selection.activeObject as XRReferenceImageLibrary;
        
        if ( selectedLibrary != null )
        {
            // Library is selected - subscribe to editor updates to monitor it
            if ( _lastSelectedLibrary != selectedLibrary )
            {
                _lastSelectedLibrary = selectedLibrary;
                EditorApplication.update -= OnEditorUpdate;
                EditorApplication.update += OnEditorUpdate;
            }
        }
        else
        {
            // Check if selected GameObject has ARTrackedImageManager with a library
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
                    // Nothing relevant selected - unsubscribe
                    if ( _lastSelectedLibrary != null )
                    {
                        _lastSelectedLibrary = null;
                        EditorApplication.update -= OnEditorUpdate;
                    }
                }
            }
            else
            {
                // Nothing relevant selected - unsubscribe
                if ( _lastSelectedLibrary != null )
                {
                    _lastSelectedLibrary = null;
                    EditorApplication.update -= OnEditorUpdate;
                }
            }
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
            RefreshARContentSpawnersForLibraryChange( _lastSelectedLibrary );
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

    static void RefreshARContentSpawnersForLibraryChange( XRReferenceImageLibrary library )
    {
        var spawners = Object.FindObjectsOfType<Felina.ARColoringBook.ARContentSpawner>();
        foreach ( var spawner in spawners )
        {
            var imageManager = spawner.GetComponent<UnityEngine.XR.ARFoundation.ARTrackedImageManager>();
            if ( imageManager != null && imageManager.referenceLibrary == library )
            {
                ARContentSpawnerRefreshHelper.RefreshSpawnerWithSerializedObject( spawner, imageManager, library );
                Debug.Log( $"[Felina] Refreshed ARContentSpawner on '{spawner.gameObject.name}' - detected library change in '{AssetDatabase.GetAssetPath( library )}' ({library.count} images)" );
            }
        }
    }
}
