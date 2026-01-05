using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.Networking;

namespace Felina.ARColoringBook.Editor
{
    [InitializeOnLoad]
    public class ARSampleInstaller : EditorWindow
    {
        private const string ARF_PACKAGE_ID = "com.unity.xr.arfoundation";
        private const string REPO_URL_BASE = "https://github.com/Unity-Technologies/arfoundation-samples/archive";

        private const string INSTALL_PATH = "Assets";
        private const string VERSION_FILE_NAME = "ar_samples_version.txt";

        private static readonly Dictionary<string, string> _versionToBranchMap = new Dictionary<string, string>
        {
            { "2.1", "2.1" }, { "3.0", "3.0" }, { "3.1", "3.1" },
            { "4.0", "4.0" }, { "4.1", "4.1" }, { "4.2", "4.2" }, { "5.0", "main" }, { "5.1", "main" }
        };

        private static ListRequest _listRequest;
        private static AddRequest _addRequest;
        private static bool _isChecking;
        private static Queue<string> _dependencyQueue = new Queue<string>();

        private static UnityWebRequest _currentDownload;
        private static string _tempZipPath;
        private static string _pendingNewBranch;
        private static string _pendingOldBranch;
        private static bool _isDownloadingOld;

        [SerializeField] private string _detectedPackageVersion;
        [SerializeField] private string _targetBranch;
        [SerializeField] private string _localVersion;
        [SerializeField] private bool _isWorking;
        [SerializeField] private string _statusMessage;

        [MenuItem( "Project Setup/Check AR Samples" )]
        public static void ShowWindow()
        {
            var win = GetWindow<ARSampleInstaller>( "AR Sample Installer" );
            win.Show();
            CheckDependencies();
        }

        static ARSampleInstaller()
        {
            EditorApplication.delayCall += CheckDependencies;
        }

        private static void CheckDependencies()
        {
            if ( _isChecking ) return;
            _isChecking = true;
            _listRequest = Client.List( true );
            EditorApplication.update += ProgressCheck;
        }

        private static void ProgressCheck()
        {
            if ( _currentDownload != null )
            {
                if ( !_currentDownload.isDone )
                {
                    float progress = _currentDownload.downloadProgress;
                    EditorUtility.DisplayProgressBar( "Downloading Samples", $"{( int ) ( progress * 100 )}%...", progress );
                }
                else
                {
                    EditorUtility.ClearProgressBar();
                    FinishAsyncDownload();
                }
                return;
            }

            if ( _addRequest != null )
            {
                if ( !_addRequest.IsCompleted ) return;
                _addRequest = null;
                ProcessDependencyQueue();
                return;
            }

            if ( _listRequest != null && _listRequest.IsCompleted )
            {
                if ( _addRequest == null && _dependencyQueue.Count == 0 )
                {
                    EditorApplication.update -= ProgressCheck;
                    _isChecking = false;
                    string versionFound = null;
                    if ( _listRequest.Status == StatusCode.Success )
                    {
                        var pkg = _listRequest.Result.FirstOrDefault( p => p.name == ARF_PACKAGE_ID );
                        if ( pkg != null ) versionFound = pkg.version;
                    }
                    if ( string.IsNullOrEmpty( versionFound ) ) versionFound = GetVersionFromFileSystem();
                    ValidateInstallation( versionFound );
                }
            }
        }

