using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Felina.ARColoringBook
{
    /// <summary>
    /// Manages scanning of tracked images, computes homography on the native side and
    /// produces GPU RenderTexture captures for consumers.
    /// </summary>
    public class ARScannerManager : MonoBehaviour
    {
        /// <summary>
        /// Singleton instance of the scanner manager.
        /// </summary>
        public static ARScannerManager Instance { get; private set; }

        // --- MATH IMPORTS ONLY ---
        [DllImport( "Felina" )] private static extern bool CheckStability( float3 f1, quaternion q1, float3 f2, quaternion q2, float f3, float f4, float f5 );
        [DllImport( "Felina" )] private static extern float CalculateQuality( float3 f1, float3 f2, float3 f3, float3 f4, float2 f5, float f6, float f7 );
        [DllImport( "Felina" )] private static unsafe extern void CalculateHomography( float f1, float f2, float f3, float f4, void* s, void* r );

        // --- SETTINGS ---
        [Header( "Architecture" )]
        [SerializeField] private MonoBehaviour _arBridgeComponent;
        [SerializeField] private IARBridge _arBridge;

        [Header( "Scanner Settings" )]
        public Material unwarpMaterial;
        public int outputResolution = 1024;
        [Range( 0f, 1f )] public float captureThreshold = 0.85f;
        public bool autoLock = true;
        public float maxMoveSpeed = 0.05f;
        public float maxRotateSpeed = 5.0f;

        // --- STATE ---
        /// <summary>
        /// Invoked when a new RenderTexture capture is available for a named target.
        /// Parameters: (targetName, renderTexture, quality)
        /// </summary>
        public event Action<string, RenderTexture, float> OnTextureCaptured;
        private Camera _arCamera;
        [SerializeField, HideInInspector] private List<ScanTarget> _targetList = new();
        private Dictionary<string, ScanTarget> _activeTargets = new();
        private Dictionary<string, bool> _isLocked = new Dictionary<string, bool>();
        private Dictionary<string, float> _bestScores = new Dictionary<string, float>();
        private Dictionary<string, RenderTexture> _capturedTextures = new Dictionary<string, RenderTexture>();

        // Native Buffers
        private NativeArray<float2> _nativeScreenPoints;
        private NativeArray<float4x4> _nativeResultMatrix;
        private float3 _lastCamPos;
        private quaternion _lastCamRot;
        public bool IsDeviceStable { get; private set; } = false;

        private void Awake()
        {
            Instance = this;
            _activeTargets.Clear();
            foreach ( var t in _targetList )
            {
                if ( !_activeTargets.ContainsKey( t.Name ) ) _activeTargets.Add( t.Name, t );
            }
        }

        void Start()
        {
            if ( _arBridge != null ) _arCamera = _arBridge.GetARCamera();
            _nativeScreenPoints = new NativeArray<float2>( 4, Allocator.Persistent );
            _nativeResultMatrix = new NativeArray<float4x4>( 1, Allocator.Persistent );

            if ( _arCamera != null )
            {
                _lastCamPos = _arCamera.transform.position;
                _lastCamRot = _arCamera.transform.rotation;
            }
        }

        void OnDestroy()
        {
            if ( _nativeScreenPoints.IsCreated ) _nativeScreenPoints.Dispose();
            if ( _nativeResultMatrix.IsCreated ) _nativeResultMatrix.Dispose();
            foreach ( var rt in _capturedTextures.Values ) if ( rt != null ) rt.Release();
            _capturedTextures.Clear();
        }

        void OnEnable() { if ( _arBridge != null ) _arBridge.OnTargetAdded += OnTargetAdded; }
        void OnDisable() { if ( _arBridge != null ) _arBridge.OnTargetAdded -= OnTargetAdded; }

        void Update()
        {
            if ( _arCamera == null ) return;

            // 1. Calculate Stability
            CalculateNativeStability();
            if ( !IsDeviceStable ) return;

            // 2. Iterate Targets
            foreach ( var kvp in _activeTargets )
            {
                ScanTarget target = kvp.Value;
                if ( _isLocked.ContainsKey( target.Name ) && _isLocked[ target.Name ] ) continue;
                if ( target.Transform == null ) continue;

                float score = GetNativeQuality( target.Transform );
                if ( !_bestScores.ContainsKey( target.Name ) ) _bestScores[ target.Name ] = 0f;

                if ( score > _bestScores[ target.Name ] )
                {
                    _bestScores[ target.Name ] = score;
                    ProcessCaptureGPU( target, score );
                }
            }
        }

        private unsafe void ProcessCaptureGPU( ScanTarget target, float score )
        {
            // --- DEPENDENCY CHECK ---
            // If License Manager is missing or we are not Pro,
            // we technically *can* still track, but the C++ "CalculateHomography"
            // will internally check if WatermarkCheckin() was called.
            // Since FelinaLicenseManager handles that, we just run normally.
            // ------------------------

            if ( !_nativeScreenPoints.IsCreated || !_nativeResultMatrix.IsCreated ) return;

            var source = _arBridge.GetCameraFeedRT();
            if ( source == null ) return;

            Vector2 size = target.Size;
            float hx = size.x * 0.5f; float hy = size.y * 0.5f;
            Transform t = target.Transform;

            _nativeScreenPoints[ 0 ] = ToScreen( t.TransformPoint( new Vector3( -hx, 0, -hy ) ) );
            _nativeScreenPoints[ 1 ] = ToScreen( t.TransformPoint( new Vector3( hx, 0, -hy ) ) );
            _nativeScreenPoints[ 2 ] = ToScreen( t.TransformPoint( new Vector3( hx, 0, hy ) ) );
            _nativeScreenPoints[ 3 ] = ToScreen( t.TransformPoint( new Vector3( -hx, 0, hy ) ) );

            // Call C++ Math
            CalculateHomography( size.x, size.y, Screen.width, Screen.height, _nativeScreenPoints.GetUnsafePtr(), _nativeResultMatrix.GetUnsafePtr() );

            float4x4 H = _nativeResultMatrix[ 0 ];

            // SECURITY RESULT CHECK
            // If the License is invalid, C++ returns Identity matrix (or zeros).
            // This implicitly stops the unwarping effect.
            if ( H.c3.w == 0 ) return;

            // ... (Rest of Blit Logic) ...
            RenderTexture destRT;
            if ( !_capturedTextures.TryGetValue( target.Name, out destRT ) )
            {
                destRT = new RenderTexture( outputResolution, outputResolution, 0, RenderTextureFormat.RGB565 );
                destRT.Create();
                _capturedTextures[ target.Name ] = destRT;
            }

            unwarpMaterial.SetTexture( "_MainTex", source );
            unwarpMaterial.SetMatrix( "_Homography", H );
            unwarpMaterial.SetMatrix( "_DisplayMatrix", Matrix4x4.identity );
            Graphics.Blit( null, destRT, unwarpMaterial );

            OnTextureCaptured?.Invoke( target.Name, destRT, score );

            if ( autoLock && score >= captureThreshold )
            {
                _isLocked[ target.Name ] = true;
            }
        }

        private void CalculateNativeStability()
        {
            float3 curPos = _arCamera.transform.position;
            quaternion curRot = _arCamera.transform.rotation;
            float dt = Time.deltaTime;
            IsDeviceStable = CheckStability( curPos, curRot, _lastCamPos, _lastCamRot, dt, maxMoveSpeed, maxRotateSpeed );
            _lastCamPos = curPos;
            _lastCamRot = curRot;
        }

        private float GetNativeQuality( Transform targetTransform )
        {
            float3 camPos = _arCamera.transform.position;
            float3 camFwd = _arCamera.transform.forward;
            float3 imgPos = targetTransform.position;
            float3 imgUp = targetTransform.up;
            float3 sPos3 = _arCamera.WorldToScreenPoint( imgPos );
            float2 sPos = ( sPos3.z > 0 ) ? new float2( sPos3.x, sPos3.y ) : new float2( -1, -1 );
            return CalculateQuality( camPos, camFwd, imgPos, imgUp, sPos, Screen.width, Screen.height );
        }

        private float2 ToScreen( Vector3 worldPos )
        {
            Vector3 s = _arCamera.WorldToScreenPoint( worldPos );
            return new float2( s.x, s.y );
        }

        private void OnTargetAdded( ScanTarget incomingTarget )
        {
            if ( _activeTargets.ContainsKey( incomingTarget.Name ) )
            {
                ScanTarget existing = _activeTargets[ incomingTarget.Name ];
                existing.Transform = incomingTarget.Transform;
                existing.IsTracking = true;
                _activeTargets[ incomingTarget.Name ] = existing;
            }
            else
            {
                _activeTargets.Add( incomingTarget.Name, incomingTarget );
            }
        }

        public RenderTexture GetCapturedTexture( string targetName ) => _capturedTextures.ContainsKey( targetName ) ? _capturedTextures[ targetName ] : null;
        public void AddScanTarget( ScanTarget newTarget ) { _targetList.Add( newTarget ); }
    }
}


