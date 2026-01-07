using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
// 2019.4 uses UnityEditor.SceneManagement, same as 2021+
using UnityEditor.SceneManagement;

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

        [MenuItem( "Setup Project/Setup Wizard" )]
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

                // 4. Create Origin (Split by Version to avoid Compile Errors)
                GameObject originGO = null;

#if UNITY_2021_2_OR_NEWER
                // --- MODERN UNITY (XROrigin) ---
                Debug.Log("Creating Modern AR Setup (XROrigin)...");
                Type xrOriginType = GetTypeSafe("Unity.XR.CoreUtils.XROrigin");
                if (xrOriginType == null) xrOriginType = GetTypeSafe("UnityEngine.XR.CoreUtils.XROrigin");

                if (xrOriginType != null)
                {
                    originGO = new GameObject("XR Origin");
                    var originComp = originGO.AddComponent(xrOriginType);
                    
                    GameObject offsetGO = new GameObject("Camera Offset");
                    offsetGO.transform.SetParent(originGO.transform, false);
                    cameraGO.transform.SetParent(offsetGO.transform, false);
                    
                    // XROrigin has explicit properties we must set
                    var camProp = xrOriginType.GetProperty("Camera");
                    if (camProp != null) camProp.SetValue(originComp, cam);
                    
                    var offsetProp = xrOriginType.GetProperty("CameraFloorOffsetObject");
                    if (offsetProp != null) offsetProp.SetValue(originComp, offsetGO);
                }
#else
                // --- LEGACY UNITY 2019/2020 (ARSessionOrigin) ---
                Debug.Log( "Creating Legacy AR Setup (ARSessionOrigin)..." );
                originGO = new GameObject( "AR Session Origin" );
                AttachComponentSafe( originGO, "UnityEngine.XR.ARFoundation.ARSessionOrigin" );
                cameraGO.transform.SetParent( originGO.transform, false );
#endif

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

                    if ( tracker != null && _userReferenceLibrary != null )
                    {
                        // --- FIX FOR 2019.4 CRASH ---
                        // We MUST use SerializedObject. Setting properties via C# triggers runtime logic
                        // which fails because the AR Subsystem is not actually running in Edit Mode.
                        SerializedObject so = new SerializedObject( tracker );
                        so.Update();

                        // 2019.4 uses "m_ReferenceLibrary" or "m_SerializedLibrary" depending on patch version.
                        // Newer versions use "m_SerializedLibrary". We check both.
                        SerializedProperty libProp = so.FindProperty( "m_SerializedLibrary" );
                        if ( libProp == null ) libProp = so.FindProperty( "m_ReferenceLibrary" );

                        if ( libProp != null )
                        {
                            libProp.objectReferenceValue = _userReferenceLibrary;
                        }

                        // Set Max Images
                        SerializedProperty maxProp = so.FindProperty( "m_MaxNumberOfMovingImages" );
                        if ( maxProp != null ) maxProp.intValue = 4;

                        so.ApplyModifiedProperties();
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