        private int ProcessFiles( string newRefPath, string oldRefPath, string backupPath )
        {
            int backupCount = 0;
            string newAssetsRoot = Path.Combine( newRefPath, "Assets" );
            string oldAssetsRoot = oldRefPath != null ? Path.Combine( oldRefPath, "Assets" ) : null;

            HashSet<string> processedRelPaths = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
            HashSet<string> sampleDirectories = new HashSet<string>( StringComparer.OrdinalIgnoreCase );

            if ( Directory.Exists( newAssetsRoot ) )
            {
                var newFiles = Directory.GetFiles( newAssetsRoot, "*", SearchOption.AllDirectories )
                                        .Where( f => !f.EndsWith( ".meta" ) && !IsSystemFile( f ) );

                foreach ( var newFile in newFiles )
                {
                    string relPath = NormalizePath( newFile.Substring( newAssetsRoot.Length + 1 ) );
                    string targetPath = Path.Combine( INSTALL_PATH, relPath );

                    processedRelPaths.Add( relPath );

                    string relDir = Path.GetDirectoryName( relPath );
                    if ( !string.IsNullOrEmpty( relDir ) ) sampleDirectories.Add( NormalizePath( relDir ) );

                    if ( File.Exists( targetPath ) )
                    {
                        if ( GetFileHash( newFile ) != GetFileHash( targetPath ) )
                        {
                            BackupFile( targetPath, relPath, backupPath );
                            backupCount++;
                        }
                    }
                }
            }

            if ( oldAssetsRoot != null && Directory.Exists( oldAssetsRoot ) )
            {
                var oldFiles = Directory.GetFiles( oldAssetsRoot, "*", SearchOption.AllDirectories )
                                        .Where( f => !f.EndsWith( ".meta" ) && !IsSystemFile( f ) );

                foreach ( var oldFile in oldFiles )
                {
                    string relPath = NormalizePath( oldFile.Substring( oldAssetsRoot.Length + 1 ) );
                    string relDir = Path.GetDirectoryName( relPath );
                    if ( !string.IsNullOrEmpty( relDir ) ) sampleDirectories.Add( NormalizePath( relDir ) );

                    if ( processedRelPaths.Contains( relPath ) ) continue;

                    string targetPath = Path.Combine( INSTALL_PATH, relPath );

                    if ( File.Exists( targetPath ) )
                    {
                        string oldOriginalHash = GetFileHash( oldFile );
                        string currentHash = GetFileHash( targetPath );

                        if ( oldOriginalHash != currentHash )
                        {
                            BackupFile( targetPath, relPath, backupPath );
                            backupCount++;
                        }
                        else
                        {
                            File.Delete( targetPath );
                            if ( File.Exists( targetPath + ".meta" ) ) File.Delete( targetPath + ".meta" );
                        }
                    }
                }
            }

            foreach ( string relDir in sampleDirectories )
            {
                string fullTargetDir = Path.Combine( INSTALL_PATH, relDir );
                if ( !Directory.Exists( fullTargetDir ) ) continue;

                var localFiles = Directory.GetFiles( fullTargetDir, "*", SearchOption.TopDirectoryOnly )
                                          .Where( f => !f.EndsWith( ".meta" ) && !IsSystemFile( f ) );

                foreach ( var localFile in localFiles )
                {
                    string relPath = NormalizePath( localFile.Substring( INSTALL_PATH.Length + 1 ) );

                    if ( !processedRelPaths.Contains( relPath ) )
                    {
                        bool existedInOld = false;
                        if ( oldAssetsRoot != null )
                        {
                            string oldPathCheck = Path.Combine( oldAssetsRoot, relPath );
                            if ( File.Exists( oldPathCheck ) ) existedInOld = true;
                        }

                        if ( !existedInOld )
                        {
                            BackupFile( localFile, relPath, backupPath );
                            backupCount++;
                        }
                    }
                }
            }

            return backupCount;
        }

        private static string NormalizePath( string path )
        {
            return path.Replace( "\\", "/" ).TrimStart( '/' );
        }

        private static bool IsSystemFile( string path )
        {
            string name = Path.GetFileName( path ).ToLower();
            return name == ".ds_store" || name == "thumbs.db";
        }

        private static string GetFileHash( string filename )
        {
            string ext = Path.GetExtension( filename ).ToLower();
            bool isText = IsTextAsset( ext );
            using ( var md5 = MD5.Create() )
            {
                if ( isText )
                {
                    string content = File.ReadAllText( filename ).Replace( "\r\n", "\n" ).Replace( "\r", "\n" );
                    byte[] bytes = System.Text.Encoding.UTF8.GetBytes( content );
                    return BitConverter.ToString( md5.ComputeHash( bytes ) ).Replace( "-", "" ).ToLowerInvariant();
                }
                else
                {
                    using ( var stream = File.OpenRead( filename ) )
                        return BitConverter.ToString( md5.ComputeHash( stream ) ).Replace( "-", "" ).ToLowerInvariant();
                }
            }
        }