//using Cysharp.Threading.Tasks;
//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Runtime.InteropServices;
//using System.Text;
//using Unity.Collections;
//using Unity.Collections.LowLevel.Unsafe;
//using Unity.Mathematics;
//using UnityEngine;
//using UnityEngine.Networking;

//namespace Felina.ARColoringBook
//{
//    public enum LicenseMode
//    {
//        Development, // Loose checks, allows offline
//        Production   // Strict checks, requires valid Firebase status
//    }

//    public class ARScannerManager : MonoBehaviour
//    {
//        public static ARScannerManager Instance { get; private set; }

//        // --- NATIVE IMPORTS ---
//        // We pass the invoice and the raw JSON string
//        [DllImport( "Felina" )] private static extern void GetValidationURL( [MarshalAs( UnmanagedType.LPStr )] string i, StringBuilder b, int m );
//        [DllImport( "Felina" )] private static extern bool ValidateLicense( [MarshalAs( UnmanagedType.LPStr )] string i, [MarshalAs( UnmanagedType.LPStr )] string r );
//        [DllImport( "Felina" )] private static extern void WatermarkCheckin();
//        [DllImport( "Felina" )] private static extern IntPtr GetWatermarkData( out int s );
//        [DllImport( "Felina" )] private static extern bool CheckStability( float3 f1, quaternion q1, float3 f2, quaternion q2, float f3, float f4, float f5 );
//        [DllImport( "Felina" )] private static extern float CalculateQuality( float3 f1, float3 f2, float3 f3, float3 f4, float2 f5, float f6, float f7 );
//        [DllImport( "Felina" )] private static unsafe extern void CalculateHomography( float f1, float f2, float f3, float f4, void* s, void* r );

