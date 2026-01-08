using UnityEngine;
using UnityEngine.Rendering;
using System;
using Felina.ARColoringBook.Base;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.Linq;
#endif

namespace Felina.ARColoringBook
{
    public enum RenderPipelineMode
    {
        AutoDetect,
        BuiltIn,
        URP
    }

    // AR Foundation URP compatibility matrix
    // AR Foundation 2.x: URP NOT supported (black screen issue)
    // AR Foundation 3.x: URP partial support (unstable)
    // AR Foundation 4.x+: URP fully supported

    [CreateAssetMenu( fileName = "Settings", menuName = "ColouringBook/Settings" )]
    public class Settings : ScriptableObject
    {
        private static Settings _instance;
        public static Settings Instance
        {
            get
            {
#if UNITY_EDITOR
if ( _instance == null )
                    LoadFromAssetDatabase();
#endif
                if ( _instance == null )
                    Debug.LogError( "[Settings] CRITICAL: Not loaded! Settings must be in Preloaded Assets." );

                return _instance;
            }
        }

        [Header( "Quality Scoring Weights" )]
        [Range( 0f, 1f )] public float WEIGHT_ANGLE = 0.6f;
        [Range( 0f, 1f )] public float WEIGHT_CENTER = 0.4f;

        [Header( "Distance Constraints (Meters)" )]
        public float MIN_SCAN_DIST = 0.2f;
        public float MAX_SCAN_DIST = 1.0f;
        [Range( 0f, 1f )] public float DIST_PENALTY = 0.5f;

        [Header( "Render Pipeline" )]
        [Tooltip( "Auto-detect: Automatically detect the render pipeline\nBuilt-in: Force Built-in Render Pipeline\nURP: Force Universal Render Pipeline" )]
        [SerializeField] private RenderPipelineMode _renderPipelineMode = RenderPipelineMode.AutoDetect;
        public RenderPipelineMode RenderPipelineMode => _renderPipelineMode;

        private bool _isURP = false;
        public bool IsURP => _isURP;

        private bool _urpCompatible = false;
        public bool IsURPCompatible => _urpCompatible;

        [Header( "Configuration" )]
        [SerializeField] private int _maxResolution = 720;
        public int MAX_RESOLUTION => _maxResolution;

        [SerializeField] private int _maxFeedRes = 3840;
        public int MAX_FEED_RES => _maxFeedRes;

        [SerializeField] private int _targetFrameRate = 60;
        public int TARGET_FRAME_RATE => _targetFrameRate;

        [Range( 0f, 1f )]
        [SerializeField] private float _captureThreshold = 0.75f;
        public float CAPTURE_THRESHOLD => _captureThreshold;

        [SerializeField] private float _maxMoveSpeed = 0.05f;
        public float MAX_MOVE_SPEED => _maxMoveSpeed;

        [SerializeField] private float _maxRotateSpeed = 5.0f;
        public float MAX_ROTATE_SPEED => _maxRotateSpeed;

        public RenderTextureFormat DEFAULT_RENDERTEXTURE_FORMAT = RenderTextureFormat.ARGBHalf;

        [NonSerialized]
        public RenderTextureSettings RENDERTEXTURE_SETTINGS;

        public bool IsInitialized { get; private set; } = false;

        [RuntimeInitializeOnLoadMethod( RuntimeInitializeLoadType.BeforeSceneLoad )]
        private static void Bootstrap()
        {
            var settings = Instance;
            if ( settings != null )
            {
                settings.InitializeRuntimeValues();
                Debug.Log( "[GameSettings] Runtime values initialized!" );
            }
        }

        private void InitializeRuntimeValues()
        {
            // Detect or set render pipeline
            DetermineRenderPipeline();

            var screenResolution = Screen.currentResolution;

            if ( screenResolution.height == 0 ) screenResolution.height = 1080;
            if ( screenResolution.width == 0 ) screenResolution.width = 1920;

            var screenRatio = ( float ) screenResolution.width / screenResolution.height;
            var height = Mathf.Min( screenResolution.width, MAX_RESOLUTION );
            var width = height * screenRatio;

            if ( !SystemInfo.SupportsRenderTextureFormat( DEFAULT_RENDERTEXTURE_FORMAT ) )
                DEFAULT_RENDERTEXTURE_FORMAT = RenderTextureFormat.Default;

            RENDERTEXTURE_SETTINGS = new RenderTextureSettings
            {
                Width = ( int ) width,
                Height = height,
                UseMipMap = false,
                AutoGenerateMips = false,
                FilterMode = FilterMode.Trilinear,
                Format = DEFAULT_RENDERTEXTURE_FORMAT
            };

            IsInitialized = true;
        }