        private static bool IsTextAsset( string ext )
        {
            return ext == ".cs" || ext == ".js" || ext == ".shader" || ext == ".txt" ||
                   ext == ".xml" || ext == ".json" || ext == ".manifest" || ext == ".unity" ||
                   ext == ".prefab" || ext == ".asset" || ext == ".mat" || ext == ".controller" ||
                   ext == ".asmdef" || ext == ".md" || ext == ".rsp" || ext == ".gradle";
        }

        private static void FinishAsyncDownload()
        {
            if ( _currentDownload.isNetworkError || _currentDownload.isHttpError )
            {
                string errorMsg = _currentDownload.error;
                string urlAttempted = _currentDownload.url;

                Debug.LogError( $"Download Failed.\nURL: {urlAttempted}\nError: {errorMsg}" );

                _currentDownload.Dispose();
                _currentDownload = null;

                var win = GetWindow<ARSampleInstaller>();
                win._isWorking = false;

                // POPUP DIALOG FOR THE USER
                EditorUtility.DisplayDialog( "Download Failed",
                    $"Could not download samples from GitHub.\n\nError: {errorMsg}\n\nCheck your internet connection or if the branch exists.",
                    "OK" );
                return;
            }

            try
            {
                File.WriteAllBytes( _tempZipPath, _currentDownload.downloadHandler.data );
            }
            catch ( Exception e )
            {
                Debug.LogError( $"File Write Error: {e.Message}" );
                EditorUtility.DisplayDialog( "Write Error", "Could not save the zip file to Temp folder.", "OK" );
                _currentDownload.Dispose();
                _currentDownload = null;
                GetWindow<ARSampleInstaller>()._isWorking = false;
                return;
            }

            _currentDownload.Dispose();
            _currentDownload = null;

            string extractPath = _isDownloadingOld ? "Temp/Temp_OldReference" : "Temp/Temp_NewInstall";

            // Extract safely
            try
            {
                ExtractZip( _tempZipPath, extractPath );
            }
            catch ( Exception e )
            {
                EditorUtility.DisplayDialog( "Extraction Error", $"Failed to unzip samples.\n{e.Message}", "OK" );
                GetWindow<ARSampleInstaller>()._isWorking = false;
                return;
            }

            var window = GetWindow<ARSampleInstaller>();
            if ( _isDownloadingOld )
            {
                _isDownloadingOld = false;
                StartAsyncDownload( _pendingNewBranch, false );
            }
            else
            {
                window.CompleteSmartInstall( "Temp/Temp_NewInstall", "Temp/Temp_OldReference" );
            }
        }

        private static void StartAsyncDownload( string branch, bool isOldVersion )
        {
            string url = $"{REPO_URL_BASE}/{branch}.zip";
            _tempZipPath = FileUtil.GetUniqueTempPathInProject() + ".zip";
            _isDownloadingOld = isOldVersion;
            _currentDownload = UnityWebRequest.Get( url );
            _currentDownload.SendWebRequest();
            EditorApplication.update -= ProgressCheck;
            EditorApplication.update += ProgressCheck;
        }

        private static void ExtractZip( string zipPath, string destFolder )
        {
            if ( Directory.Exists( destFolder ) ) Directory.Delete( destFolder, true );
            Directory.CreateDirectory( destFolder );
            System.IO.Compression.ZipFile.ExtractToDirectory( zipPath, destFolder );
            if ( File.Exists( zipPath ) ) File.Delete( zipPath );
        }

