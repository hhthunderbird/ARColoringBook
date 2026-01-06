using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
#if UNITY_2021_2_OR_NEWER
using UnityEditor.SceneManagement;
#else
using UnityEditor.Experimental.SceneManagement;
#endif

namespace Felina.ARColoringBook.Editor
{
    [InitializeOnLoad]
    public class ARProjectSetupWizard : EditorWindow
    {
        // --- CONFIGURATION ---
        private const string ARF_PACKAGE_ID = "com.unity.xr.arfoundation";
        private const string MATH_PACKAGE_ID = "com.unity.mathematics";
        private const string ARKIT_PACKAGE_ID = "com.unity.xr.arkit";
        private const string ARCORE_PACKAGE_ID = "com.unity.xr.arcore";
        private const string UNITASK_GIT_URL = "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask";

        // --- STATE VARIABLES ---
        private static ListRequest _listRequest;
        private static AddRequest _addRequest;
        private static bool _isCheckingPackages;
        private static Queue<string> _installQueue = new Queue<string>();

        // GUI State
        private Vector2 _scrollPos;
        [SerializeField] private bool _isWorking;
        [SerializeField] private string _statusMessage;

        // Analysis Data
        private string _unityVersionMajor;
        private string _recommendedArVersion;
        private bool _hasUniTask;
        private bool _hasLegacyUniTask;

        // Scene Setup Data
        private UnityEngine.Object _userReferenceLibrary;

        private class PackageStatus
        {
            public string Id;
            public string InstalledVersion;
            public bool IsInstalled => !string.IsNullOrEmpty( InstalledVersion );
            public string RecommendedVersion;
        }

        private List<PackageStatus> _packages = new List<PackageStatus>();

        // --- INITIALIZATION ---

        [MenuItem( "Felina/Project Setup Wizard" )]
        public static void ShowWindow()
        {
            var win = GetWindow<ARProjectSetupWizard>( "AR Setup" );
            win.minSize = new Vector2( 450, 500 );
            win.Show();
            win.RefreshAnalysis();
        }

        private void OnEnable()
        {
            RefreshAnalysis();
        }

        private void RefreshAnalysis()
        {
            if ( _isCheckingPackages ) return;

            _unityVersionMajor = Application.unityVersion.Split( '.' )[ 0 ];
            DetermineRecommendations();

            _hasLegacyUniTask = System.IO.Directory.Exists( "Assets/Plugins/UniTask" );

            _isCheckingPackages = true;
            _statusMessage = "Refreshing Package List...";
            _listRequest = Client.List( true );
            EditorApplication.update += ProgressPackageCheck;
        }

        private void DetermineRecommendations()
        {
            int year = int.Parse( _unityVersionMajor );
            if ( year <= 2019 ) _recommendedArVersion = "2.1.18";
            else if ( year == 2020 ) _recommendedArVersion = "4.1.13";
            else if ( year == 2021 ) _recommendedArVersion = "4.2.10";
            else if ( year == 2022 ) _recommendedArVersion = "5.1.5";
            else _recommendedArVersion = "6.0.0";
        }

        // --- SCENE GENERATION LOGIC ---

        private void CreateUniversalARScene()
        {
            try
            {
                // 1. Create New Scene
                var newScene = EditorSceneManager.NewScene( NewSceneSetup.EmptyScene, NewSceneMode.Single );
                newScene.name = "AR_ImageTracking_Auto";

                // 2. Create AR Session
                GameObject sessionGO = new GameObject( "AR Session" );
                AttachComponentSafe( sessionGO, "UnityEngine.XR.ARFoundation.ARSession" );
                AttachComponentSafe( sessionGO, "UnityEngine.XR.ARFoundation.ARInputManager" );

                // 3. Create Camera
                GameObject cameraGO = new GameObject( "Main Camera" );
                cameraGO.tag = "MainCamera";
                Camera cam = cameraGO.AddComponent<Camera>();

                // 4. Create Origin
                GameObject originGO = null;
                Type xrOriginType = GetTypeSafe( "Unity.XR.CoreUtils.XROrigin" );
                if ( xrOriginType == null ) xrOriginType = GetTypeSafe( "UnityEngine.XR.CoreUtils.XROrigin" );

                if ( xrOriginType != null )
                {
                    Debug.Log( "Creating Modern AR Setup (XROrigin)..." );
                    originGO = new GameObject( "XR Origin" );
                    var originComp = originGO.AddComponent( xrOriginType );

                    GameObject offsetGO = new GameObject( "Camera Offset" );
                    offsetGO.transform.SetParent( originGO.transform, false );
                    cameraGO.transform.SetParent( offsetGO.transform, false );

                    var camProp = xrOriginType.GetProperty( "Camera" );
                    if ( camProp != null ) camProp.SetValue( originComp, cam );

                    var offsetProp = xrOriginType.GetProperty( "CameraFloorOffsetObject" );
                    if ( offsetProp != null ) offsetProp.SetValue( originComp, offsetGO );
                }
                else
                {
                    Debug.Log( "Creating Legacy AR Setup (ARSessionOrigin)..." );
                    originGO = new GameObject( "AR Session Origin" );
                    AttachComponentSafe( originGO, "UnityEngine.XR.ARFoundation.ARSessionOrigin" );
                    cameraGO.transform.SetParent( originGO.transform, false );
                }

                // 5. Setup Camera Components
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 20f;

                AttachComponentSafe( cameraGO, "UnityEngine.XR.ARFoundation.ARCameraManager" );
                AttachComponentSafe( cameraGO, "UnityEngine.XR.ARFoundation.ARCameraBackground" );
                AttachComponentSafe( cameraGO, "UnityEngine.SpatialTracking.TrackedPoseDriver" );

                // 6. Add Image Tracking & Spawner
                if ( originGO != null )
                {
                    var tracker = AttachComponentSafe( originGO, "UnityEngine.XR.ARFoundation.ARTrackedImageManager" );
                    AttachComponentSafe( originGO, "ARContentSpawner" );

                    if ( tracker != null )
                    {
                        if ( _userReferenceLibrary != null )
                        {
                            var libProp = tracker.GetType().GetProperty( "referenceLibrary" );
                            if ( libProp != null ) libProp.SetValue( tracker, _userReferenceLibrary );

                            var maxProp = tracker.GetType().GetProperty( "maxNumberOfMovingImages" );
                            if ( maxProp != null ) maxProp.SetValue( tracker, 4 );
                        }
                    }
                }

                // 7. Create Light
                GameObject lightGO = new GameObject( "Directional Light" );
                Light light = lightGO.AddComponent<Light>();
                light.type = LightType.Directional;
                lightGO.transform.rotation = Quaternion.Euler( 50, -30, 0 );

                // 8. Create Managers
                GameObject managersGO = new GameObject( "AR Managers" );
                AttachComponentSafe( managersGO, "ARScannerManager" );
                AttachComponentSafe( managersGO, "ARFoundationBridge" );

                EditorUtility.DisplayDialog( "Success", "AR Scene generated successfully!", "OK" );
            }
            catch ( Exception e )
            {
                Debug.LogError( $"Error creating scene: {e}" );
                EditorUtility.DisplayDialog( "Error", "Failed to generate scene. Check console.", "OK" );
                GUIUtility.ExitGUI();
            }
        }

