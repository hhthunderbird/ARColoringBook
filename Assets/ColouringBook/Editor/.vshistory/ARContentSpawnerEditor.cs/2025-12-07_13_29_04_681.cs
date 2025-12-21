using UnityEngine;
using UnityEditor;
using UnityEngine.XR.ARSubsystems;

namespace Felina.ARColoringBook.Editor
{
    [CustomEditor( typeof( ARContentSpawner ) )]
    public class ARContentSpawnerEditor : UnityEditor.Editor
    {
        SerializedProperty arBridgeComponentProp;
        SerializedProperty referenceLibraryProp;
        SerializedProperty contentLibraryProp;

        void OnEnable()
        {
            arBridgeComponentProp = serializedObject.FindProperty( "arBridgeComponent" );
            referenceLibraryProp = serializedObject.FindProperty( "referenceLibrary" );
            contentLibraryProp = serializedObject.FindProperty( "contentLibrary" );
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // 1. Draw Bridge Config
            EditorGUILayout.PropertyField( arBridgeComponentProp );
            EditorGUILayout.Space();

            // 2. Draw Library Config
            EditorGUILayout.LabelField( "Library Configuration", EditorStyles.boldLabel );
            EditorGUILayout.PropertyField( referenceLibraryProp );

            XRReferenceImageLibrary library = referenceLibraryProp.objectReferenceValue as XRReferenceImageLibrary;

            if ( library == null )
            {
                EditorGUILayout.HelpBox( "Assign a Reference Library to enable prefab mapping.", MessageType.Info );
                serializedObject.ApplyModifiedProperties();
                return;
            }

            // 3. Prepare Image Names
            string[] imageNames = new string[ library.count ];
            for ( int i = 0; i < library.count; i++ )
            {
                imageNames[ i ] = library[ i ].name;
            }

            if ( imageNames.Length == 0 )
            {
                EditorGUILayout.HelpBox( "Selected Library is empty.", MessageType.Warning );
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField( $"Mappings ({contentLibraryProp.arraySize})", EditorStyles.boldLabel );

            // 4. Draw List Manually to inject Dropdown Logic
            // We iterate through the array property to draw each element
            for ( int i = 0; i < contentLibraryProp.arraySize; i++ )
            {
                SerializedProperty element = contentLibraryProp.GetArrayElementAtIndex( i );
                SerializedProperty prefabProp = element.FindPropertyRelative( "prefab" );
                SerializedProperty imageNameProp = element.FindPropertyRelative( "imageName" );

                EditorGUILayout.BeginVertical( EditorStyles.helpBox );

                // Header for the Item
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label( $"Entry {i + 1}", EditorStyles.miniLabel );
                if ( GUILayout.Button( "Remove", EditorStyles.miniButtonRight, GUILayout.Width( 60 ) ) )
                {
                    contentLibraryProp.DeleteArrayElementAtIndex( i );
                    break; // Break loop to avoid index errors after deletion
                }
                EditorGUILayout.EndHorizontal();

                // Dropdown Logic
                int currentIndex = -1;
                string currentName = imageNameProp.stringValue;

                // Find index of current string
                for ( int j = 0; j < imageNames.Length; j++ )
                {
                    if ( imageNames[ j ] == currentName )
                    {
                        currentIndex = j;
                        break;
                    }
                }
                if ( currentIndex == -1 ) currentIndex = 0; // Default to first

                // Draw Popup
                int newIndex = EditorGUILayout.Popup( "Image Target", currentIndex, imageNames );
                if ( newIndex >= 0 && newIndex < imageNames.Length )
                {
                    imageNameProp.stringValue = imageNames[ newIndex ];
                }

                // Draw Prefab Field
                EditorGUILayout.PropertyField( prefabProp, new GUIContent( "Prefab to Spawn" ) );

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space( 2 );
            }

            // 5. Add Button
            if ( GUILayout.Button( "Add New Mapping" ) )
            {
                contentLibraryProp.InsertArrayElementAtIndex( contentLibraryProp.arraySize );
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}