        private void PerformSmartInstall( string newBranch, string oldBranch )
        {
            _isWorking = true;
            _statusMessage = "Starting Download...";
            _pendingNewBranch = newBranch;
            _pendingOldBranch = oldBranch;

            if ( oldBranch != "None" && oldBranch != newBranch ) StartAsyncDownload( oldBranch, true );
            else StartAsyncDownload( newBranch, false );
        }

        public void CompleteSmartInstall( string newExtractPath, string oldExtractPath )
        {
            try
            {
                string newRefPath = Directory.GetDirectories( newExtractPath )[ 0 ];
                string oldRefPath = Directory.Exists( oldExtractPath ) ? Directory.GetDirectories( oldExtractPath )[ 0 ] : null;

                _statusMessage = "Analyzing file conflicts...";
                string backupFolder = $"Assets/AR_Backup/Backup_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}";
                int backedUpCount = ProcessFiles( newRefPath, oldRefPath, backupFolder );

                _statusMessage = "Checking dependencies...";
                InstallDependenciesFromManifest( newRefPath );

                _statusMessage = "Copying new files...";
                CopyFiles( newRefPath );

                string majorMinor = GetMajorMinor( _detectedPackageVersion );
                File.WriteAllText( Path.Combine( INSTALL_PATH, VERSION_FILE_NAME ), majorMinor );
                _localVersion = majorMinor;

                AssetDatabase.Refresh();

                string msg = $"Success! Installed '{_targetBranch}'.";
                if ( backedUpCount > 0 ) msg += $"\n\n{backedUpCount} suspicious/modified files were moved to:\n{backupFolder}";

                EditorUtility.DisplayDialog( "Complete", msg, "OK" );

                if ( backedUpCount == 0 && Directory.Exists( backupFolder ) ) Directory.Delete( backupFolder );
                if ( Directory.Exists( "Assets/AR_Backup" ) && Directory.GetDirectories( "Assets/AR_Backup" ).Length == 0 ) Directory.Delete( "Assets/AR_Backup" );
                AssetDatabase.Refresh();
            }
            catch ( Exception e )
            {
                EditorUtility.DisplayDialog( "Installation Failed", e.Message, "OK" );
                Debug.LogError( $"{e}" );
            }
            finally
            {
                _isWorking = false;
                EditorUtility.ClearProgressBar();
            }
        }

        private void BackupFile( string fullPath, string relPath, string backupRoot )
        {
            string dest = Path.Combine( backupRoot, relPath );
            string destDir = Path.GetDirectoryName( dest );
            if ( !Directory.Exists( destDir ) ) Directory.CreateDirectory( destDir );
            File.Copy( fullPath, dest, true );
            File.Delete( fullPath );
            if ( File.Exists( fullPath + ".meta" ) ) File.Delete( fullPath + ".meta" );
        }

        private void CopyFiles( string sourceRoot )
        {
            string sourceAssets = Path.Combine( sourceRoot, "Assets" );
            var allFiles = Directory.GetFiles( sourceAssets, "*", SearchOption.AllDirectories );
            foreach ( var file in allFiles )
            {
                string relPath = file.Substring( sourceAssets.Length + 1 );
                string dest = Path.Combine( INSTALL_PATH, relPath );
                string destDir = Path.GetDirectoryName( dest );
                if ( !Directory.Exists( destDir ) ) Directory.CreateDirectory( destDir );
                File.Copy( file, dest, true );
            }
        }

        private static void ValidateInstallation( string version )
        {
            var window = GetWindow<ARSampleInstaller>( "AR Sample Installer" );
            if ( string.IsNullOrEmpty( version ) ) { window._detectedPackageVersion = "Not Installed"; window.Repaint(); return; }

            string majorMinor = GetMajorMinor( version );
            string versionFilePath = Path.Combine( INSTALL_PATH, VERSION_FILE_NAME );
            string localVersion = File.Exists( versionFilePath ) ? File.ReadAllText( versionFilePath ).Trim() : "None";

            window._detectedPackageVersion = version;
            window._targetBranch = GetBestBranch( majorMinor );
            window._localVersion = localVersion;
            window._isWorking = false;
            window.Repaint();
        }