        private Component AttachComponentSafe( GameObject go, string typeName )
        {
            Type t = GetTypeSafe( typeName );
            if ( t != null ) return go.AddComponent( t );
            Debug.LogWarning( $"Could not find type '{typeName}'. Package might be missing." );
            return null;
        }

        private Type GetTypeSafe( string typeName )
        {
            foreach ( var assembly in AppDomain.CurrentDomain.GetAssemblies() )
            {
                var type = assembly.GetType( typeName );
                if ( type != null ) return type;
            }
            if ( !typeName.Contains( "." ) )
            {
                foreach ( var assembly in AppDomain.CurrentDomain.GetAssemblies() )
                {
                    if ( assembly.FullName.StartsWith( "System" ) || assembly.FullName.StartsWith( "Unity" ) ) continue;
                    var type = assembly.GetTypes().FirstOrDefault( t => t.Name == typeName );
                    if ( type != null ) return type;
                }
            }
            return null;
        }

        // --- PACKAGE MANAGER LOGIC ---

        private void ProgressPackageCheck()
        {
            if ( _listRequest != null )
            {
                if ( !_listRequest.IsCompleted ) return;
                if ( _listRequest.Status == StatusCode.Success ) ParsePackageList( _listRequest.Result );
                else Debug.LogError( $"Package Check Failed: {_listRequest.Error.message}" );

                _listRequest = null;
                if ( _installQueue.Count > 0 ) { ProcessInstallQueue(); return; }
                _isCheckingPackages = false;
                _statusMessage = "";
                EditorApplication.update -= ProgressPackageCheck;
                Repaint();
                return;
            }

            if ( _addRequest != null )
            {
                if ( !_addRequest.IsCompleted ) return;
                if ( _addRequest.Status == StatusCode.Failure ) Debug.LogError( $"Failed: {_addRequest.Error.message}" );
                _addRequest = null;
                ProcessInstallQueue();
            }
        }

        private void ParsePackageList( PackageCollection packages )
        {
            _packages.Clear();
            string GetVer( string id ) => packages.FirstOrDefault( p => p.name == id )?.version;

            _packages.Add( new PackageStatus { Id = ARF_PACKAGE_ID, InstalledVersion = GetVer( ARF_PACKAGE_ID ), RecommendedVersion = _recommendedArVersion } );
            _packages.Add( new PackageStatus { Id = MATH_PACKAGE_ID, InstalledVersion = GetVer( MATH_PACKAGE_ID ), RecommendedVersion = "1.2.6" } );

            string platformRec = _recommendedArVersion.StartsWith( "2" ) ? "2.1.18" :
                                 _recommendedArVersion.StartsWith( "4" ) ? "4.1.13" : _recommendedArVersion;

            _packages.Add( new PackageStatus { Id = ARKIT_PACKAGE_ID, InstalledVersion = GetVer( ARKIT_PACKAGE_ID ), RecommendedVersion = platformRec } );
            _packages.Add( new PackageStatus { Id = ARCORE_PACKAGE_ID, InstalledVersion = GetVer( ARCORE_PACKAGE_ID ), RecommendedVersion = platformRec } );

            _hasUniTask = GetTypeSafe( "Cysharp.Threading.Tasks.UniTask" ) != null;
        }

        private void QueueMissingPackages()
        {
            _installQueue.Clear();
            foreach ( var pkg in _packages )
            {
                if ( !pkg.IsInstalled ) _installQueue.Enqueue( $"{pkg.Id}@{pkg.RecommendedVersion}" );
            }
            if ( !_hasUniTask ) _installQueue.Enqueue( UNITASK_GIT_URL );

            if ( _installQueue.Count > 0 )
            {
                _isWorking = true;
                _statusMessage = "Installing Packages...";
                _isCheckingPackages = true;
                EditorApplication.update += ProgressPackageCheck;
                ProcessInstallQueue();
            }
        }