        private void DetermineRenderPipeline()
        {
            // Check AR Foundation URP compatibility
            CheckARFoundationURPCompatibility();

            switch ( _renderPipelineMode )
            {
                case RenderPipelineMode.AutoDetect:
                    _isURP = GraphicsSettings.currentRenderPipeline != null;
                    if ( _isURP && !_urpCompatible )
                    {
                        Debug.LogWarning( "[Settings] URP detected but AR Foundation version may not be compatible. Forcing Built-in pipeline." );
                        Debug.LogWarning( "[Settings] AR Foundation 2.x does NOT support URP properly (black screen issue)." );
                        Debug.LogWarning( "[Settings] AR Foundation 4.x+ is required for URP support. Current version may be incompatible." );
                        _isURP = false; // Force built-in for incompatible versions
                    }
                    Debug.Log( $"[Settings] Auto-detected render pipeline: {( _isURP ? "URP" : "Built-in" )}" );
                    break;
                case RenderPipelineMode.BuiltIn:
                    _isURP = false;
                    Debug.Log( "[Settings] Using Built-in Render Pipeline (forced)" );
                    break;
                case RenderPipelineMode.URP:
                    if ( !_urpCompatible )
                    {
                        Debug.LogError( "[Settings] ?? WARNING: URP forced but AR Foundation version is NOT compatible!" );
                        Debug.LogError( "[Settings] AR Foundation 2.x does NOT support URP (will show black screen)." );
                        Debug.LogError( "[Settings] AR Foundation 4.x+ is required for URP. Expect rendering issues!" );
                    }
                    _isURP = true;
                    Debug.Log( "[Settings] Using URP (forced)" );
                    break;
            }
        }

        private void CheckARFoundationURPCompatibility()
        {
            // Check for AR Foundation version defines
            // AR Foundation 4.0+ has proper URP support
            #if AR_FOUNDATION_4_OR_NEWER || AR_FOUNDATION_5_OR_NEWER || AR_FOUNDATION_6_OR_NEWER
                _urpCompatible = true;
                Debug.Log( "[Settings] AR Foundation 4.x+ detected - URP is supported" );
            #elif UNITY_2020_2_OR_NEWER
                // Unity 2020.2+ likely has AR Foundation 3.x or 4.x
                _urpCompatible = true;
                Debug.Log( "[Settings] Unity 2020.2+ detected - URP likely supported" );
            #else
                // Unity 2019.4 with AR Foundation 2.x - URP NOT supported
                _urpCompatible = false;
                Debug.LogWarning( "[Settings] AR Foundation 2.x detected - URP NOT supported (Unity 2019.4)" );
            #endif
        }

        private void OnEnable() => _instance = this;

#if UNITY_EDITOR
        private void OnValidate()
        {
            EditorApplication.delayCall += () => RegisterPreloadedAsset( this );
        }

        public static void LoadFromAssetDatabase()
        {
            string[] guids = AssetDatabase.FindAssets( "t:Settings" );
            if ( guids.Length > 0 )
            {
                string path = AssetDatabase.GUIDToAssetPath( guids[ 0 ] );
                _instance = AssetDatabase.LoadAssetAtPath<Settings>( path );
            }
        }

        public static void RegisterPreloadedAsset( Settings settingsAsset )
        {
            if ( settingsAsset == null ) return;

            var preloaded = PlayerSettings.GetPreloadedAssets().ToList();
            if ( !preloaded.Contains( settingsAsset ) )
            {
                preloaded.Add( settingsAsset );
                PlayerSettings.SetPreloadedAssets( preloaded.ToArray() );
                Debug.Log( $"[GameSettings] Auto-registered '{settingsAsset.name}' to Preloaded Assets!" );
            }
        }

        class SettingsBuildProcessor : IPreprocessBuildWithReport
        {
            public int callbackOrder => 0;

            public void OnPreprocessBuild( BuildReport report )
            {
                Debug.Log( "[GameSettings] Verifying Settings inclusion for build..." );

                if ( _instance == null )
                {
                    LoadFromAssetDatabase();
                }

                if ( _instance != null )
                {
                    RegisterPreloadedAsset( _instance );
                }
                else
                {
                    Debug.LogError( "[GameSettings] BUILD FAILURE: Could not find a 'Settings' asset in the project! Please create one via Create > ColouringBook > Settings." );
                }
            }
        }
#endif
    }
}