//        private const int CHECK_INTERVAL_DAYS = 30;
//        private const string POISON_FILE = "sys_config.dat";
//        private const string PREF_LAST_CHECK = "sys_check_ts";
//        private const string PREF_STATUS = "sys_status";
//        private const string PREF_CACHE = "sys_cache";

//        [SerializeField, HideInInspector]
//        private Settings _settings;

//        [Header( "Build Configuration" )]
//        [Tooltip( "Set to 'Production' only when building for the App Store." )]
//        public LicenseMode buildMode = LicenseMode.Development;

//        [Header( "Licensing" )]
//        [Tooltip( "Enter your Asset Store Invoice Number." )]
//        public string invoiceNumber;
//        [Tooltip( "Enter the Key generated from the Felina Portal." )]
//        public string licenseKey;

//        // REMOVED: public Texture2D watermarkTexture; 
//        private Texture2D _embeddedWatermark; // Internal use only
//        private bool _isPro = false;
//        private bool _isBanned = false;

//        // --- COMPONENTS ---
//        [Header( "Architecture" )]
//        [SerializeField] private MonoBehaviour _arBridgeComponent;
//        [SerializeField] private IARBridge _arBridge;

//        [Header( "Scanner Settings" )]
//        public Material unwarpMaterial;
//        public int outputResolution = 1024;
//        [Range( 0f, 1f )] public float captureThreshold = 0.85f;
//        public bool autoLock = true;