        private void ProcessInstallQueue()
        {
            if ( _installQueue.Count > 0 )
            {
                string nextPkg = _installQueue.Dequeue();
                _statusMessage = $"Installing {nextPkg}...";
                _addRequest = Client.Add( nextPkg );
            }
            else
            {
                _addRequest = null;
                _statusMessage = "Verifying installation...";
                _listRequest = Client.List( true );
            }
        }

        // --- GUI RENDER ---

        private void OnGUI()
        {
            GUILayout.Label( "AR Project Setup Wizard", EditorStyles.boldLabel );

            if ( _isCheckingPackages || _isWorking )
            {
                EditorGUILayout.HelpBox( _statusMessage, MessageType.Info );
                Repaint();
                return;
            }

            _scrollPos = EditorGUILayout.BeginScrollView( _scrollPos );

            DrawEnvironmentSection();
            DrawDependenciesSection();
            DrawSceneSetupSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawEnvironmentSection()
        {
            EditorGUILayout.LabelField( "Environment Analysis", EditorStyles.boldLabel );
            using ( new EditorGUILayout.VerticalScope( EditorStyles.helpBox ) )
            {
                EditorGUILayout.LabelField( $"Unity Version: {Application.unityVersion}" );
                EditorGUILayout.LabelField( $"Target AR Version: {_recommendedArVersion}" );
                EditorGUILayout.LabelField( $"Active Platform: {EditorUserBuildSettings.activeBuildTarget}" );
            }
            EditorGUILayout.Space();
        }

        private void DrawDependenciesSection()
        {
            EditorGUILayout.LabelField( "Core Dependencies", EditorStyles.boldLabel );
            using ( new EditorGUILayout.VerticalScope( EditorStyles.helpBox ) )
            {
                bool allGood = true;
                GUIContent GetStatusIcon( bool ok ) { return ok ? EditorGUIUtility.IconContent( "testpassed" ) : EditorGUIUtility.IconContent( "console.erroricon" ); }

                foreach ( var pkg in _packages )
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label( GetStatusIcon( pkg.IsInstalled ), GUILayout.Width( 20 ) );
                    GUILayout.Label( pkg.Id.Replace( "com.unity.", "" ), GUILayout.Width( 150 ) );
                    if ( pkg.IsInstalled ) GUILayout.Label( $"v{pkg.InstalledVersion}", EditorStyles.miniLabel );
                    else { GUILayout.Label( "MISSING", EditorStyles.miniLabel ); allGood = false; }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label( GetStatusIcon( _hasUniTask ), GUILayout.Width( 20 ) );
                GUILayout.Label( "UniTask", GUILayout.Width( 150 ) );
                GUILayout.Label( _hasUniTask ? "Installed" : "Missing", EditorStyles.miniLabel );
                EditorGUILayout.EndHorizontal();

                if ( _hasLegacyUniTask )
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.HelpBox( "Warning: Legacy 'Assets/Plugins/UniTask' folder found. Delete it to use Package Manager version.", MessageType.Warning );
                }

                if ( !allGood || !_hasUniTask )
                {
                    EditorGUILayout.Space();
                    GUI.backgroundColor = Color.green;
                    if ( GUILayout.Button( "Install Missing Core Packages", GUILayout.Height( 30 ) ) ) QueueMissingPackages();
                    GUI.backgroundColor = Color.white;
                }
            }
            EditorGUILayout.Space();
        }

