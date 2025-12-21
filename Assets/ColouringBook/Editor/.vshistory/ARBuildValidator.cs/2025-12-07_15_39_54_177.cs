using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using static UnityEngine.EventSystems.EventTrigger;

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
                var enumerator = lib.GetEnumerator();
                int i = -1;
                while ( enumerator.MoveNext() )
                {
                    i++;
                    var item = enumerator.Current;

                    // 1. Check Name
                    if ( string.IsNullOrEmpty( item.name ) )
                    {
                        string errorMsg = $"[Felina Build Error] In Library '{lib.name}', Entry {i} has an EMPTY Name!";
                        EditorGUIUtility.PingObject( lib );
                        throw new BuildFailedException( errorMsg );
                    }

                    // 2. Check Texture
                    if ( item.texture == null )
                    {
                        string errorMsg = $"[Felina Build Error] In Library '{lib.name}', Entry '{item.name}' has no Texture assigned!";
                        EditorGUIUtility.PingObject( lib );
                        throw new BuildFailedException( errorMsg );
                    }

                    // 3. Optional Warning: Zero Size
                    if ( item.specifySize && item.size == Vector2.zero )
                    {
                        Debug.LogWarning( $"[Felina Warning] In Library '{lib.name}', Entry '{item.name}' has Physical Size set to Zero." );
                    }
                }
            }

            Debug.Log( "[Felina] Validation Passed." );
        }
    }
}