//        [Header( "Stability" )]
//        public float maxMoveSpeed = 0.05f;
//        public float maxRotateSpeed = 5.0f;

//        // EVENT: Passes RenderTexture (GPU Handle) instead of Texture2D (CPU Memory)
//        public event Action<string, RenderTexture, float> OnTextureCaptured;

//        // State
//        private Camera _arCamera;

//        // Tracking Lists
//        [SerializeField, HideInInspector]
//        private List<ScanTarget> _targetList = new();
//        private Dictionary<string, ScanTarget> _activeTargets = new();
//        private Dictionary<string, bool> _isLocked = new Dictionary<string, bool>();
//        private Dictionary<string, float> _bestScores = new Dictionary<string, float>();

//        // GPU Cache: Keeps the textures alive on the GPU
//        private Dictionary<string, RenderTexture> _capturedTextures = new Dictionary<string, RenderTexture>();

//        // Native Buffers (Reusable)
//        private NativeArray<float2> _nativeScreenPoints;
//        private NativeArray<float4x4> _nativeResultMatrix;

//        // Stability State
//        private float3 _lastCamPos;
//        private quaternion _lastCamRot;
//        public bool IsDeviceStable { get; private set; } = false;

//        private void Awake()
//        {
//            Instance = this;

//            if ( _settings == null )
//            {
//                Debug.LogError( "[Felina] Settings file missing! App cannot validate." );
//                return;
//            }

//            // 1. FAST CHECK: Load previous status from PlayerPrefs
//            string storedStatus = PlayerPrefs.GetString( PREF_STATUS, "VALID" ); // Default to innocent

//            if ( storedStatus == "BANNED" )
//            {
//                Debug.LogWarning( "[Felina] CACHED BAN DETECTED. Reverting to Restricted Mode." );
//                _isBanned = true;
//                _isPro = false;
//                // We do NOT return. We let the app run in Shame Mode.
//            }
//            else if ( storedStatus != "VALID_STATUS_2025" )
//            {
//                _isBanned = false;
//                _isPro = false;
//            }

//            // 1. Load Watermark from C++ (Secure)
//            LoadNativeWatermark();


//            // 1. TRY CACHED LICENSE FIRST
//            // This allows the app to work offline immediately if previously validated.
//            string cachedJson = PlayerPrefs.GetString( PREF_CACHE, "" );

//            if ( !string.IsNullOrEmpty( cachedJson ) && !string.IsNullOrEmpty( invoiceNumber ) )
//            {
//                // Send cached data to C++ to unlock immediately
//                _isPro = ValidateLicense( invoiceNumber, cachedJson );

//                if ( _isPro ) Debug.Log( " [Felina] Unlocked via Cache." );
//                else
//                {
//                    // If cached json is "REFUNDED", C++ returns false. We ban.
//                    Debug.LogWarning( " [Felina] Cached license is Invalid/Banned." );
//                    _isBanned = true;
//                }
//            }

//            // 2. RUN NETWORK CHECK (If needed)
//            //CheckLicenseRoutine().Forget();

//            if ( _isPro ) Debug.Log( $"[Felina] Pro License Active." );
//            else Debug.LogWarning( $"[Felina] Free/Dev Mode. Watermark Active." );

//            // 4. Start Background Re-Validation (Monthly Check)
//            if ( _isPro && !_isBanned && !_settings.isDevelopmentBuild )
//            {
//                PeriodicLicenseCheckRoutine().Forget();
//            }

//            // Initialize Dictionary from the Serialized List
//            _activeTargets.Clear();
//            foreach ( var t in _targetList )
//            {
//                if ( !_activeTargets.ContainsKey( t.Name ) )
//                {
//                    _activeTargets.Add( t.Name, t );
//                }
//            }
//        }