        private void DrawSceneSetupSection()
        {
            EditorGUILayout.LabelField( "Scene Auto-Setup", EditorStyles.boldLabel );

            var arfPkg = _packages.FirstOrDefault( p => p.Id == ARF_PACKAGE_ID );
            bool canCreate = arfPkg != null && arfPkg.IsInstalled;

            using ( new EditorGUILayout.VerticalScope( EditorStyles.helpBox ) )
            {
                if ( canCreate )
                {
                    EditorGUILayout.LabelField( "Generates a complete AR Scene for this project." );
                    _userReferenceLibrary = EditorGUILayout.ObjectField( "Reference Library (Optional)", _userReferenceLibrary, typeof( ScriptableObject ), false );

                    EditorGUILayout.Space();
                    if ( GUILayout.Button( "Create AR Scene", GUILayout.Height( 30 ) ) )
                    {
                        CreateUniversalARScene();
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox( "Install AR Foundation first.", MessageType.Warning );
                }
            }
        }
    }
}


//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Reflection;
//using UnityEditor;
//using UnityEditor.PackageManager;
//using UnityEditor.PackageManager.Requests;
//using UnityEngine;
//using UnityEngine.Networking;
//#if UNITY_2021_2_OR_NEWER
//using UnityEditor.SceneManagement;
//#else
//using UnityEditor.Experimental.SceneManagement;
//#endif

//namespace Felina.ARColoringBook.Editor
//{
//    [InitializeOnLoad]
//    public class ARProjectSetupWizard : EditorWindow
//    {
//        // --- CONFIGURATION ---
//        private const string ARF_PACKAGE_ID = "com.unity.xr.arfoundation";
//        private const string MATH_PACKAGE_ID = "com.unity.mathematics";
//        private const string ARKIT_PACKAGE_ID = "com.unity.xr.arkit";
//        private const string ARCORE_PACKAGE_ID = "com.unity.xr.arcore";
//        private const string UNITASK_GIT_URL = "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask";

//        private const string REPO_URL_BASE = "https://github.com/Unity-Technologies/arfoundation-samples/archive";
//        private const string INSTALL_PATH = "Assets";
//        private const string VERSION_FILE_NAME = "ar_samples_version.txt";

//        // Sample Cleanup Config
//        private const string SAMPLES_ROOT = "Assets/Samples";
//        private static readonly string[] AR_SAMPLE_PATTERNS = new[] { "AR Foundation", "ARFoundation", "XR AR Foundation" };

//        // --- STATE VARIABLES ---
//        private static ListRequest _listRequest;
//        private static AddRequest _addRequest;
//        private static bool _isCheckingPackages;
//        private static Queue<string> _installQueue = new Queue<string>();

//        // Download State
//        private static UnityWebRequest _currentDownload;
//        private static string _tempZipPath;
//        private static string _pendingNewBranch;
//        private static bool _isDownloadingOld;

//        // GUI State
//        private Vector2 _scrollPos;
//        [SerializeField] private bool _isWorking;
//        [SerializeField] private string _statusMessage;

//        // Analysis Data
//        private string _unityVersionMajor;
//        private string _recommendedArVersion;
//        private bool _hasUniTask;
//        private bool _hasLegacyUniTask;

//        // Scene Setup Data
//        private UnityEngine.Object _userReferenceLibrary;

//        // Sample Cleanup Data
//        private List<SampleInfo> _incompatibleSamples = new List<SampleInfo>();

//        private class PackageStatus
//        {
//            public string Id;
//            public string InstalledVersion;
//            public bool IsInstalled => !string.IsNullOrEmpty( InstalledVersion );
//            public string RecommendedVersion;
//        }

//        private class SampleInfo { public string Path; public string Version; }
//        private List<PackageStatus> _packages = new List<PackageStatus>();

//        private static readonly Dictionary<string, string> _versionToBranchMap = new Dictionary<string, string>
//        {
//            { "2.1", "2.1" }, { "3.0", "3.0" }, { "3.1", "3.1" },
//            { "4.0", "4.0" }, { "4.1", "4.1" }, { "4.2", "4.2" },
//            { "5.0", "main" }, { "5.1", "main" }, { "6.0", "main" }
//        };

//        // --- INITIALIZATION ---

//        [MenuItem( "Project Setup/Setup Wizard" )]
//        public static void ShowWindow()
//        {
//            var win = GetWindow<ARProjectSetupWizard>( "AR Setup" );
//            win.minSize = new Vector2( 450, 700 );
//            win.Show();
//            win.RefreshAnalysis();
//        }

//        private void OnEnable()
//        {
//            RefreshAnalysis();
//        }

//        private void RefreshAnalysis()
//        {
//            if ( _isCheckingPackages ) return;

//            _unityVersionMajor = Application.unityVersion.Split( '.' )[ 0 ];
//            DetermineRecommendations();

//            _hasLegacyUniTask = Directory.Exists( "Assets/Plugins/UniTask" );
//            CheckForIncompatibleSamples();

//            _isCheckingPackages = true;
//            _statusMessage = "Refreshing Package List...";
//            _listRequest = Client.List( true );
//            EditorApplication.update += ProgressPackageCheck;
//        }

//        private void DetermineRecommendations()
//        {
//            int year = int.Parse( _unityVersionMajor );
//            if ( year <= 2019 ) _recommendedArVersion = "2.1.18";
//            else if ( year == 2020 ) _recommendedArVersion = "4.1.13";
//            else if ( year == 2021 ) _recommendedArVersion = "4.2.10";
//            else if ( year == 2022 ) _recommendedArVersion = "5.1.5";
//            else _recommendedArVersion = "6.0.0";
//        }

//        // --- SCENE GENERATION LOGIC ---

//        private void CreateUniversalARScene()
//        {
//            try
//            {
//                // 1. Create New Scene
//                var newScene = EditorSceneManager.NewScene( NewSceneSetup.EmptyScene, NewSceneMode.Single );
//                newScene.name = "AR_ImageTracking_Auto";

//                // 2. Create AR Session (Common to all versions)
//                GameObject sessionGO = new GameObject( "AR Session" );
//                AttachComponentSafe( sessionGO, "UnityEngine.XR.ARFoundation.ARSession" );
//                AttachComponentSafe( sessionGO, "UnityEngine.XR.ARFoundation.ARInputManager" );

//                // 3. Create Camera (FIX: Add directly to avoid missing component error)
//                GameObject cameraGO = new GameObject( "Main Camera" );
//                cameraGO.tag = "MainCamera";
//                Camera cam = cameraGO.AddComponent<Camera>();

//                // 4. Create Origin (Handles Version Differences)
//                GameObject originGO = null;

//                // Try Modern (ARF 5+ / Unity 2022+) - XROrigin
//                Type xrOriginType = GetTypeSafe( "Unity.XR.CoreUtils.XROrigin" );
//                if ( xrOriginType == null ) xrOriginType = GetTypeSafe( "UnityEngine.XR.CoreUtils.XROrigin" );

//                if ( xrOriginType != null )
//                {
//                    Debug.Log( "Creating Modern AR Setup (XROrigin)..." );
//                    originGO = new GameObject( "XR Origin" );
//                    var originComp = originGO.AddComponent( xrOriginType );

//                    GameObject offsetGO = new GameObject( "Camera Offset" );
//                    offsetGO.transform.SetParent( originGO.transform, false );
//                    cameraGO.transform.SetParent( offsetGO.transform, false );

//                    // Link properties via Reflection
//                    var camProp = xrOriginType.GetProperty( "Camera" );
//                    if ( camProp != null ) camProp.SetValue( originComp, cam );

//                    var offsetProp = xrOriginType.GetProperty( "CameraFloorOffsetObject" );
//                    if ( offsetProp != null ) offsetProp.SetValue( originComp, offsetGO );
//                }
//                else
//                {
//                    // Fallback to Legacy (ARF 4 / Unity 2020/2021) - ARSessionOrigin
//                    Debug.Log( "Creating Legacy AR Setup (ARSessionOrigin)..." );
//                    originGO = new GameObject( "AR Session Origin" );
//                    AttachComponentSafe( originGO, "UnityEngine.XR.ARFoundation.ARSessionOrigin" );

//                    cameraGO.transform.SetParent( originGO.transform, false );
//                    // Legacy origin usually finds camera automatically in children
//                }

//                // 5. Setup Camera Components
//                cam.clearFlags = CameraClearFlags.SolidColor;
//                cam.backgroundColor = Color.black;
//                cam.nearClipPlane = 0.1f;
//                cam.farClipPlane = 20f;

//                AttachComponentSafe( cameraGO, "UnityEngine.XR.ARFoundation.ARCameraManager" );
//                AttachComponentSafe( cameraGO, "UnityEngine.XR.ARFoundation.ARCameraBackground" );
//                AttachComponentSafe( cameraGO, "UnityEngine.SpatialTracking.TrackedPoseDriver" );

//                // 6. Add Image Tracking & Spawner
//                if ( originGO != null )
//                {
//                    var tracker = AttachComponentSafe( originGO, "UnityEngine.XR.ARFoundation.ARTrackedImageManager" );

//                    // Add ARContentSpawner exactly where the Tracker is
//                    AttachComponentSafe( originGO, "ARContentSpawner" );

//                    if ( tracker != null )
//                    {
//                        if ( _userReferenceLibrary != null )
//                        {
//                            var libProp = tracker.GetType().GetProperty( "referenceLibrary" );
//                            if ( libProp != null ) libProp.SetValue( tracker, _userReferenceLibrary );

//                            var maxProp = tracker.GetType().GetProperty( "maxNumberOfMovingImages" );
//                            if ( maxProp != null ) maxProp.SetValue( tracker, 4 );
//                        }
//                    }
//                }

//                // 7. Create Light
//                GameObject lightGO = new GameObject( "Directional Light" );
//                Light light = lightGO.AddComponent<Light>();
//                light.type = LightType.Directional;
//                lightGO.transform.rotation = Quaternion.Euler( 50, -30, 0 );

//                // 8. Create Managers (Professional Separation)
//                GameObject managersGO = new GameObject( "AR Managers" );
//                AttachComponentSafe( managersGO, "ARScannerManager" );
//                AttachComponentSafe( managersGO, "ARFoundationBridge" );

//                EditorUtility.DisplayDialog( "Scene Created", "AR Image Tracking Scene generated successfully!", "OK" );
//            }
//            catch ( Exception e )
//            {
//                Debug.LogError( $"Error creating scene: {e}" );
//                EditorUtility.DisplayDialog( "Error", "Failed to generate scene. Check console for details.", "OK" );
//                GUIUtility.ExitGUI(); // Prevents layout errors
//            }
//        }

//        // Improved Helper: Finds types by exact name OR simple name (if namespace is unknown)
//        private Component AttachComponentSafe( GameObject go, string typeName )
//        {
//            Type t = GetTypeSafe( typeName );
//            if ( t != null ) return go.AddComponent( t );
//            Debug.LogWarning( $"Could not find type '{typeName}'. Is the script/package in the project?" );
//            return null;
//        }

//        private Type GetTypeSafe( string typeName )
//        {
//            // 1. Try finding by full qualified name first (fastest)
//            foreach ( var assembly in AppDomain.CurrentDomain.GetAssemblies() )
//            {
//                var type = assembly.GetType( typeName );
//                if ( type != null ) return type;
//            }

//            // 2. If simple name (no dots), search all types (slower, but finds your custom scripts)
//            if ( !typeName.Contains( "." ) )
//            {
//                foreach ( var assembly in AppDomain.CurrentDomain.GetAssemblies() )
//                {
//                    // Optimization: Skip system assemblies
//                    if ( assembly.FullName.StartsWith( "System" ) || assembly.FullName.StartsWith( "Unity" ) ) continue;

//                    var type = assembly.GetTypes().FirstOrDefault( t => t.Name == typeName );
//                    if ( type != null ) return type;
//                }
//            }
//            return null;
//        }

//        // --- SAMPLE CLEANUP LOGIC ---

//        private void CheckForIncompatibleSamples()
//        {
//            _incompatibleSamples.Clear();
//            if ( !Directory.Exists( SAMPLES_ROOT ) ) return;

//            var allSampleDirs = Directory.GetDirectories( SAMPLES_ROOT, "*", SearchOption.AllDirectories );
//            var currentUnityVer = ParseUnityVersion( Application.unityVersion );

//            foreach ( var pattern in AR_SAMPLE_PATTERNS )
//            {
//                var arSampleDirs = allSampleDirs.Where( dir => Path.GetFileName( dir ).Contains( pattern, StringComparison.OrdinalIgnoreCase ) ).ToArray();
//                foreach ( var dir in arSampleDirs )
//                {
//                    string ver = GetVersionFromPath( dir );
//                    if ( !string.IsNullOrEmpty( ver ) && !IsVersionCompatible( ver, currentUnityVer ) )
//                    {
//                        _incompatibleSamples.Add( new SampleInfo { Path = dir, Version = ver } );
//                    }
//                }
//            }
//        }

//        private string GetVersionFromPath( string path )
//        {
//            var parts = path.Split( Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar );
//            for ( int i = 0; i < parts.Length; i++ )
//            {
//                if ( AR_SAMPLE_PATTERNS.Any( p => parts[ i ].Contains( p, StringComparison.OrdinalIgnoreCase ) ) )
//                {
//                    if ( i + 1 < parts.Length ) return parts[ i + 1 ];
//                }
//            }
//            return null;
//        }

//        private bool IsVersionCompatible( string arVersion, UnityVersion unityVersion )
//        {
//            var parts = arVersion.Split( '.' );
//            if ( parts.Length == 0 || !int.TryParse( parts[ 0 ], out int arMajor ) ) return true;

//            if ( unityVersion.Major == 2019 || unityVersion.Major == 2020 ) return arMajor >= 2 && arMajor <= 4;
//            if ( unityVersion.Major == 2021 || unityVersion.Major == 2022 ) return arMajor >= 4 && arMajor <= 5;
//            if ( unityVersion.Major >= 2023 || unityVersion.Major >= 6000 ) return arMajor >= 6;
//            return true;
//        }

//        private void RemoveIncompatibleSamples()
//        {
//            AssetDatabase.StartAssetEditing();
//            try
//            {
//                foreach ( var sample in _incompatibleSamples )
//                {
//                    if ( Directory.Exists( sample.Path ) )
//                    {
//                        FileUtil.DeleteFileOrDirectory( sample.Path );
//                        FileUtil.DeleteFileOrDirectory( sample.Path + ".meta" );
//                    }
//                }
//            }
//            finally
//            {
//                AssetDatabase.StopAssetEditing();
//                AssetDatabase.Refresh();
//                CheckForIncompatibleSamples();
//            }
//        }

//        private struct UnityVersion { public int Major; }
//        private UnityVersion ParseUnityVersion( string v ) { return new UnityVersion { Major = int.Parse( v.Split( '.' )[ 0 ] ) }; }

//        // --- PACKAGE MANAGER LOGIC ---

//        private void ProgressPackageCheck()
//        {
//            if ( _listRequest != null )
//            {
//                if ( !_listRequest.IsCompleted ) return;

//                if ( _listRequest.Status == StatusCode.Success )
//                    ParsePackageList( _listRequest.Result );
//                else
//                    Debug.LogError( $"Package Check Failed: {_listRequest.Error.message}" );

//                _listRequest = null;
//                if ( _installQueue.Count > 0 ) { ProcessInstallQueue(); return; }
//                _isCheckingPackages = false;
//                _statusMessage = "";
//                EditorApplication.update -= ProgressPackageCheck;
//                Repaint();
//                return;
//            }

//            if ( _addRequest != null )
//            {
//                if ( !_addRequest.IsCompleted ) return;
//                if ( _addRequest.Status == StatusCode.Failure ) Debug.LogError( $"Failed: {_addRequest.Error.message}" );
//                _addRequest = null;
//                ProcessInstallQueue();
//            }
//        }

//        private void ParsePackageList( PackageCollection packages )
//        {
//            _packages.Clear();
//            string GetVer( string id ) => packages.FirstOrDefault( p => p.name == id )?.version;

//            _packages.Add( new PackageStatus { Id = ARF_PACKAGE_ID, InstalledVersion = GetVer( ARF_PACKAGE_ID ), RecommendedVersion = _recommendedArVersion } );
//            _packages.Add( new PackageStatus { Id = MATH_PACKAGE_ID, InstalledVersion = GetVer( MATH_PACKAGE_ID ), RecommendedVersion = "1.2.6" } );

//            string platformRec = _recommendedArVersion.StartsWith( "2" ) ? "2.1.18" :
//                                 _recommendedArVersion.StartsWith( "4" ) ? "4.1.13" : _recommendedArVersion;

//            _packages.Add( new PackageStatus { Id = ARKIT_PACKAGE_ID, InstalledVersion = GetVer( ARKIT_PACKAGE_ID ), RecommendedVersion = platformRec } );
//            _packages.Add( new PackageStatus { Id = ARCORE_PACKAGE_ID, InstalledVersion = GetVer( ARCORE_PACKAGE_ID ), RecommendedVersion = platformRec } );

//            // FIX: Use GetTypeSafe instead of Type.GetType to search in all Assemblies (Plugins/UniTask)
//            _hasUniTask = GetTypeSafe( "Cysharp.Threading.Tasks.UniTask" ) != null;
//        }

//        private void QueueMissingPackages()
//        {
//            _installQueue.Clear();
//            foreach ( var pkg in _packages )
//            {
//                if ( !pkg.IsInstalled ) _installQueue.Enqueue( $"{pkg.Id}@{pkg.RecommendedVersion}" );
//            }
//            if ( !_hasUniTask ) _installQueue.Enqueue( UNITASK_GIT_URL );

//            if ( _installQueue.Count > 0 )
//            {
//                _isWorking = true;
//                _statusMessage = "Installing Packages...";
//                _isCheckingPackages = true;
//                EditorApplication.update += ProgressPackageCheck;
//                ProcessInstallQueue();
//            }
//        }

//        private void ProcessInstallQueue()
//        {
//            if ( _installQueue.Count > 0 )
//            {
//                string nextPkg = _installQueue.Dequeue();
//                _statusMessage = $"Installing {nextPkg}...";
//                _addRequest = Client.Add( nextPkg );
//            }
//            else
//            {
//                _addRequest = null;
//                _statusMessage = "Verifying installation...";
//                _listRequest = Client.List( true );
//            }
//        }

//        // --- GUI RENDER ---

//        private void OnGUI()
//        {
//            GUILayout.Label( "AR Project Setup Wizard", EditorStyles.boldLabel );

//            if ( _isCheckingPackages || _isWorking )
//            {
//                EditorGUILayout.HelpBox( _statusMessage, MessageType.Info );
//                Repaint();
//                return;
//            }

//            _scrollPos = EditorGUILayout.BeginScrollView( _scrollPos );

//            DrawEnvironmentSection();
//            DrawDependenciesSection();
//            DrawSceneSetupSection();
//            DrawSamplesSection();

//            EditorGUILayout.EndScrollView();
//        }

//        private void DrawEnvironmentSection()
//        {
//            EditorGUILayout.LabelField( "Environment Analysis", EditorStyles.boldLabel );
//            using ( new EditorGUILayout.VerticalScope( EditorStyles.helpBox ) )
//            {
//                EditorGUILayout.LabelField( $"Unity Version: {Application.unityVersion}" );
//                EditorGUILayout.LabelField( $"Target AR Version: {_recommendedArVersion}" );
//                EditorGUILayout.LabelField( $"Active Platform: {EditorUserBuildSettings.activeBuildTarget}" );
//            }
//            EditorGUILayout.Space();
//        }

//        private void DrawDependenciesSection()
//        {
//            EditorGUILayout.LabelField( "Core Dependencies", EditorStyles.boldLabel );
//            using ( new EditorGUILayout.VerticalScope( EditorStyles.helpBox ) )
//            {
//                bool allGood = true;
//                GUIContent GetStatusIcon( bool ok ) { return ok ? EditorGUIUtility.IconContent( "testpassed" ) : EditorGUIUtility.IconContent( "console.erroricon" ); }

//                foreach ( var pkg in _packages )
//                {
//                    EditorGUILayout.BeginHorizontal();
//                    GUILayout.Label( GetStatusIcon( pkg.IsInstalled ), GUILayout.Width( 20 ) );
//                    GUILayout.Label( pkg.Id.Replace( "com.unity.", "" ), GUILayout.Width( 150 ) );
//                    if ( pkg.IsInstalled ) GUILayout.Label( $"v{pkg.InstalledVersion}", EditorStyles.miniLabel );
//                    else { GUILayout.Label( "MISSING", EditorStyles.miniLabel ); allGood = false; }
//                    EditorGUILayout.EndHorizontal();
//                }

//                EditorGUILayout.BeginHorizontal();
//                GUILayout.Label( GetStatusIcon( _hasUniTask ), GUILayout.Width( 20 ) );
//                GUILayout.Label( "UniTask", GUILayout.Width( 150 ) );
//                GUILayout.Label( _hasUniTask ? "Installed" : "Missing", EditorStyles.miniLabel );
//                EditorGUILayout.EndHorizontal();

//                if ( _hasLegacyUniTask )
//                {
//                    EditorGUILayout.Space();
//                    EditorGUILayout.HelpBox( "Manual Action Required: 'Assets/Plugins/UniTask' folder detected.\nPlease delete it manually to use the Package Manager version.", MessageType.Error );
//                }

//                if ( !allGood || !_hasUniTask )
//                {
//                    EditorGUILayout.Space();
//                    GUI.backgroundColor = Color.green;
//                    if ( GUILayout.Button( "Install Missing Core Packages", GUILayout.Height( 30 ) ) ) QueueMissingPackages();
//                    GUI.backgroundColor = Color.white;
//                }
//            }
//            EditorGUILayout.Space();
//        }

//        // --- SCENE SETUP GUI ---
//        private void DrawSceneSetupSection()
//        {
//            EditorGUILayout.LabelField( "Scene Auto-Setup", EditorStyles.boldLabel );

//            var arfPkg = _packages.FirstOrDefault( p => p.Id == ARF_PACKAGE_ID );
//            bool canCreate = arfPkg != null && arfPkg.IsInstalled;

//            using ( new EditorGUILayout.VerticalScope( EditorStyles.helpBox ) )
//            {
//                if ( canCreate )
//                {
//                    EditorGUILayout.LabelField( "Generates a scene compatible with your AR version." );
//                    _userReferenceLibrary = EditorGUILayout.ObjectField( "Reference Library (Optional)", _userReferenceLibrary, typeof( ScriptableObject ), false );

//                    EditorGUILayout.Space();
//                    if ( GUILayout.Button( "Create Auto-Configured AR Scene", GUILayout.Height( 30 ) ) )
//                    {
//                        CreateUniversalARScene();
//                    }
//                }
//                else
//                {
//                    EditorGUILayout.HelpBox( "Install AR Foundation to generate scenes.", MessageType.Warning );
//                }
//            }
//            EditorGUILayout.Space();
//        }

//        private void DrawSamplesSection()
//        {
//            EditorGUILayout.LabelField( "AR Foundation Samples", EditorStyles.boldLabel );

//            if ( _incompatibleSamples.Count > 0 )
//            {
//                GUI.backgroundColor = new Color( 1f, 0.6f, 0.6f );
//                using ( new EditorGUILayout.VerticalScope( EditorStyles.helpBox ) )
//                {
//                    EditorGUILayout.LabelField( $"Found {_incompatibleSamples.Count} incompatible/old sample folders.", EditorStyles.boldLabel );
//                    if ( GUILayout.Button( "Clean Up Incompatible Samples" ) ) RemoveIncompatibleSamples();
//                }
//                GUI.backgroundColor = Color.white;
//                EditorGUILayout.Space();
//            }

//            var arfPkg = _packages.FirstOrDefault( p => p.Id == ARF_PACKAGE_ID );
//            if ( arfPkg == null || !arfPkg.IsInstalled )
//            {
//                EditorGUILayout.HelpBox( "Install AR Foundation first to manage samples.", MessageType.Warning );
//                return;
//            }

//            string installedSamplesVer = "None";
//            string versionFile = Path.Combine( INSTALL_PATH, VERSION_FILE_NAME );
//            if ( File.Exists( versionFile ) ) installedSamplesVer = File.ReadAllText( versionFile );

//            string majorMinor = GetMajorMinor( arfPkg.InstalledVersion );
//            string targetBranch = GetBestBranch( majorMinor );

//            using ( new EditorGUILayout.VerticalScope( EditorStyles.helpBox ) )
//            {
//                EditorGUILayout.LabelField( $"Installed Github Samples: {installedSamplesVer}" );
//                EditorGUILayout.LabelField( $"Available for v{majorMinor}: Branch '{targetBranch}'" );
//                if ( GUILayout.Button( "Download & Install Samples" ) ) PerformSmartInstall( targetBranch, installedSamplesVer );
//            }
//        }

//        // --- SAMPLE INSTALLATION HELPERS ---

//        private static string GetMajorMinor( string version ) { var p = version.Split( '.' ); return p.Length >= 2 ? $"{p[ 0 ]}.{p[ 1 ]}" : "4.1"; }
//        private static string GetBestBranch( string mm ) { return _versionToBranchMap.ContainsKey( mm ) ? _versionToBranchMap[ mm ] : ( mm.StartsWith( "4." ) ? "4.1" : "main" ); }

//        private void PerformSmartInstall( string newBranch, string oldBranch )
//        {
//            _isWorking = true;
//            _statusMessage = "Starting Download...";
//            _pendingNewBranch = newBranch;
//            StartAsyncDownload( newBranch, false );
//        }

//        private static void StartAsyncDownload( string branch, bool isOldVersion )
//        {
//            string url = $"{REPO_URL_BASE}/{branch}.zip";
//            _tempZipPath = FileUtil.GetUniqueTempPathInProject() + ".zip";
//            _isDownloadingOld = isOldVersion;
//            _currentDownload = UnityWebRequest.Get( url );
//            _currentDownload.SendWebRequest();
//            EditorApplication.update += ProgressDownload;
//        }

//        private static void ProgressDownload()
//        {
//            if ( _currentDownload == null ) return;
//            if ( !_currentDownload.isDone )
//            {
//                EditorUtility.DisplayProgressBar( "Downloading", $"Downloading... {( _currentDownload.downloadProgress * 100 ):F0}%", _currentDownload.downloadProgress );
//                return;
//            }
//            EditorUtility.ClearProgressBar();
//            EditorApplication.update -= ProgressDownload;
//            FinishAsyncDownload();
//        }

//        private static void FinishAsyncDownload()
//        {
//            if ( _currentDownload.result != UnityWebRequest.Result.Success )
//            {
//                Debug.LogError( $"Download Failed: {_currentDownload.error}" );
//                GetWindow<ARProjectSetupWizard>()._isWorking = false;
//                _currentDownload.Dispose(); _currentDownload = null; return;
//            }

//            File.WriteAllBytes( _tempZipPath, _currentDownload.downloadHandler.data );
//            _currentDownload.Dispose(); _currentDownload = null;

//            string extractPath = "Temp/Temp_NewInstall";
//            try
//            {
//                if ( Directory.Exists( extractPath ) ) Directory.Delete( extractPath, true );
//                Directory.CreateDirectory( extractPath );
//                System.IO.Compression.ZipFile.ExtractToDirectory( _tempZipPath, extractPath );
//            }
//            catch ( Exception e ) { Debug.LogError( $"Extraction Error: {e.Message}" ); GetWindow<ARProjectSetupWizard>()._isWorking = false; return; }
//            finally { if ( File.Exists( _tempZipPath ) ) File.Delete( _tempZipPath ); }

//            GetWindow<ARProjectSetupWizard>().CompleteSmartInstall( extractPath, null );
//        }

//        public void CompleteSmartInstall( string newExtractPath, string oldExtractPath )
//        {
//            try
//            {
//                string newRefPath = Directory.GetDirectories( newExtractPath )[ 0 ];
//                _statusMessage = "Installing files...";
//                CopyFiles( newRefPath );

//                string majorMinor = GetMajorMinor( _packages.First( p => p.Id == ARF_PACKAGE_ID ).InstalledVersion );
//                File.WriteAllText( Path.Combine( INSTALL_PATH, VERSION_FILE_NAME ), majorMinor );

//                AssetDatabase.Refresh();
//                EditorUtility.DisplayDialog( "Success", "AR Samples Installed successfully.", "OK" );
//            }
//            catch ( Exception e ) { Debug.LogError( e ); }
//            finally
//            {
//                _isWorking = false; _statusMessage = "";
//                if ( Directory.Exists( "Temp/Temp_NewInstall" ) ) Directory.Delete( "Temp/Temp_NewInstall", true );
//                RefreshAnalysis();
//            }
//        }

//        private void CopyFiles( string sourceRoot )
//        {
//            string sourceAssets = Path.Combine( sourceRoot, "Assets" );
//            if ( !Directory.Exists( sourceAssets ) ) return;
//            foreach ( var file in Directory.GetFiles( sourceAssets, "*", SearchOption.AllDirectories ) )
//            {
//                if ( file.EndsWith( ".meta" ) ) continue;
//                string relPath = file.Substring( sourceAssets.Length + 1 );
//                string dest = Path.Combine( INSTALL_PATH, relPath );
//                Directory.CreateDirectory( Path.GetDirectoryName( dest ) );
//                File.Copy( file, dest, true );
//            }
//        }
//    }
//}