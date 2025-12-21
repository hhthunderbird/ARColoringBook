using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.XR.ARSubsystems; // Required for XRReferenceImageLibrary

namespace Felina.ARColoringBook.Editor
{
    public class ARBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0; // Run early

        public void OnPreprocessBuild( BuildReport report )
        {
            Debug.Log( "[Felina] Validating AR Reference Libraries..." );

            // 1. Find all Reference Libraries in the project
            string[] guids = AssetDatabase.FindAssets( "t:XRReferenceImageLibrary" );

            foreach ( string guid in guids )
            {
                string path = AssetDatabase.GUIDToAssetPath( guid );
                XRReferenceImageLibrary lib = AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>( path );

                if ( lib == null ) continue;

                // 2. Check every image in the library
                for ( int i = 0; i < lib.count; i++ )
                {
                    var entry = lib[ i ];

                    // Check for Empty Name
                    if ( string.IsNullOrEmpty( entry.name ) )
                    {
                        string errorMsg = $"[Felina Build Error] In Library '{lib.name}', Entry {i} has an EMPTY Name! AR targets must be named.";

                        // Highlight the bad asset
                        EditorGUIUtility.PingObject( lib );

                        // STOP THE BUILD
                        throw new BuildFailedException( errorMsg );
                    }

                    // Check for Valid Texture
                    if ( entry.texture == null )
                    {
                        string errorMsg = $"[Felina Build Error] In Library '{lib.name}', Entry '{entry.name}' has no Texture assigned!";
                        EditorGUIUtility.PingObject( lib );
                        throw new BuildFailedException( errorMsg );
                    }
                }
            }

            Debug.Log( "[Felina] Validation Passed. Proceeding with build." );
        }
    }
}