//        void Start()
//        {
//            if ( _arBridge != null ) _arCamera = _arBridge.GetARCamera();

//            // Allocate Native Memory (Persistent)
//            _nativeScreenPoints = new NativeArray<float2>( 4, Allocator.Persistent );
//            _nativeResultMatrix = new NativeArray<float4x4>( 1, Allocator.Persistent );

//            if ( _arCamera != null )
//            {
//                _lastCamPos = _arCamera.transform.position;
//                _lastCamRot = _arCamera.transform.rotation;
//            }
//        }

//        //private async UniTask CheckLicenseRoutine()
//        //{
//        //    // Check interval logic
//        //    string lastCheckStr = PlayerPrefs.GetString( PREF_LAST_CHECK, "" );
//        //    if ( !string.IsNullOrEmpty( lastCheckStr ) && long.TryParse( lastCheckStr, out long binDate ) )
//        //    {
//        //        if ( ( DateTime.Now - DateTime.FromBinary( binDate ) ).TotalDays < CHECK_INTERVAL_DAYS )
//        //        {
//        //            //return early
//        //            return;
//        //        }
//        //    }

//        //    Debug.Log( "[Felina] Verifying License with Server..." );
//        //    string url = $"{FIREBASE_URL}licenses/{invoiceNumber}.json";

//        //    using ( UnityWebRequest req = UnityWebRequest.Get( url ) )
//        //    {

//        //        await req.SendWebRequest();

//        //        if ( req.result == UnityWebRequest.Result.Success )
//        //        {
//        //            string json = req.downloadHandler.text;

//        //            // 3. PASS TO C++ BLACK BOX
//        //            // We don't parse it here. We give it to C++.
//        //            bool isValid = ValidateLicense( invoiceNumber, json );

//        //            if ( isValid )
//        //            {
//        //                Debug.Log( "[Felina] Server confirmed license. Updating Cache." );
//        //                _isPro = true;
//        //                _isBanned = false;

//        //                // Save valid JSON for offline use
//        //                PlayerPrefs.SetString( PREF_CACHED_JSON, json );
//        //                PlayerPrefs.SetString( PREF_LAST_CHECK, DateTime.Now.ToBinary().ToString() );
//        //                PlayerPrefs.Save();
//        //            }
//        //            else
//        //            {
//        //                Debug.LogError( "[Felina] Server rejected license." );
//        //                _isPro = false;
//        //                _isBanned = true;
//        //                // Overwrite cache with the "REFUNDED" json so offline works (as a ban)
//        //                PlayerPrefs.SetString( PREF_CACHED_JSON, json );
//        //                PlayerPrefs.Save();
//        //            }
//        //        }
//        //    }
//        //}

//        // --- THE KILL SWITCH ROUTINE ---
//        private async UniTask PeriodicLicenseCheckRoutine()
//        {
//            // A. Interval Check
//            string lastCheckStr = PlayerPrefs.GetString( PREF_LAST_CHECK, "" );
//            if ( !string.IsNullOrEmpty( lastCheckStr ) && long.TryParse( lastCheckStr, out long binDate ) )
//            {
//                if ( ( DateTime.Now - DateTime.FromBinary( binDate ) ).TotalDays < CHECK_INTERVAL_DAYS ) return;
//            }

//            Debug.Log( "[Felina] Contacting License Server..." );

//            // B. Get URL from C++ (Hidden)
//            StringBuilder urlBuilder = new StringBuilder( 256 );
//            GetValidationURL( _settings.invoiceNumber, urlBuilder, urlBuilder.Capacity );
//            string url = urlBuilder.ToString();

//            using ( UnityWebRequest req = UnityWebRequest.Get( url ) )
//            {
//                await req.SendWebRequest();

//                if ( req.result != UnityWebRequest.Result.Success )
//                {
//                    Debug.LogWarning( "[Felina] Network Error. Skipping check." );
//                    return;
//                }

