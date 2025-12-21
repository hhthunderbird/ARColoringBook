using UnityEngine;
using UnityEditor;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;
using System.Linq;

namespace Felina.ARColoringBook.Editor // Good practice to put Editor scripts in .Editor namespace
{
    [CustomEditor( typeof( ARPaintableObject ) )]
    public class ARPaintableObjectEditor : UnityEditor.Editor
    {
        SerializedProperty referenceLibraryProp;
        SerializedProperty referenceImageNameProp;
        SerializedProperty materialIndexProp;
        SerializedProperty texturePropertyNameProp;

        void OnEnable()
        {
            referenceLibraryProp = serializedObject.FindProperty( "referenceLibrary" );
            referenceImageNameProp = serializedObject.FindProperty( "referenceImageName" );
            materialIndexProp = serializedObject.FindProperty( "materialIndex" );
            texturePropertyNameProp = serializedObject.FindProperty( "texturePropertyName" );
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // 1. Draw the Library Selector
            EditorGUILayout.PropertyField( referenceLibraryProp, new GUIContent( "Reference Library" ) );

            // 2. Draw the Image Dropdown (Only if library is selected)
            XRReferenceImageLibrary library = referenceLibraryProp.objectReferenceValue as XRReferenceImageLibrary;

            if ( library != null )
            {
                if ( library.count > 0 )
                {
                    // Fetch all image names from the library
                    string[] imageNames = new string[ library.count ];
                    for ( int i = 0; i < library.count; i++ )
                    {
                        imageNames[ i ] = library[ i ].name;
                    }

                    // Find the index of the currently selected name
                    int currentIndex = -1;
                    string currentName = referenceImageNameProp.stringValue;

                    // Try to find the current string in the new list
                    for ( int i = 0; i < imageNames.Length; i++ )
                    {
                        if ( imageNames[ i ] == currentName )
                        {
                            currentIndex = i;
                            break;
                        }
                    }

                    // If the current name isn't found (or is empty), default to 0
                    if ( currentIndex == -1 ) currentIndex = 0;

                    // Draw the Popup
                    int newIndex = EditorGUILayout.Popup( "Reference Image", currentIndex, imageNames );

                    // Save the selection back to the string property
                    if ( newIndex >= 0 && newIndex < imageNames.Length )
                    {
                        referenceImageNameProp.stringValue = imageNames[ newIndex ];
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox( "Selected Library is empty.", MessageType.Warning );
                }
            }
            else
            {
                EditorGUILayout.HelpBox( "Please assign an XR Reference Image Library to select an image.", MessageType.Info );
            }

            // 3. Draw the rest of the settings
            EditorGUILayout.Space();
            EditorGUILayout.LabelField( "Material Settings", EditorStyles.boldLabel );
            EditorGUILayout.PropertyField( materialIndexProp );
            EditorGUILayout.PropertyField( texturePropertyNameProp );

            serializedObject.ApplyModifiedProperties();
        }
    }
}