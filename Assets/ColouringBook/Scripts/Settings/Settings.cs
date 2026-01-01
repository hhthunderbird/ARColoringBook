using UnityEngine;
using System;
using Felina.ARColoringBook;
#if UNITY_EDITOR
using UnityEditor;
using System.Linq;
#endif

[CreateAssetMenu( fileName = "Settings", menuName = "ColouringBook/Settings" )]
public class Settings : ScriptableObject
{
    // --- Singleton Logic (Same as before) ---
    private static Settings _instance;
    public static Settings Instance
    {
        get
        {
#if UNITY_EDITOR
            if ( _instance == null )
            {
                string[] guids = AssetDatabase.FindAssets( "t:Settings" );
                if ( guids.Length > 0 )
                    _instance = AssetDatabase.LoadAssetAtPath<Settings>( AssetDatabase.GUIDToAssetPath( guids[ 0 ] ) );
            }
#endif
            if ( _instance == null )
                Debug.LogError( "[GameSettings] CRITICAL: Not loaded! Add to Preloaded Assets." );
            return _instance;
        }
    }

    // Inside Settings.cs, add these new fields under "Scanner thresholds"

    [Header( "Quality Scoring Weights" )]
    [Range( 0f, 1f )] public float WEIGHT_ANGLE = 0.6f;  // Was hardcoded 0.6
    [Range( 0f, 1f )] public float WEIGHT_CENTER = 0.4f; // Was hardcoded 0.4

    [Header( "Distance Constraints (Meters)" )]
    public float MIN_SCAN_DIST = 0.2f; // Was 0.2m (Too strict?)
    public float MAX_SCAN_DIST = 1.0f; // Was 1.0m
    [Range( 0f, 1f )] public float DIST_PENALTY = 0.5f;  // Was 0.5 (Heavy penalty)

    // --- 1. Serialized Fields (Editor Settings) ---
    [Header( "Configuration" )]
    // Render / capture defaults
    [SerializeField]
    private int _maxResolution = 720;
    public int MAX_RESOLUTION => _maxResolution;

    [SerializeField]
    private int _maxFeedRes = 3840;
    public int MAX_FEED_RES => _maxFeedRes;  // Allow 4K resolution

    [SerializeField]
    private int _targetFrameRate = 60;
    public int TARGET_FRAME_RATE => _targetFrameRate;

    // Scanner thresholds
    [Range( 0f, 1f )]
    [SerializeField]
    private float _captureThreshold = 0.75f;
    public float CAPTURE_THRESHOLD => _captureThreshold;

    [SerializeField]
    private float _maxMoveSpeed = 0.05f;
    public float MAX_MOVE_SPEED => _maxMoveSpeed;

    [SerializeField]
    private float _maxRotateSpeed = 5.0f;
    public float MAX_ROTATE_SPEED => _maxRotateSpeed;

    public RenderTextureFormat DEFAULT_RENDERTEXTURE_FORMAT = RenderTextureFormat.ARGBHalf;

    [NonSerialized]
    public RenderTextureSettings RENDERTEXTURE_SETTINGS;

    // Safety flag
    public bool IsInitialized { get; private set; } = false;

    // --- 3. The Initialization Magic ---
    // This attribute runs this static method automatically BEFORE any MonoBehaviour's Awake()
    [RuntimeInitializeOnLoadMethod( RuntimeInitializeLoadType.BeforeSceneLoad )]
    private static void Bootstrap()
    {
        // 1. Force load the Instance
        var settings = Instance;

        // 2. Populate Runtime Values
        settings.InitializeRuntimeValues();

        Debug.Log( "[GameSettings] \u2699 Runtime values initialized!" );
    }

    private void InitializeRuntimeValues()
    {
        // Now it is safe to access the Screen API
        var screenResolution = Screen.currentResolution;
        var screenRatio = (float) screenResolution.width / screenResolution.height;
        var height = Mathf.Min( screenResolution.width, MAX_RESOLUTION );
        var width = height * screenRatio;
        

        if ( !SystemInfo.SupportsRenderTextureFormat( DEFAULT_RENDERTEXTURE_FORMAT ) )
            DEFAULT_RENDERTEXTURE_FORMAT = RenderTextureFormat.Default;

        RENDERTEXTURE_SETTINGS = new RenderTextureSettings
        {
            Width = (int) width,
            Height = height,
            UseMipMap = false,
            AutoGenerateMips = false,
            FilterMode = FilterMode.Trilinear,
            Format = DEFAULT_RENDERTEXTURE_FORMAT
        };

        IsInitialized = true;
    }

    // --- 4. Setup Automation (Same as before) ---
    private void OnEnable() => _instance = this;

    private void OnValidate()
    {
#if UNITY_EDITOR
        EditorApplication.delayCall += AddToPreloadedAssets;
#endif
    }
#if UNITY_EDITOR
    private void AddToPreloadedAssets()
    {
        if ( this == null ) return;
        var preloaded = PlayerSettings.GetPreloadedAssets().ToList();
        if ( !preloaded.Contains( this ) )
        {
            preloaded.Add( this );
            PlayerSettings.SetPreloadedAssets( preloaded.ToArray() );
            Debug.Log( $"[GameSettings] \u2713 Auto-registered '{name}' to Preloaded Assets!" );
        }
    }
#endif
}