//                string json = req.downloadHandler.text;

//                // C. Pass to C++ Black Box
//                bool isValid = ValidateLicense( _settings.invoiceNumber, json );

//                if ( isValid )
//                {
//                    Debug.Log( "[Felina] Verified." );
//                    _isPro = true; _isBanned = false;
//                    PlayerPrefs.SetString( PREF_STATUS, "VALID" );
//                    PlayerPrefs.SetString( PREF_CACHE, json );
//                    PlayerPrefs.SetString( PREF_LAST_CHECK, DateTime.Now.ToBinary().ToString() );

//                    // Cleanup poison if exists
//                    string pPath = Path.Combine( Application.persistentDataPath, POISON_FILE );
//                    if ( File.Exists( pPath ) ) File.Delete( pPath );

//                    PlayerPrefs.Save();
//                }
//                else
//                {
//                    // D. Ban Logic (Only if explicit)
//                    if ( json.Contains( "BANNED" ) )
//                    {
//                        Debug.LogError( " [Felina] BANNED DETECTED." );
//                        _isPro = false; _isBanned = true;
//                        PlayerPrefs.SetString( PREF_STATUS, "BANNED" );
//                        File.WriteAllText( Path.Combine( Application.persistentDataPath, POISON_FILE ), "0" );
//                        PlayerPrefs.Save();
//                    }
//                    if ( json.Contains( "REFUNDED" ))
//                    {
//                        Debug.LogError( " [Felina] REFUND DETECTED." );
//                        _isPro = false; _isBanned = true;
//                        PlayerPrefs.SetString( PREF_STATUS, "REFUNDED" );
//                        File.WriteAllText( Path.Combine( Application.persistentDataPath, POISON_FILE ), "0" );
//                        PlayerPrefs.Save();
//                    }
//                    else
//                    {
//                        Debug.LogWarning( $"[Felina] Invalid/Garbage response. Ignoring." );
//                    }
//                }
//            }
//        }

//        private void LoadNativeWatermark()
//        {
//            var ptr = GetWatermarkData( out var size );

//            if ( size > 0 && ptr != IntPtr.Zero )
//            {
//                byte[] imageData = new byte[ size ];
//                Marshal.Copy( ptr, imageData, 0, size );

//                _embeddedWatermark = new Texture2D( 2, 2 );
//                _embeddedWatermark.LoadImage( imageData ); // Auto-decodes PNG/JPG
//                _embeddedWatermark.Apply();
//            }
//        }
//        void OnDestroy()
//        {
//            if ( _nativeScreenPoints.IsCreated ) _nativeScreenPoints.Dispose();
//            if ( _nativeResultMatrix.IsCreated ) _nativeResultMatrix.Dispose();

//            // Clean up GPU memory
//            foreach ( var rt in _capturedTextures.Values )
//            {
//                if ( rt != null ) rt.Release();
//            }
//            _capturedTextures.Clear();
//        }

//        void OnEnable() { if ( _arBridge != null ) _arBridge.OnTargetAdded += OnTargetAdded; }
//        void OnDisable() { if ( _arBridge != null ) _arBridge.OnTargetAdded -= OnTargetAdded; }

//        // --- THE PHOTOGRAPHER'S LOOP ---
//        void Update()
//        {
//            if ( _arCamera == null ) return;

//            // 1. Check Conditions (Is the camera steady?)
//            CalculateNativeStability();

//            // 2. If Shaking -> Do Nothing (Save Battery)
//            if ( !IsDeviceStable ) return;

//            // 3. Evaluate "The Shot" for every visible target
//            foreach ( var kvp in _activeTargets )
//            {
//                ScanTarget target = kvp.Value;

//                // Ignore if already done or invalid
//                if ( _isLocked.ContainsKey( target.Name ) && _isLocked[ target.Name ] ) continue;
//                if ( target.Transform == null ) continue;

//                // 4. Calculate Score (Is the angle good?)
//                float score = GetNativeQuality( target.Transform );

