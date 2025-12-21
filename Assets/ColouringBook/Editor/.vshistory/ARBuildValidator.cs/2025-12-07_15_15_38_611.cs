using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.XR.ARSubsystems;

namespace Felina.ARColoringBook.Editor
{
    public class ARBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild( BuildReport report )
        {
            Debug.Log( "[Felina] Validating AR Reference Libraries..." );

            // Find all libraries
            string[] guids = AssetDatabase.FindAssets( "t:XRReferenceImageLibrary" );

            foreach ( string guid in guids )
            {
                string path = AssetDatabase.GUIDToAssetPath( guid );
                XRReferenceImageLibrary lib = AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>( path );

                if ( lib == null ) continue;

                // FIX: Use SerializedObject to read the raw asset data safely
                SerializedObject so = new SerializedObject( lib );
                SerializedProperty imagesProp = so.FindProperty( "m_Images" );

                if ( imagesProp == null ) continue;

                for ( int i = 0; i < imagesProp.arraySize; i++ )
                {
                    SerializedProperty entry = imagesProp.GetArrayElementAtIndex( i );
                    SerializedProperty textureProp = entry.FindPropertyRelative( "m_Texture" );
                    SerializedProperty nameProp = entry.FindPropertyRelative( "m_Name" );
                    SerializedProperty sizeProp = entry.FindPropertyRelative( "m_Size" );
                    SerializedProperty specifySizeProp = entry.FindPropertyRelative( "m_SpecifySize" );

                    string entryName = nameProp.stringValue;

                    // 1. Check Name
                    if ( string.IsNullOrEmpty( entryName ) )
                    {
                        string errorMsg = $"[Felina Build Error] In Library '{lib.name}', Entry {i} has an EMPTY Name!";
                        EditorGUIUtility.PingObject( lib );
                        throw new BuildFailedException( errorMsg );
                    }

                    // 2. Check Texture (The Robust Way)
                    if ( textureProp.objectReferenceValue == null )
                    {
                        string errorMsg = $"[Felina Build Error] In Library '{lib.name}', Entry '{entryName}' has no Texture assigned!";
                        EditorGUIUtility.PingObject( lib );
                        throw new BuildFailedException( errorMsg );
                    }

                    // 3. Optional Warning: Zero Size
                    if ( specifySizeProp.boolValue && sizeProp.vector2Value == Vector2.zero )
                    {
                        Debug.LogWarning( $"[Felina Warning] In Library '{lib.name}', Entry '{entryName}' has Physical Size set to Zero." );
                    }
                }
            }

            Debug.Log( "[Felina] Validation Passed." );
        }
    }
}