//using UnityEditor;

//namespace Felina.ARColoringBook.Editor
//{
//    [CustomEditor( typeof( ARContentSpawner ) )]
//    public class ARContentSpawnerEditor : UnityEditor.Editor
//    {
//        private ARContentSpawner _spawner;
//        //private SerializedProperty _prefabPairsProp;
//        //private ARTrackedImageManager _trackedImageManager;
//        //private List<XRReferenceImage> _referenceImages = new();

//        void OnEnable()
//        {
//            _spawner = target as ARContentSpawner;
//            //_prefabPairsProp = serializedObject.FindProperty( "_prefabPairs" );
//            //_trackedImageManager = _spawner.GetComponent<ARTrackedImageManager>();

//            EditorApplication.hierarchyChanged += OnHierarchyChanged;
//        }

//        private void OnHierarchyChanged()
//        {
//            _spawner?.Reset();
//        }

//        public override void OnInspectorGUI()
//        {
//            //draw default inspector
//            DrawDefaultInspector();

//            //serializedObject.Update();

//            //// Show info about ARTrackedImageManager
//            //if ( _trackedImageManager == null )
//            //{
//            //    EditorGUILayout.HelpBox( "ARContentSpawner requires ARTrackedImageManager on the same GameObject!", MessageType.Error );
//            //    if ( GUILayout.Button( "Add ARTrackedImageManager" ) )
//            //    {
//            //        _spawner.gameObject.AddComponent<ARTrackedImageManager>();
//            //    }
//            //    serializedObject.ApplyModifiedProperties();
//            //    return;
//            //}

//            //// Get reference library
//            //var library = _trackedImageManager.referenceLibrary as XRReferenceImageLibrary;
//            //if ( library == null )
//            //{
//            //    EditorGUILayout.HelpBox( "ARTrackedImageManager has no Reference Image Library assigned!", MessageType.Warning );
//            //    serializedObject.ApplyModifiedProperties();
//            //    return;
//            //}

//            //// Check if library changed
//            //if ( HasLibraryChanged( library ) )
//            //{
//            //    SyncPrefabPairsWithLibrary( library );
//            //}

//            //// Draw prefab assignments
//            //EditorGUILayout.Space();
//            //EditorGUILayout.LabelField( $"Image Prefab Assignments ({library.count})", EditorStyles.boldLabel );
//            //EditorGUILayout.Space();

//            //for ( int i = 0; i < library.count; i++ )
//            //{
//            //    var image = library[ i ];
//            //    var prefab = _spawner.GetPrefabForImage( image );

//            //    EditorGUILayout.BeginHorizontal( EditorStyles.helpBox );

//            //    EditorGUILayout.LabelField( image.name, GUILayout.Width( 150 ) );

//            //    var newPrefab = ( GameObject ) EditorGUILayout.ObjectField( prefab, typeof( GameObject ), false );

//            //    if ( newPrefab != prefab )
//            //    {
//            //        Undo.RecordObject( target, "Change Prefab Assignment" );
//            //        _spawner.SetPrefabForImage( image, newPrefab );
//            //        EditorUtility.SetDirty( target );
//            //    }

//            //    EditorGUILayout.EndHorizontal();
//            //}

//            //serializedObject.ApplyModifiedProperties();
//        }

//        //private bool HasLibraryChanged( XRReferenceImageLibrary library )
//        //{
//        //    if ( library == null )
//        //        return _referenceImages.Count > 0;

//        //    if ( _referenceImages.Count != library.count )
//        //        return true;

//        //    for ( int i = 0; i < library.count; i++ )
//        //    {
//        //        if ( i >= _referenceImages.Count || _referenceImages[ i ].guid != library[ i ].guid )
//        //            return true;
//        //    }

//        //    return false;
//        //}

//        //private void SyncPrefabPairsWithLibrary( XRReferenceImageLibrary library )
//        //{
//        //    // Update cached reference images
//        //    _referenceImages.Clear();
//        //    foreach ( var image in library )
//        //    {
//        //        _referenceImages.Add( image );
//        //    }

//        //    // Sync prefab pairs
//        //    var existingPairs = new Dictionary<string, GameObject>();
//        //    for ( int i = 0; i < _prefabPairsProp.arraySize; i++ )
//        //    {
//        //        var element = _prefabPairsProp.GetArrayElementAtIndex( i );
//        //        var guidProp = element.FindPropertyRelative( "imageGuid" );
//        //        var prefabProp = element.FindPropertyRelative( "prefab" );

//        //        if ( guidProp != null && prefabProp != null )
//        //        {
//        //            existingPairs[ guidProp.stringValue ] = prefabProp.objectReferenceValue as GameObject;
//        //        }
//        //    }

//        //    // Rebuild pairs list to match library
//        //    _prefabPairsProp.ClearArray();
//        //    foreach ( var image in library )
//        //    {
//        //        _prefabPairsProp.arraySize++;
//        //        var element = _prefabPairsProp.GetArrayElementAtIndex( _prefabPairsProp.arraySize - 1 );

//        //        element.FindPropertyRelative( "imageGuid" ).stringValue = image.guid.ToString();
//        //        element.FindPropertyRelative( "imageName" ).stringValue = image.name;

//        //        // Preserve existing prefab assignment if it exists
//        //        if ( existingPairs.TryGetValue( image.guid.ToString(), out var existingPrefab ) )
//        //        {
//        //            element.FindPropertyRelative( "prefab" ).objectReferenceValue = existingPrefab;
//        //        }
//        //        else
//        //        {
//        //            element.FindPropertyRelative( "prefab" ).objectReferenceValue = null;
//        //        }
//        //    }
//        //}
//    }
//}
