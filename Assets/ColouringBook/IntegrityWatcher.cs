//#if UNITY_EDITOR
//using UnityEditor;
//using UnityEngine;
//using System.IO;
//using System.Security.Cryptography;
//using System.Collections.Generic;
//using System.Text;

//namespace Felina.ARColoringBook.Editor
//{
//    [InitializeOnLoad]
//    public class IntegrityWatcher
//    {
//        // 1. REGISTER FILES TO PROTECT
//        // Update these hashes AFTER you minify/finalize your files.
//        // Use the Menu Item "Felina/Dev/Log Current Hashes" to get the values.
//            private static readonly Dictionary<string, string> _goldenHashes = new Dictionary<string, string>
//            {
//                // Update these paths to match your actual folder structure
//                { "Assets/ColouringBook/Internals.cs",      "REPLACE_WITH_ACTUAL_HASH" },
//                { "Assets/ColouringBook/New Folder/LicenseManager.cs", "REPLACE_WITH_ACTUAL_HASH" },
//                { "Assets/ColouringBook/Editor/BuildEnforcer.cs",   "REPLACE_WITH_ACTUAL_HASH" }
//            };

//        static IntegrityWatcher()
//        {
//            // Runs every time Unity recompiles or opens
//            ValidateFiles();
//        }

//        private static void ValidateFiles()
//        {
//            bool tamperingDetected = false;

//            foreach ( var kvp in _goldenHashes )
//            {
//                string path = kvp.Key;
//                string expectedHash = kvp.Value;

//                if ( !File.Exists( path ) )
//                {
//                    // File missing isn't tampering, but it is broken
//                    Debug.LogWarning( $"[Felina Integrity] Missing file: {path}" );
//                    continue;
//                }

//                string currentHash = ComputeFileHash( path );

//                if ( !currentHash.Equals( expectedHash, System.StringComparison.OrdinalIgnoreCase ) )
//                {
//                    Debug.LogError( $"<color=red>[Felina Security] TAMPERING DETECTED: {path}</color>\n" +
//                                   $"This file has been modified! Revert changes immediately to ensure license stability.\n" +
//                                   $"Expected: {expectedHash}\nActual: {currentHash}" );
//                    tamperingDetected = true;
//                }
//            }

//            if ( tamperingDetected )
//            {
//                // Annoying Popup
//                EditorUtility.DisplayDialog( "Security Alert",
//                    "Core Felina files have been modified.\nThis voids the support warranty and may break builds.\n\nCheck Console for details.",
//                    "I Understand" );
//            }
//        }

//        private static string ComputeFileHash( string filePath )
//        {
//            using ( var md5 = MD5.Create() )
//            {
//                using ( var stream = File.OpenRead( filePath ) )
//                {
//                    byte[] hashBytes = md5.ComputeHash( stream );
//                    return System.BitConverter.ToString( hashBytes ).Replace( "-", "" ).ToLowerInvariant();
//                }
//            }
//        }

//        // --- DEV TOOL ---
//        // Click this to get the hashes you need to paste above
//        [MenuItem( "Felina/Dev/Log Current Hashes" )]
//        public static void LogHashes()
//        {
//            StringBuilder sb = new StringBuilder( "<b>CURRENT FILE HASHES (Copy these to IntegrityWatcher.cs):</b>\n\n" );
//            foreach ( var kvp in _goldenHashes )
//            {
//                if ( File.Exists( kvp.Key ) )
//                {
//                    string hash = ComputeFileHash( kvp.Key );
//                    sb.AppendLine( $"{{ \"{kvp.Key}\", \"{hash}\" }}," );
//                }
//                else
//                {
//                    sb.AppendLine( $"// File not found: {kvp.Key}" );
//                }
//            }
//            Debug.Log( sb.ToString() );
//        }
//    }
//}
//#endif