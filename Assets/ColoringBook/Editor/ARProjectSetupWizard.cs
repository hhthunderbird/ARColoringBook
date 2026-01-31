using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SpatialTracking;
using Felina.ARTextureMapping.DI;
using System.Reflection;
using UnityEngine.XR.ARFoundation;

namespace Felina.ARTextureMapping.Editor
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

        [MenuItem( "Window/Coloring Book/Scene Setup Wizard" )]
        public static void ShowWindow()
        {
            var win = GetWindow<ARProjectSetupWizard>( "AR Setup" );
            win.minSize = new Vector2( 450, 600 ); 
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

            _hasLegacyUniTask = Directory.Exists( "Assets/Plugins/UniTask" );

            _isCheckingPackages = true;
            _statusMessage = "Refreshing Package List...";
            _listRequest = Client.List( true );
            EditorApplication.update += ProgressPackageCheck;
        }

        private void DetermineRecommendations()
        {
            int year = int.Parse( _unityVersionMajor );
            if ( year <= 2019 ) _recommendedArVersion = "4.1.13";
            else if ( year == 2020 ) _recommendedArVersion = "4.1.13";
            else if ( year == 2021 ) _recommendedArVersion = "4.2.10";
            else if ( year == 2022 ) _recommendedArVersion = "5.1.5";
            else _recommendedArVersion = "6.0.0";
        }

        private void CreateUniversalARScene()
        {
            try
            {
                var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                newScene.name = "AR_ImageTracking_Auto";

                GameObject sessionGO = new GameObject("AR Session");
                AttachComponentSafe(sessionGO, "UnityEngine.XR.ARFoundation.ARSession");
                AttachComponentSafe(sessionGO, "UnityEngine.XR.ARFoundation.ARInputManager");

                GameObject cameraGO = new GameObject("Main Camera");
                cameraGO.tag = "MainCamera";
                Camera cam = cameraGO.AddComponent<Camera>();
                cam.nearClipPlane = 0.01f;
                var cameraManager = AttachComponentSafe(cameraGO, "UnityEngine.XR.ARFoundation.ARCameraManager") as ARCameraManager;
                cameraManager.imageStabilizationRequested = true;
                AttachComponentSafe(cameraGO, "UnityEngine.XR.ARFoundation.ARCameraBackground");
                var trackedPoseDriver = AttachComponentSafe(cameraGO, "UnityEngine.SpatialTracking.TrackedPoseDriver") as TrackedPoseDriver;
                if (trackedPoseDriver != null)
                {
                    trackedPoseDriver.SetPoseSource(TrackedPoseDriver.DeviceType.GenericXRDevice, TrackedPoseDriver.TrackedPose.ColorCamera);
                    trackedPoseDriver.UseRelativeTransform = true;
                }

                GameObject managersGO = new GameObject("AR Managers");
                var scanner = AttachComponentSafe(managersGO, "ARScannerManager");
                var bridge = AttachComponentSafe(managersGO, "ARFoundationBridge");

                // XR Origin and ARContentSpawner
                GameObject originGO = null;
                Component trackedImageManager = null;
                Component contentSpawner = null;
#if UNITY_2021_2_OR_NEWER
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
                    var camProp = xrOriginType.GetProperty("Camera");
                    if (camProp != null) camProp.SetValue(originComp, cam);
                    var offsetProp = xrOriginType.GetProperty("CameraFloorOffsetObject");
                    if (offsetProp != null) offsetProp.SetValue(originComp, offsetGO);
                    trackedImageManager = AttachComponentSafe(originGO, "UnityEngine.XR.ARFoundation.ARTrackedImageManager");
                    contentSpawner = AttachComponentSafe(originGO, "ARContentSpawner");
                }
#else
                Debug.Log("Creating Legacy AR Setup (ARSessionOrigin)...");
                originGO = new GameObject("AR Session Origin");
                AttachComponentSafe(originGO, "UnityEngine.XR.ARFoundation.ARSessionOrigin");
                cameraGO.transform.SetParent(originGO.transform, false);
                trackedImageManager = AttachComponentSafe(originGO, "UnityEngine.XR.ARFoundation.ARTrackedImageManager");
                contentSpawner = AttachComponentSafe(originGO, "ARContentSpawner");
#endif
                
                if (trackedImageManager != null && _userReferenceLibrary != null)
                {
                    SerializedObject so = new SerializedObject(trackedImageManager);
                    so.Update();
                    SerializedProperty libProp = so.FindProperty("m_SerializedLibrary"); //m_SerializedLibrary
                    if (libProp == null) libProp = so.FindProperty("m_ReferenceLibrary");
                    if (libProp != null) libProp.objectReferenceValue = _userReferenceLibrary;
                    SerializedProperty maxProp = so.FindProperty("m_MaxNumberOfMovingImages");
                    if (maxProp != null) maxProp.intValue = 4;
                    so.ApplyModifiedProperties();
                }

                if ( contentSpawner != null )
                {
                    GameObject truckPrefab = null;
                    Texture2D outlineTex = null;

                    var truckGuids = AssetDatabase.FindAssets( "truck t:Prefab" );
                    if ( truckGuids.Length > 0 )
                        truckPrefab = AssetDatabase.LoadAssetAtPath<GameObject>( AssetDatabase.GUIDToAssetPath( truckGuids[ 0 ] ) );

                    var texGuids = AssetDatabase.FindAssets( "delivery-outline t:Texture2D" );
                    if ( texGuids.Length > 0 )
                        outlineTex = AssetDatabase.LoadAssetAtPath<Texture2D>( AssetDatabase.GUIDToAssetPath( texGuids[ 0 ] ) );

                    var targetDataField = contentSpawner.GetType().GetField( "_targetData", BindingFlags.NonPublic | BindingFlags.Instance );
                    if ( targetDataField != null )
                    {
                        var spawnerOnValidate = contentSpawner.GetType().GetMethod( "OnValidate", BindingFlags.NonPublic | BindingFlags.Instance );
                        spawnerOnValidate?.Invoke( contentSpawner, null );

                        var targetList = ( System.Collections.IList ) targetDataField.GetValue( contentSpawner );

                        for ( int i = 0; i < targetList.Count; i++ )
                        {
                            var data = targetList[ i ];
                            var nameField = data.GetType().GetField( "name" );
                            string entryName = ( string ) nameField?.GetValue( data );

                            if ( entryName != null && ( entryName.ToLower().Contains( "truck" ) || entryName.ToLower().Contains( "delivery" ) ) )
                            {
                                var prefabField = data.GetType().GetField( "prefab" );
                                var markerField = data.GetType().GetField( "blankMarker" );

                                prefabField?.SetValue( data, truckPrefab );
                                markerField?.SetValue( data, outlineTex );

                                targetList[ i ] = data;
                                Debug.Log( $"[Wizard] Assigned assets to TargetData: {entryName}" );
                            }
                        }
                    }
                    EditorUtility.SetDirty( contentSpawner );
                }

                GameObject lightGO = new GameObject("Directional Light");
                Light light = lightGO.AddComponent<Light>();
                light.type = LightType.Directional;
                lightGO.transform.rotation = Quaternion.Euler(50, -30, 0);

                // --- MONOCONTAINER SETUP ---
                GameObject containerGO = new GameObject("MonoContainer");
                var monoContainer = containerGO.AddComponent<MonoContainer>();
                // Assign dependants
                var dependantsField = typeof(MonoContainer).GetField("_dependants", BindingFlags.NonPublic | BindingFlags.Instance);
                var dependantsList = new List<MonoBehaviour>();
                if (bridge is MonoBehaviour) dependantsList.Add((MonoBehaviour)bridge);
                if (scanner is MonoBehaviour) dependantsList.Add((MonoBehaviour)scanner);
                if (contentSpawner is MonoBehaviour) dependantsList.Add((MonoBehaviour)contentSpawner);
                dependantsField?.SetValue(monoContainer, dependantsList);
                var onValidateMethod = typeof(MonoContainer).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic);
                onValidateMethod?.Invoke(monoContainer, null);
                AssetDatabase.SaveAssets();

                // --- UI CANVAS SETUP ---
                GameObject canvasGO = new GameObject("Canvas");
                var canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                var scaler = canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);

                // Button
                GameObject buttonGO = new GameObject("CaptureButton");
                buttonGO.transform.SetParent(canvasGO.transform, false);
                var buttonRect = buttonGO.AddComponent<RectTransform>();
                buttonRect.anchorMin = new Vector2(0.5f, 0);
                buttonRect.anchorMax = new Vector2(0.5f, 0);
                buttonRect.anchoredPosition = new Vector2(0, 150);
                buttonRect.sizeDelta = new Vector2(400, 120);
                var buttonImage = buttonGO.AddComponent<UnityEngine.UI.Image>();
                var button = buttonGO.AddComponent<UnityEngine.UI.Button>();

                // Semaphore Image
                GameObject semGO = new GameObject("StabilitySemaphore");
                semGO.transform.SetParent(canvasGO.transform, false);
                var semRect = semGO.AddComponent<RectTransform>();
                semRect.anchorMin = new Vector2(0.5f, 1);
                semRect.anchorMax = new Vector2(0.5f, 1);
                semRect.anchoredPosition = new Vector2(0, -150);
                semRect.sizeDelta = new Vector2(100, 100);
                var semImage = semGO.AddComponent<UnityEngine.UI.Image>();

                // UIController
                var uiController = canvasGO.AddComponent<UIController>();
                // Try to assign fields by reflection
                var btnField = typeof(UIController).GetField("_captureButton", BindingFlags.NonPublic | BindingFlags.Instance);
                if (btnField != null) btnField.SetValue(uiController, button);
                var semField = typeof(UIController).GetField("_reticleImage", BindingFlags.NonPublic | BindingFlags.Instance);
                if (semField != null) semField.SetValue(uiController, semImage);
                EditorUtility.SetDirty(uiController);
                AssetDatabase.SaveAssets();

                // --- EVENT SYSTEM SETUP ---
                // Required for UI interactions like the Capture Button
                GameObject eventSystemGO = new GameObject( "EventSystem" );
                eventSystemGO.AddComponent<UnityEngine.EventSystems.EventSystem>();

                // Check for New Input System vs Legacy Input Manager
                Type inputModuleType = GetTypeSafe( "UnityEngine.InputSystem.UI.InputSystemUIInputModule" );
                if ( inputModuleType != null )
                {
                    eventSystemGO.AddComponent( inputModuleType );
                }
                else
                {
                    eventSystemGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                }

                // Set RotationMaterial on ARScannerManager if found
                if (scanner != null)
                {
                    var matGuids = AssetDatabase.FindAssets("t:Material RotationMaterial");
                    if (matGuids.Length > 0)
                    {
                        var matPath = AssetDatabase.GUIDToAssetPath(matGuids[0]);
                        var rotationMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                        var rotMatField = scanner.GetType().GetField("_rotationMaterial", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (rotMatField != null)
                        {
                            rotMatField.SetValue(scanner, rotationMat);
                            EditorUtility.SetDirty(scanner);
                        }
                    }
                }

                EditorUtility.DisplayDialog("Success", "AR Scene generated successfully!", "OK");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error creating scene: {e}");
                EditorUtility.DisplayDialog("Error", "Failed to generate scene. Check console.", "OK");
                GUIUtility.ExitGUI();
            }
        }

        private Component AttachComponentSafe( GameObject go, string typeName )
        {
            Type t = GetTypeSafe( typeName );
            if ( t != null ) return go.AddComponent( t );
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

        private void RepairAsmdefs()
        {
            string[] asmdefFiles = new string[]
            {
                "Assets/ColouringBook/Scripts/Runtime/Felina.ARTextureMapping.Runtime.asmdef",
                "Assets/ColouringBook/Scripts/Editor/Felina.ARTextureMapping.Editor.asmdef",
                "Assets/ColouringBook/Scripts/Events/Felina.ARTextureMapping.Events.asmdef"
            };

            bool changed = false;

            foreach ( var path in asmdefFiles )
            {
                if ( !File.Exists( path ) ) continue;

                string json = File.ReadAllText( path );
                bool fileChanged = false;

                if ( !json.Contains( "\"UniTask\"" ) )
                {
                    if ( json.Contains( "\"references\": [" ) )
                    {
                        json = json.Replace( "\"references\": [", "\"references\": [\n        \"UniTask\"," );
                        fileChanged = true;
                        Debug.Log( $"Added UniTask reference to {Path.GetFileName( path )}" );
                    }
                }

                if ( fileChanged )
                {
                    File.WriteAllText( path, json );
                    changed = true;
                }

                AssetDatabase.ImportAsset( path, ImportAssetOptions.ForceUpdate );
            }

            if ( changed )
            {
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog( "Repair Complete", "Assembly Definitions have been updated. Scripts should recompile now.", "OK" );
            }
            else
            {
                EditorUtility.DisplayDialog( "Refreshed", "Forced re-evaluation of Assembly Definitions.", "OK" );
            }
        }

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
            DrawRepairSection();

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
            EditorGUILayout.Space();
        }

        private void DrawRepairSection()
        {
            EditorGUILayout.LabelField( "Troubleshooting", EditorStyles.boldLabel );
            using ( new EditorGUILayout.VerticalScope( EditorStyles.helpBox ) )
            {
                EditorGUILayout.LabelField( "Scripts missing references? Click below." );
                EditorGUILayout.HelpBox( "This forces Unity to re-check all Assembly Definitions (asmdef) and ensures UniTask is linked correctly.", MessageType.Info );

                if ( GUILayout.Button( "Repair Assembly Definitions", GUILayout.Height( 30 ) ) )
                {
                    RepairAsmdefs();
                }
            }
        }
    }
}