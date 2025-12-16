using UnityEditor;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Felina.ARColoringBook.Editor
{
    [CustomEditor( typeof( ARContentSpawner ) )]
    public class ARContentSpawnerEditor : UnityEditor.Editor
    {
        private ARContentSpawner _targetScript;
        private SerializedProperty _contentLibraryProp; // the targetScript ARContentPair list to be filled

        void OnEnable()
        {
            _targetScript = ( ARContentSpawner ) target;
            _contentLibraryProp = serializedObject.FindProperty( "_contentLibrary" ); 
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawDefaultInspector();

            // resolve reference library: component > bridge component > scene manager > project first
            XRReferenceImageLibrary chosenLib = null;

            var bridge = _targetScript.GetComponent<IARBridge>();
            ARTrackedImageManager imageManager = null;
            if ( bridge != null ) imageManager = bridge.ARTrackedImageManager;
            var sceneManager = imageManager != null ? imageManager : Object.FindObjectOfType<ARTrackedImageManager>();
            var list = sceneManager != null ? sceneManager.referenceLibrary : null;

            // go from here - use `list` (XRReferenceImageLibrary or IReferenceImageLibrary) provided above
            string[] imageNames = new string[0];
            if ( list != null )
            {
                int cnt = (int)list.count;
                if ( cnt > 0 )
                {
                    imageNames = new string[ cnt ];
                    for ( int i = 0; i < cnt; i++ ) imageNames[i] = list[ i ].name;
                }
            }

            EditorGUILayout.Space();
            var arraySize = _contentLibraryProp != null ? _contentLibraryProp.arraySize : 0;
            EditorGUILayout.LabelField( $"Mappings ({arraySize})", EditorStyles.boldLabel );

            // 2. Draw list
            if ( _contentLibraryProp == null )
            {
                EditorGUILayout.HelpBox( "Serialized property 'contentLibrary' not found on ARContentSpawner.", MessageType.Error );
                serializedObject.ApplyModifiedProperties();
                return;
            }

            int listSize = _contentLibraryProp.arraySize;
            for ( int i = 0; i < listSize; i++ )
            {
                var element = _contentLibraryProp.GetArrayElementAtIndex( i );
                if ( element == null ) continue;
                SerializedProperty prefabProp = element.FindPropertyRelative( "Prefab" );
                SerializedProperty imageNameProp = element.FindPropertyRelative( "ImageName" );

                if ( imageNameProp == null )
                {
                    EditorGUILayout.HelpBox( $"Element {i} missing 'ImageName' property.", MessageType.Warning );
                    continue;
                }

                EditorGUILayout.BeginVertical( EditorStyles.helpBox );
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label( $"Entry {i + 1}", EditorStyles.miniLabel );
                if ( GUILayout.Button( "Remove", EditorStyles.miniButtonRight, GUILayout.Width( 60 ) ) )
                {
                    _contentLibraryProp.DeleteArrayElementAtIndex( i );
                    break;
                }
                EditorGUILayout.EndHorizontal();

                // image dropdown
                if ( imageNames.Length > 0 )
                {
                    int currentIndex = 0;
                    for ( int j = 0; j < imageNames.Length; j++ ) if ( imageNames[j] == imageNameProp.stringValue ) { currentIndex = j; break; }
                    int newIndex = EditorGUILayout.Popup( "Reference Image", currentIndex, imageNames );
                    if ( newIndex >= 0 && newIndex < imageNames.Length ) imageNameProp.stringValue = imageNames[newIndex];
                }
                else
                {
                    EditorGUILayout.HelpBox( "No reference images available.", MessageType.Info );
                }

                EditorGUILayout.PropertyField( prefabProp, new GUIContent( "Prefab to Spawn" ) );
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space( 2 );
            }

            // 3. Add button
            EditorGUILayout.Space();
            if ( GUILayout.Button( "Add New Mapping" ) )
            {
                _contentLibraryProp.arraySize++;
                var newElem = _contentLibraryProp.GetArrayElementAtIndex( _contentLibraryProp.arraySize - 1 );
                newElem.FindPropertyRelative( "ImageName" ).stringValue = imageNames.Length > 0 ? imageNames[0] : string.Empty;
                newElem.FindPropertyRelative( "Prefab" ).objectReferenceValue = null;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