        private void OnGUI()
        {
            GUILayout.Label( "AR Foundation Sample Manager", EditorStyles.boldLabel );
            EditorGUILayout.Space();

            if ( _isChecking || _isWorking ) { EditorGUILayout.HelpBox( _isWorking ? _statusMessage : "Checking Versions...", MessageType.Info ); return; }
            if ( _detectedPackageVersion == "Not Installed" ) { EditorGUILayout.HelpBox( "ARFoundation not detected.", MessageType.Error ); return; }

            EditorGUILayout.LabelField( $"ARFoundation Version: {_detectedPackageVersion}" );
            EditorGUILayout.LabelField( $"Target Branch: {_targetBranch}" );
            EditorGUILayout.LabelField( $"Installed Version: {_localVersion}" );
            EditorGUILayout.Space();

            if ( _localVersion != GetMajorMinor( _detectedPackageVersion ) )
            {
                EditorGUILayout.HelpBox( $"Update Available: '{_localVersion}' -> '{_targetBranch}'", MessageType.Warning );
                GUI.backgroundColor = Color.green;
                if ( GUILayout.Button( $"Safe Install '{_targetBranch}' Samples", GUILayout.Height( 30 ) ) ) PerformSmartInstall( _targetBranch, _localVersion );
                GUI.backgroundColor = Color.white;
            }
            else
            {
                EditorGUILayout.HelpBox( "Samples Match.", MessageType.Info );
                if ( GUILayout.Button( "Re-Install / Verify" ) ) PerformSmartInstall( _targetBranch, _localVersion );
            }
        }

        private static string GetVersionFromFileSystem()
        {
            string localPath = Path.GetFullPath( "Packages/" + ARF_PACKAGE_ID + "/package.json" );
            if ( File.Exists( localPath ) ) return ExtractVersionFromJson( localPath );
            string libPath = Path.GetFullPath( "Library/PackageCache" );
            if ( Directory.Exists( libPath ) ) { var dirs = Directory.GetDirectories( libPath, ARF_PACKAGE_ID + "@*" ); if ( dirs.Length > 0 && File.Exists( Path.Combine( dirs[ 0 ], "package.json" ) ) ) return ExtractVersionFromJson( Path.Combine( dirs[ 0 ], "package.json" ) ); }
            return null;
        }
        private static string ExtractVersionFromJson( string path ) { try { return File.ReadAllText( path ).Split( new[] { "\"version\":" }, StringSplitOptions.None )[ 1 ].Split( '"' )[ 1 ]; } catch { } return null; }
        private void InstallDependenciesFromManifest( string projectRoot )
        {
            string manifestPath = Path.Combine( projectRoot, "Packages", "manifest.json" );
            if ( !File.Exists( manifestPath ) ) return;
            string jsonContent = File.ReadAllText( manifestPath );
            foreach ( var line in jsonContent.Split( new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries ) ) { if ( line.Contains( "\"com.unity." ) && !line.Contains( ARF_PACKAGE_ID ) ) { var parts = line.Trim().Replace( "\"", "" ).Replace( ",", "" ).Split( ':' ); if ( parts.Length == 2 ) _dependencyQueue.Enqueue( $"{parts[ 0 ].Trim()}@{parts[ 1 ].Trim()}" ); } }
            if ( _dependencyQueue.Count > 0 ) { EditorApplication.update += ProgressCheck; ProcessDependencyQueue(); }
        }
        private static void ProcessDependencyQueue() { if ( _dependencyQueue.Count > 0 ) _addRequest = Client.Add( _dependencyQueue.Dequeue() ); }
        private static string GetMajorMinor( string version ) { var p = version.Split( '.' ); return p.Length >= 2 ? $"{p[ 0 ]}.{p[ 1 ]}" : "4.1"; }
        private static string GetBestBranch( string mm ) { return _versionToBranchMap.ContainsKey( mm ) ? _versionToBranchMap[ mm ] : ( mm.StartsWith( "4." ) ? "4.1" : "main" ); }
    }
}