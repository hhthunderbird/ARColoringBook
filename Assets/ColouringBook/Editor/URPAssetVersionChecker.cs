using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Felina.ARColoringBook.Editor
{
    [InitializeOnLoad]
    public class URPAssetVersionChecker : AssetPostprocessor
    {
        static URPAssetVersionChecker()
        {
            CheckAllURPAssets();
        }

        static void OnPostprocessAllAssets( string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths )
        {
            bool urpAssetChanged = importedAssets.Any( path => path.EndsWith( ".asset" ) );

            if ( urpAssetChanged )
            {
                CheckAllURPAssets();
            }
        }

        static void CheckAllURPAssets()
        {
            string[] guids = AssetDatabase.FindAssets( "t:UniversalRenderPipelineAsset" );

            foreach ( string guid in guids )
            {
                string path = AssetDatabase.GUIDToAssetPath( guid );
                UniversalRenderPipelineAsset asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>( path );

                if ( asset != null )
                {
                    if ( EditorUtility.IsDirty( asset ) )
                    {
                        Debug.LogWarning( $"[URP Check] Asset '{asset.name}' at {path} might need saving/updating to match current URP version." );
                    }
                }
            }
        }
    }
}