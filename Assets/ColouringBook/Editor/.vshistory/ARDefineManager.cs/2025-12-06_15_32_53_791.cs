using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Felina.ARColoringBook.Editor
{
    [InitializeOnLoad]
    public static class ARDefineManager
    {
        private const string ARFOUNDATION_PACKAGE_ID = "com.unity.xr.arfoundation";
        private const string FELINA_DEFINE_SYMBOL = "FELINA_ARFOUNDATION";

        static ARDefineManager()
        {
            // Subscribe to package manager events to detect installs/uninstalls
            Events.registeredPackages += OnPackagesRegistered;
            // Also run a check on generic reloads (startup)
            CheckForARFoundation();
        }

        private static void OnPackagesRegistered( PackageRegistrationEventArgs args )
        {
            // If ARFoundation was just added or removed, re-run our check
            bool checkNeeded = args.added.Any( p => p.name == ARFOUNDATION_PACKAGE_ID ) ||
                               args.removed.Any( p => p.name == ARFOUNDATION_PACKAGE_ID );

            if ( checkNeeded )
            {
                CheckForARFoundation();
            }
        }

        /// <summary>
        /// Asks the Package Manager: "Is ARFoundation installed?"
        /// </summary>
        private static void CheckForARFoundation()
        {
            ListRequest listRequest = Client.List( true ); // List all packages, including offline
            EditorApplication.update += () => ProgressRequest( listRequest );
        }

        private static void ProgressRequest( ListRequest request )
        {
            if ( !request.IsCompleted ) return;

            EditorApplication.update -= () => ProgressRequest( request );

            if ( request.Status == StatusCode.Success )
            {
                bool hasARFoundation = request.Result.Any( p => p.name == ARFOUNDATION_PACKAGE_ID );
                UpdateDefines( hasARFoundation );
            }
            else
            {
                Debug.LogWarning( "[Felina] Failed to check packages: " + request.Error.message );
            }
        }

        private static void UpdateDefines( bool enable )
        {
            string currentDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup( EditorUserBuildSettings.selectedBuildTargetGroup );
            bool hasSymbol = currentDefines.Contains( FELINA_DEFINE_SYMBOL );

            if ( enable && !hasSymbol )
            {
                // Add the symbol
                string newDefines = currentDefines + ";" + FELINA_DEFINE_SYMBOL;
                PlayerSettings.SetScriptingDefineSymbolsForGroup( EditorUserBuildSettings.selectedBuildTargetGroup, newDefines );
                Debug.Log( $"[Felina] ARFoundation detected. Adding define: {FELINA_DEFINE_SYMBOL}" );
            }
            else if ( !enable && hasSymbol )
            {
                // Remove the symbol
                string newDefines = currentDefines.Replace( FELINA_DEFINE_SYMBOL, "" ).Replace( ";;", ";" ); // Cleanup
                PlayerSettings.SetScriptingDefineSymbolsForGroup( EditorUserBuildSettings.selectedBuildTargetGroup, newDefines );
                Debug.Log( $"[Felina] ARFoundation removed. Removing define: {FELINA_DEFINE_SYMBOL}" );
            }
        }
    }
}