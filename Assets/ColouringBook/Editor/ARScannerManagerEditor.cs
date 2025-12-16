using UnityEditor;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Felina.ARColoringBook.Editor
{
    [CustomEditor( typeof( ARScannerManager ) )]
    public class ARScannerManagerEditor : UnityEditor.Editor
    {
        private ARScannerManager _targetScript;
        private SerializedProperty _targetListProp;
        private SerializedProperty _settingsProp;

        private void OnEnable()
        {
            _targetScript = ( ARScannerManager ) target;
            _targetListProp = serializedObject.FindProperty( "_targetList" );
            _settingsProp = serializedObject.FindProperty( "settings" );
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField( $"Runtime Debugging", EditorStyles.boldLabel );

            var bridge = _targetScript.GetComponent<IARBridge>();
            ARTrackedImageManager imageManager = null;
            if ( bridge != null ) imageManager = bridge.ARTrackedImageManager;
            var sceneManager = imageManager != null ? imageManager : Object.FindObjectOfType<ARTrackedImageManager>();
            var list = sceneManager != null ? sceneManager.referenceLibrary : null;

            // Active targets preview / populate
            EditorGUILayout.Space();
            EditorGUILayout.LabelField( "Active Targets", EditorStyles.boldLabel );

            if ( _targetListProp == null )
            {
                EditorGUILayout.HelpBox( "Serialized _targetList not found on ARScannerManager. Field name must match exactly.", MessageType.Error );
                serializedObject.ApplyModifiedProperties();
                return;
            }

            // Show mismatch warning if library present
            // Note: we will re-read arraySize after possible modification to avoid stale values
            int listCount = _targetListProp.arraySize;
            if ( list != null && listCount != list.count )
            {
                EditorGUILayout.HelpBox( $"ReferenceImageLibrary count ({list.count}) differs from ARScannerManager target list count ({listCount}). Press Refresh to sync.", MessageType.Warning );
            }

            // Always show button to populate/refresh
            if ( !Application.isPlaying && GUILayout.Button( "Refresh Active Targets from Reference Library" ) )
            {
                ARTrackedImageManager mgr = sceneManager;
                if ( mgr == null )
                {
                    EditorUtility.DisplayDialog( "Populate Failed", "No ARTrackedImageManager found in scene or bridge.", "OK" );
                }
                else
                {
                    Undo.RecordObject( _targetScript, "Populate Active Targets" );

                    // clear existing
                    _targetListProp.ClearArray();
                    _targetListProp.arraySize = 0;

                    for ( int i = 0; i < mgr.referenceLibrary.count; i++ )
                    {
                        var libEntry = mgr.referenceLibrary[ i ];
                        _targetListProp.arraySize++;
                        var newElem = _targetListProp.GetArrayElementAtIndex( _targetListProp.arraySize - 1 );

                        newElem.FindPropertyRelative( "Name" ).stringValue = libEntry.name;
                        newElem.FindPropertyRelative( "Size" ).vector2Value = new Vector2( libEntry.size.x, libEntry.size.y );
                        newElem.FindPropertyRelative( "IsTracking" ).boolValue = false;
                        newElem.FindPropertyRelative( "Transform" ).objectReferenceValue = null;
                        newElem.FindPropertyRelative( "Score" ).floatValue = 0f;
                    }

                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty( _targetScript );
                    if ( !Application.isPlaying ) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty( _targetScript.gameObject.scene );
                }
            }

            // Refresh serialized data after possible modifications
            serializedObject.Update();

            // Re-read array size in case we changed it above
            listCount = _targetListProp.arraySize;

            // Display existing entries
            if ( listCount == 0 )
            {
                EditorGUILayout.HelpBox( "No active targets populated. You can populate from the Reference Image Library.", MessageType.Info );
            }
            else
            {
                EditorGUILayout.LabelField( $"Active Targets ({listCount})", EditorStyles.boldLabel );
                EditorGUI.indentLevel++;

                for ( int i = 0; i < listCount; i++ )
                {
                    if ( i < 0 || i >= _targetListProp.arraySize ) break;
                    var elem = _targetListProp.GetArrayElementAtIndex( i );
                    if ( elem == null ) continue;
                    var nameProp = elem.FindPropertyRelative( "Name" );           // string
                    var sizeProp = elem.FindPropertyRelative( "Size" );           // Vector2
                    if ( nameProp == null || sizeProp == null ) continue;

                    var name = nameProp.stringValue;

                    // Try to get thumbnail from the reference image library (scene manager)
                    Texture2D thumb = null;
                    var sceneMgr = Object.FindObjectOfType<ARTrackedImageManager>();
                    var refLib = sceneMgr != null ? sceneMgr.referenceLibrary as XRReferenceImageLibrary : null;
                    if ( refLib != null )
                    {
                        for ( int j = 0; j < refLib.count; j++ )
                        {
                            var ri = refLib[ j ];
                            if ( ri.name == name )
                            {
                                // XRReferenceImage exposes a texture (may be null)
                                try { thumb = ri.texture as Texture2D; } catch { thumb = null; }
                                break;
                            }
                        }
                    }

                    EditorGUILayout.BeginHorizontal( EditorStyles.helpBox );
                    if ( thumb != null )
                    {
                        GUILayout.Label( thumb, GUILayout.Width( 64 ), GUILayout.Height( 64 ) );
                    }
                    else
                    {
                        GUILayout.Box( "No Image", GUILayout.Width( 64 ), GUILayout.Height( 64 ) );
                    }

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField( name, EditorStyles.boldLabel );
                    EditorGUILayout.LabelField( $"Size: {sizeProp.vector2Value.x} x {sizeProp.vector2Value.y}" );
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }



            // AUTOMATICALLY FIND SETTINGS
            if ( _settingsProp != null && _settingsProp.objectReferenceValue == null )
            {
                EditorGUILayout.HelpBox( "Settings not linked. Searching...", MessageType.Warning );

                string[] guids = AssetDatabase.FindAssets( "t:Settings" );

                if ( guids.Length > 0 )
                {
                    string path = AssetDatabase.GUIDToAssetPath( guids[ 0 ] );
                    
                    var settings = AssetDatabase.LoadAssetAtPath<Settings>( path );

                    _settingsProp.objectReferenceValue = settings;

                    serializedObject.ApplyModifiedProperties();
                    var done = _settingsProp.serializedObject.ApplyModifiedProperties();
                    
                    Debug.Log( $"[Felina] Assigned Settings asset to ARScannerManager: {_settingsProp.objectReferenceValue} = {done}" );
                    
                    Debug.Log( $" [Felina] Auto-linked settings from: {path}" );
                }
                else
                {
                    EditorGUILayout.HelpBox( " No 'Settings' file found in Project! Please Create one.", MessageType.Error );
                    if ( GUILayout.Button( "Create Settings Now" ) )
                    {
                        CreateSettingsFile();
                    }
                }

                serializedObject.ApplyModifiedProperties();
            }
        }
        private void CreateSettingsFile()
        {
            Settings asset = ScriptableObject.CreateInstance<Settings>();
            AssetDatabase.CreateAsset( asset, "Assets/ColouringBook/Settings/Settings.asset" );
            AssetDatabase.SaveAssets();
            // The next OnInspectorGUI draw will pick it up automatically
        }
    }

}