//                if ( !_bestScores.ContainsKey( target.Name ) ) _bestScores[ target.Name ] = 0f;

//                // 5. If this is the best shot so far -> Capture it!
//                if ( score > _bestScores[ target.Name ] )
//                {
//                    _bestScores[ target.Name ] = score;
//                    ProcessCaptureGPU( target, score );
//                }
//            }
//        }

//        private void OnTargetAdded( ScanTarget incomingTarget )
//        {
//            // Debug.Log($"[Felina] Bridge found: {incomingTarget.Name}");

//            if ( _activeTargets.ContainsKey( incomingTarget.Name ) )
//            {
//                // MERGE: We found a match!
//                // We take the "Live" Transform from the incoming data
//                // and put it into our existing slot.
//                ScanTarget existing = _activeTargets[ incomingTarget.Name ];

//                existing.Transform = incomingTarget.Transform; // UPDATE THE REFERENCE
//                existing.IsTracking = true;

//                // Save the updated struct back into the dictionary
//                _activeTargets[ incomingTarget.Name ] = existing;

//                // Debug.Log($"[Felina] ACTIVATED existing target '{incomingTarget.Name}'");
//            }
//            else
//            {
//                // NEW: This wasn't in our Editor List, but ARFoundation found it.
//                // Add it as a new live target.
//                _activeTargets.Add( incomingTarget.Name, incomingTarget );
//                // Debug.Log($"[Felina] ADDED new target '{incomingTarget.Name}'");
//            }
//        }

//        private void CalculateNativeStability()
//        {
//            float3 curPos = _arCamera.transform.position;
//            quaternion curRot = _arCamera.transform.rotation;
//            float dt = Time.deltaTime;

//            IsDeviceStable = CheckStability( curPos, curRot, _lastCamPos, _lastCamRot, dt, maxMoveSpeed, maxRotateSpeed );

//            _lastCamPos = curPos;
//            _lastCamRot = curRot;
//        }

//        private float GetNativeQuality( Transform targetTransform )
//        {
//            float3 camPos = _arCamera.transform.position;
//            float3 camFwd = _arCamera.transform.forward;
//            float3 imgPos = targetTransform.position;
//            float3 imgUp = targetTransform.up;

//            float3 sPos3 = _arCamera.WorldToScreenPoint( imgPos );
//            // Handle "behind camera"
//            float2 sPos = ( sPos3.z > 0 ) ? new float2( sPos3.x, sPos3.y ) : new float2( -1, -1 );

//            return CalculateQuality( camPos, camFwd, imgPos, imgUp, sPos, Screen.width, Screen.height );
//        }

//        private unsafe void ProcessCaptureGPU( ScanTarget target, float score )
//        {
//            // A. NOW we ask the Bridge for the image data
//            RenderTexture source = _arBridge.GetCameraFeedRT();
//            if ( source == null ) return;

//            // B. Calculate Math (Native)
//            Vector2 size = target.Size;
//            float hx = size.x * 0.5f; float hy = size.y * 0.5f;
//            Transform t = target.Transform;

//            _nativeScreenPoints[ 0 ] = ToScreen( t.TransformPoint( new Vector3( -hx, 0, -hy ) ) );
//            _nativeScreenPoints[ 1 ] = ToScreen( t.TransformPoint( new Vector3( hx, 0, -hy ) ) );
//            _nativeScreenPoints[ 2 ] = ToScreen( t.TransformPoint( new Vector3( hx, 0, hy ) ) );
//            _nativeScreenPoints[ 3 ] = ToScreen( t.TransformPoint( new Vector3( -hx, 0, hy ) ) );

//            CalculateHomography(
//                size.x, size.y,
//                Screen.width, Screen.height,
//                _nativeScreenPoints.GetUnsafePtr(),
//                _nativeResultMatrix.GetUnsafePtr()
//            );

//            float4x4 H = _nativeResultMatrix[ 0 ];
//            if ( H.c3.w == 0 ) return;

//            // C. Get Output Texture (Persistent on GPU)
//            RenderTexture destRT;

