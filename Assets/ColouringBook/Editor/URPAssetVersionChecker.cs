using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using System.Linq;

[InitializeOnLoad]
public class URPAssetVersionChecker : AssetPostprocessor
{
    // Static constructor runs on Editor startup and recompile
    static URPAssetVersionChecker()
    {
        CheckAllURPAssets();
    }

    // Runs whenever assets are imported, deleted, or moved
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
        // Find all URP Assets in the project
        string[] guids = AssetDatabase.FindAssets( "t:UniversalRenderPipelineAsset" );

        foreach ( string guid in guids )
        {
            string path = AssetDatabase.GUIDToAssetPath( guid );
            UniversalRenderPipelineAsset asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>( path );

            if ( asset != null )
            {
                // Logic to check compatibility
                // Note: URP doesn't expose a public "version" field easily readable on the asset itself 
                // that differs from the package version, but we can check if it matches the active one
                // or if it triggers Unity's internal "dirty" flags for upgrade.

                // A common "outdated" sign is if the asset lacks certain new properties 
                // or if the Editor marks it as dirty immediately after load.

                if ( EditorUtility.IsDirty( asset ) )
                {
                    Debug.LogWarning( $"[URP Check] Asset '{asset.name}' at {path} might need saving/updating to match current URP version." );
                }

                // Optional: Check against the currently active asset
                if ( GraphicsSettings.currentRenderPipeline == asset )
                {
                    // This is the active one, verify it works
                    // (You could add custom logic here if you have specific settings to enforce)
                }
            }
        }
    }
}