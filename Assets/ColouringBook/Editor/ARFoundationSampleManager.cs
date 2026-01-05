using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace Felina.ARColoringBook.Editor
{
    /// <summary>
    /// Automatically manages AR Foundation sample files to prevent version conflicts.
    /// Detects and removes incompatible sample files based on current Unity version.
    /// </summary>
    [InitializeOnLoad]
    public class ARFoundationSampleManager
    {
        private const string PREFS_KEY = "ARColoringBook_SampleCheckDone";
        private const string SAMPLES_ROOT = "Assets/Samples";
        
        // AR Foundation sample folders that might exist
        private static readonly string[] AR_SAMPLE_PATTERNS = new[]
        {
            "AR Foundation",
            "ARFoundation",
            "XR AR Foundation"
        };

        static ARFoundationSampleManager()
        {
            // Run on editor startup with proper delegate signature
            EditorApplication.delayCall += () => CheckAndCleanSamples();
        }

        [MenuItem( "Felina/Clean AR Foundation Samples", false, 102 )]
        public static void ManualClean()
        {
            CheckAndCleanSamples( forceRun: true );
        }

        private static void CheckAndCleanSamples( bool forceRun = false )
        {
            // Skip if already checked this session (unless forced)
            if ( !forceRun && SessionState.GetBool( PREFS_KEY, false ) )
                return;

            SessionState.SetBool( PREFS_KEY, true );

            var incompatibleSamples = FindIncompatibleSamples();

            if ( incompatibleSamples.Count == 0 )
            {
                if ( forceRun )
                {
                    EditorUtility.DisplayDialog(
                        "AR Foundation Sample Check",
                        "No incompatible AR Foundation samples found.\n\nYour project is clean!",
                        "OK"
                    );
                }
                return;
            }

            // Show dialog with options
            string message = BuildWarningMessage( incompatibleSamples );
            
            int choice = EditorUtility.DisplayDialogComplex(
                "Incompatible AR Foundation Samples Detected",
                message,
                "Remove Automatically",
                "Show in Explorer",
                "Ignore"
            );

            switch ( choice )
            {
                case 0: // Remove
                    RemoveIncompatibleSamples( incompatibleSamples );
                    break;
                case 1: // Show
                    ShowSamplesInExplorer( incompatibleSamples );
                    break;
                case 2: // Ignore
                    Debug.LogWarning( "[ARFoundationSampleManager] User chose to ignore incompatible samples. This may cause compilation errors." );
                    break;
            }
        }

        private static List<SampleInfo> FindIncompatibleSamples()
        {
            var incompatibleSamples = new List<SampleInfo>();

            if ( !Directory.Exists( SAMPLES_ROOT ) )
                return incompatibleSamples;

            // Get current Unity version
            string unityVersion = Application.unityVersion;
            var currentVersion = ParseUnityVersion( unityVersion );

            // Find all AR Foundation sample folders
            var allSampleDirs = Directory.GetDirectories( SAMPLES_ROOT, "*", SearchOption.AllDirectories );

            foreach ( var pattern in AR_SAMPLE_PATTERNS )
            {
                var arSampleDirs = allSampleDirs.Where( dir => 
                    Path.GetFileName( dir ).Contains( pattern, System.StringComparison.OrdinalIgnoreCase ) 
                ).ToArray();

                foreach ( var sampleDir in arSampleDirs )
                {
                    var sampleInfo = AnalyzeSampleDirectory( sampleDir, currentVersion );
                    if ( sampleInfo != null && !sampleInfo.IsCompatible )
                    {
                        incompatibleSamples.Add( sampleInfo );
                    }
                }
            }

            return incompatibleSamples;
        }

        private static SampleInfo AnalyzeSampleDirectory( string path, UnityVersion currentVersion )
        {
            // Try to determine AR Foundation version from path
            // e.g., "Assets/Samples/AR Foundation/2.1.18/..."
            var pathParts = path.Split( Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar );
            
            string arVersion = null;
            for ( int i = 0; i < pathParts.Length; i++ )
            {
                if ( AR_SAMPLE_PATTERNS.Any( p => pathParts[i].Contains( p, System.StringComparison.OrdinalIgnoreCase ) ) )
                {
                    // Next part should be version
                    if ( i + 1 < pathParts.Length )
                    {
                        arVersion = pathParts[i + 1];
                        break;
                    }
                }
            }

            if ( string.IsNullOrEmpty( arVersion ) )
                return null;

            var sample = new SampleInfo
            {
                Path = path,
                ARFoundationVersion = arVersion,
                IsCompatible = IsVersionCompatible( arVersion, currentVersion )
            };

            return sample;
        }

        private static bool IsVersionCompatible( string arVersion, UnityVersion unityVersion )
        {
            // Parse AR Foundation version (e.g., "2.1.18" or "6.3.1")
            var parts = arVersion.Split( '.' );
            if ( parts.Length == 0 || !int.TryParse( parts[0], out int arMajor ) )
                return true; // Can't determine, assume compatible

            // Compatibility rules:
            // Unity 2019.4 - 2020.3 -> AR Foundation 2.x-4.x
            // Unity 2021.x - 2022.x -> AR Foundation 4.x-5.x
            // Unity 2023.x+, Unity 6.x -> AR Foundation 6.x+

            if ( unityVersion.Major == 2019 || unityVersion.Major == 2020 )
            {
                return arMajor >= 2 && arMajor <= 4;
            }
            else if ( unityVersion.Major == 2021 || unityVersion.Major == 2022 )
            {
                return arMajor >= 4 && arMajor <= 5;
            }
            else if ( unityVersion.Major >= 2023 || unityVersion.Major >= 6000 )
            {
                return arMajor >= 6;
            }

            return true; // Unknown version, assume compatible
        }

        private static string BuildWarningMessage( List<SampleInfo> samples )
        {
            var message = $"Found {samples.Count} incompatible AR Foundation sample folder(s) for Unity {Application.unityVersion}:\n\n";
            
            foreach ( var sample in samples.Take( 5 ) ) // Show max 5
            {
                message += $"? {sample.Path}\n  AR Foundation {sample.ARFoundationVersion}\n\n";
            }

            if ( samples.Count > 5 )
            {
                message += $"... and {samples.Count - 5} more.\n\n";
            }

            message += "These samples were designed for a different Unity version and may cause compilation errors.\n\n";
            message += "Recommended action: Remove them and import samples matching your Unity version from Package Manager.";

            return message;
        }

        private static void RemoveIncompatibleSamples( List<SampleInfo> samples )
        {
            int removedCount = 0;
            int failedCount = 0;

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach ( var sample in samples )
                {
                    try
                    {
                        if ( Directory.Exists( sample.Path ) )
                        {
                            // Remove .meta file too
                            string metaPath = sample.Path + ".meta";
                            
                            Directory.Delete( sample.Path, recursive: true );
                            if ( File.Exists( metaPath ) )
                                File.Delete( metaPath );

                            removedCount++;
                            Debug.Log( $"[ARFoundationSampleManager] Removed: {sample.Path}" );
                        }
                    }
                    catch ( System.Exception ex )
                    {
                        Debug.LogError( $"[ARFoundationSampleManager] Failed to remove {sample.Path}: {ex.Message}" );
                        failedCount++;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            string resultMessage = $"Removed {removedCount} incompatible sample folder(s).";
            if ( failedCount > 0 )
                resultMessage += $"\n\nFailed to remove {failedCount} folder(s). You may need to delete them manually.";

            EditorUtility.DisplayDialog( "Sample Cleanup Complete", resultMessage, "OK" );
        }

        private static void ShowSamplesInExplorer( List<SampleInfo> samples )
        {
            if ( samples.Count > 0 )
            {
                string path = samples[0].Path;
                EditorUtility.RevealInFinder( path );

                Debug.LogWarning( $"[ARFoundationSampleManager] Showing {samples.Count} incompatible sample(s) in Explorer. Please delete them manually." );
                foreach ( var sample in samples )
                {
                    Debug.LogWarning( $"  - {sample.Path}" );
                }
            }
        }

        private static UnityVersion ParseUnityVersion( string versionString )
        {
            // e.g., "6000.3.0f1" or "2022.3.54f1"
            var parts = versionString.Split( '.' );
            
            int major = 0, minor = 0, patch = 0;
            
            if ( parts.Length > 0 ) int.TryParse( parts[0], out major );
            if ( parts.Length > 1 ) int.TryParse( parts[1], out minor );
            if ( parts.Length > 2 )
            {
                // Remove non-numeric suffix (e.g., "0f1" -> "0")
                var patchStr = new string( parts[2].TakeWhile( char.IsDigit ).ToArray() );
                int.TryParse( patchStr, out patch );
            }

            return new UnityVersion { Major = major, Minor = minor, Patch = patch };
        }

        private class SampleInfo
        {
            public string Path;
            public string ARFoundationVersion;
            public bool IsCompatible;
        }

        private struct UnityVersion
        {
            public int Major;
            public int Minor;
            public int Patch;
        }
    }
}
