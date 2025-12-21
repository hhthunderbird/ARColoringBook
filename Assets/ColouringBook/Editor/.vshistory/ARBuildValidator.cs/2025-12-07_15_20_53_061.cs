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

                // Use the public API on XRReferenceImageLibrary to read image entries reliably
                for ( int i = 0; i < lib.count; i++ )
                {
                    var entry = lib[ i ];
                    string entryName = entry.name;

                    // 1. Check Name
                    if ( string.IsNullOrEmpty( entryName ) )
                    {
                        string errorMsg = $"[Felina Build Error] In Library '{lib.name}', Entry {i} has an EMPTY Name!";
                        EditorGUIUtility.PingObject( lib );
                        throw new BuildFailedException( errorMsg );
                    }

                    // 2. Check Texture
                    if ( entry.texture == null )
                    {
                        string errorMsg = $"[Felina Build Error] In Library '{lib.name}', Entry '{entryName}' has no Texture assigned!";
                        EditorGUIUtility.PingObject( lib );
                        throw new BuildFailedException( errorMsg );
                    }

                    // 3. Optional Warning: Zero Size
                    if ( entry.specifySize && entry.size == Vector2.zero )
                    {
                        Debug.LogWarning( $"[Felina Warning] In Library '{lib.name}', Entry '{entryName}' has Physical Size set to Zero." );
                    }
                }
            }

            Debug.Log( "[Felina] Validation Passed." );
        }
    }
}