//            Debug.Log( $"[Felina] ARScannerManager: Processing Capture GPU for '{target.Name}' with score {score}" );

//            if ( !_capturedTextures.TryGetValue( target.Name, out destRT ) )
//            {
//                destRT = new RenderTexture( outputResolution, outputResolution, 0, RenderTextureFormat.RGB565 );
//                destRT.Create();
//                Debug.Log( $"[Felina] ARScannerManager: Created new RenderTexture for '{target.Name}'" );
//                _capturedTextures[ target.Name ] = destRT;
//                Debug.Log( $"[Felina] ARScannerManager: Stored RenderTexture for '{target.Name}' in cache {_capturedTextures.Count}" );
//            }

//            // D. Blit (GPU Copy)
//            unwarpMaterial.SetTexture( "_MainTex", source );
//            unwarpMaterial.SetMatrix( "_Homography", H );
//            unwarpMaterial.SetMatrix( "_DisplayMatrix", Matrix4x4.identity );

//            Graphics.Blit( null, destRT, unwarpMaterial );

//            // E. Notify Consumers (They will apply destRT to their materials)
//            OnTextureCaptured?.Invoke( target.Name, destRT, score );

//            // F. Lock if good enough
//            if ( autoLock && score >= captureThreshold )
//            {
//                _isLocked[ target.Name ] = true;
//                Debug.Log( $"[Felina] LOCKED {target.Name} (GPU)" );
//            }
//        }

//        private float2 ToScreen( Vector3 worldPos )
//        {
//            Vector3 s = _arCamera.WorldToScreenPoint( worldPos );
//            return new float2( s.x, s.y );
//        }

//        // Used by Late-Joining objects to get the texture immediately
//        public RenderTexture GetCapturedTexture( string targetName )
//        {
//            Debug.Log( $"[Felina] ARScannerManager: GetCapturedTexture called for '{targetName}'" );
//            Debug.Log( $"[Felina] ARScannerManager: {_capturedTextures.Count} {_capturedTextures.ContainsKey( targetName )}" );

//            foreach ( var key in _capturedTextures.Keys )
//            {
//                Debug.Log( $"[Felina] ARScannerManager: Captured Texture Key: '{key}'" );
//            }

//            return _capturedTextures.ContainsKey( targetName ) ? _capturedTextures[ targetName ] : null;
//        }

//        public void AddScanTarget( ScanTarget newTarget )
//        {
//            _targetList.Add( newTarget );
//        }

//        private void OnGUI()
//        {
//            if ( _isPro ) return;

//            // 1. Security Heartbeat
//            WatermarkCheckin();

//            // 2. Draw Embedded Watermark
//            if ( _embeddedWatermark != null )
//            {
//                GUI.color = new Color( 1, 1, 1, 0.7f ); // Transparency

//                float w = Screen.width * 0.25f; // 25% of screen width
//                float h = w * ( ( float ) _embeddedWatermark.height / _embeddedWatermark.width );

//                // Bottom Right
//                GUI.DrawTexture( new Rect( Screen.width - w - 20, Screen.height - h - 20, w, h ), _embeddedWatermark );
//                GUI.color = Color.white;
//            }

//            // 4. If BANNED, add extra Red Warning Text
//            if ( _isBanned )
//            {
//                GUI.color = Color.red;

//                // Create a style for big bold text
//                GUIStyle style = new GUIStyle( GUI.skin.label );
//                style.fontSize = 30;
//                style.fontStyle = FontStyle.Bold;
//                style.alignment = TextAnchor.UpperCenter;

//                // Draw Text at Top Center
//                GUI.Label( new Rect( 0, 50, Screen.width, 100 ), "UNLICENSED USE", style );

//                // Optional: Draw it again at bottom for visibility
//                style.fontSize = 20;
//                GUI.Label( new Rect( 0, Screen.height - 150, Screen.width, 50 ), "Please Purchase Valid License", style );
//            }

//            GUI.color = Color.white; // Reset
//        }
//    }
//}