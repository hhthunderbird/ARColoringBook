using UnityEditor;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Felina.ARColoringBook.Editor
{
    [CustomEditor( typeof( ARPaintableObject ) )]
    public class ARPaintableObjectEditor : UnityEditor.Editor
    {
        private SerializedProperty _referenceLibraryProp;
        private SerializedProperty _referenceImageNameProp;
        private SerializedProperty _materialIndexProp;
        private SerializedProperty _texturePropertyNameProp;

        void OnEnable()
        {
            _referenceLibraryProp = serializedObject.FindProperty( "referenceLibrary" );
            _referenceImageNameProp = serializedObject.FindProperty( "_referenceImageName" );
            _materialIndexProp = serializedObject.FindProperty( "materialIndex" );
            _texturePropertyNameProp = serializedObject.FindProperty( "texturePropertyName" );
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField( _referenceLibraryProp, new GUIContent( "Reference Library" ) );

            var library = _referenceLibraryProp.objectReferenceValue as XRReferenceImageLibrary;

            if ( library != null )
            {
                if ( library.count > 0 )
                {
                    var imageNames = new string[ library.count ];
                    for ( var i = 0; i < library.count; i++ )
                    {
                        imageNames[ i ] = library[ i ].name;
                    }

                    var currentIndex = -1;
                    var currentName = _referenceImageNameProp.stringValue;

                    for ( var i = 0; i < imageNames.Length; i++ )
                    {
                        if ( imageNames[ i ] == currentName )
                        {
                            currentIndex = i;
                            break;
                        }
                    }

                    if ( currentIndex == -1 ) currentIndex = 0;

                    var newIndex = EditorGUILayout.Popup( "Reference Image", currentIndex, imageNames );

                    if ( newIndex >= 0 && newIndex < imageNames.Length )
                    {
                        _referenceImageNameProp.stringValue = imageNames[ newIndex ];
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

            EditorGUILayout.Space();
            EditorGUILayout.LabelField( "Material Settings", EditorStyles.boldLabel );
            EditorGUILayout.PropertyField( _materialIndexProp );
            EditorGUILayout.PropertyField( _texturePropertyNameProp );

            serializedObject.ApplyModifiedProperties();
        }
    }
}