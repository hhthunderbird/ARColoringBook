using Cysharp.Threading.Tasks;
using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Felina.ARColoringBook
{
    [DefaultExecutionOrder( -100 )]
    public class LicenseManager : MonoBehaviour
    {
        public static LicenseManager Instance { get; private set; }

        // --- UPDATED IMPORTS (Using byte[] for invoice) ---
        [DllImport( "Felina" )] private static extern void GetValidationURL( byte[] encryptedInvoice, int len, StringBuilder b, int m );
        [DllImport( "Felina" )] private static extern bool ValidateLicense( byte[] encryptedInvoice, int len, [MarshalAs( UnmanagedType.LPStr )] string r );

        // (Editor helper to encrypt on Play Mode)
        [DllImport( "Felina" )] private static extern void EncryptInvoiceString( string input, byte[] output, out int len );

        [DllImport( "Felina" )] private static extern void WatermarkCheckin();
        [DllImport( "Felina" )] private static extern IntPtr GetWatermarkData( out int s );

        // --- PUBLIC ACCESSORS ---
        public bool IsPro => _isPro;
        public bool IsBanned => _isBanned;

        // --- STATE ---
        private bool _isPro = false;
        private bool _isBanned = false;
        private Texture2D _embeddedWatermark;

        // --- RUNTIME INVOICE (Always Bytes) ---
        private byte[] _runtimeInvoice;

        private void Awake()
        {
            Instance = this;
            LoadNativeWatermark();

            if ( Settings.Instance == null ) { Debug.LogError( "[Felina] Settings missing!" ); return; }

            // --- 1. SETUP INVOICE ---
            // In a Build, we use the pre-encrypted bytes.
            // In Editor Play Mode, we encrypt the string on the fly.
            if ( Application.isEditor )
            {
                if ( !string.IsNullOrEmpty( Settings.Instance.InvoiceNumber ) )
                {
                    string txt = Settings.Instance.InvoiceNumber;
                    byte[] buffer = new byte[ txt.Length + 1 ];
                    EncryptInvoiceString( txt, buffer, out int len );
                    _runtimeInvoice = new byte[ len ];
                    Array.Copy( buffer, _runtimeInvoice, len );
                }
            }
            else
            {
                // Build Mode: Use stored bytes
                _runtimeInvoice = Settings.Instance.EncryptedInvoice;
            }

            if ( _runtimeInvoice == null || _runtimeInvoice.Length == 0 )
            {
                Debug.LogError( "[Felina] Invoice missing! Check Settings." );
                return;
            }

            // --- 2. CHECKS ---
            string storedStatus = PlayerPrefs.GetString( Internals.PREF_STATUS, "VALID" );
            if ( storedStatus == "BANNED" )
            {
                _isBanned = true; _isPro = false;
                Debug.LogWarning( "[Felina] BANNED." );
            }
            else
            {
                AttemptOfflineUnlock();
            }

            if ( Settings.Instance.BuildMode == LicenseMode.Production )
            {
                PeriodicLicenseCheckRoutine().Forget();
            }
        }

        private void AttemptOfflineUnlock()
        {
            string cachedJson = PlayerPrefs.GetString( Internals.PREF_CACHE, "" );
            if ( !_isBanned && !string.IsNullOrEmpty( cachedJson ) )
            {
                // Pass bytes to C++
                bool valid = ValidateLicense( _runtimeInvoice, _runtimeInvoice.Length, cachedJson );
                if ( valid ) _isPro = true;
            }
        }

        private async UniTask PeriodicLicenseCheckRoutine()
        {
            // 1. LOAD DATES
            string lastCheckStr = PlayerPrefs.GetString( Internals.PREF_LAST_CHECK, "0" );
            long.TryParse( lastCheckStr, out var lastBin );
            long nowBin = DateTime.Now.ToBinary();

            // 2. ASK C++ (The Black Box Check)
            // "Is it time?" -> C++ replies Yes/No based on its internal 30-day constant.
            // The user cannot find "30" in the C# code to change it.
            if ( !Internals.IsLicenseCheckDue( lastBin, nowBin ) )
            {
                return; // C++ says: "Not yet."
            }

            // ... (Proceed with URL fetching and Validation) ...
        }

        // ... (Keep LoadNativeWatermark and OnGUI) ...
        private void LoadNativeWatermark()
        {
            int size;
            IntPtr ptr = GetWatermarkData( out size );
            if ( size > 0 && ptr != IntPtr.Zero )
            {
                byte[] imageData = new byte[ size ];
                Marshal.Copy( ptr, imageData, 0, size );
                _embeddedWatermark = new Texture2D( 2, 2 );
                _embeddedWatermark.LoadImage( imageData );
                _embeddedWatermark.Apply();
            }
        }

        private void OnGUI()
        {
            if ( _isPro && !_isBanned ) return;

            WatermarkCheckin();

            if ( _embeddedWatermark != null )
            {
                GUI.color = new Color( 1, 1, 1, 0.7f );
                float w = Screen.width * 0.25f;
                float h = w * ( ( float ) _embeddedWatermark.height / _embeddedWatermark.width );
                GUI.DrawTexture( new Rect( Screen.width - w - 20, Screen.height - h - 20, w, h ), _embeddedWatermark );
            }

            if ( _isBanned )
            {
                GUI.color = Color.red;
                GUIStyle s = new GUIStyle( GUI.skin.label ) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };
                GUI.Label( new Rect( 0, 50, Screen.width, 100 ), "UNLICENSED USE", s );
                s.fontSize = 20;
                GUI.Label( new Rect( 0, Screen.height - 150, Screen.width, 50 ), "Please Purchase Valid License", s );
            }
            GUI.color = Color.white;
        }
    }
}


//using Cysharp.Threading.Tasks;
//using System;
//using System.IO;
//using System.Runtime.InteropServices;
//using System.Text;
//using UnityEngine;
//using UnityEngine.Networking;

//namespace Felina.ARColoringBook
//{
//    [DefaultExecutionOrder( -100 )] // Run before Scanner
//    public class LicenseManager : MonoBehaviour
//    {
//        public static LicenseManager Instance { get; private set; }

//        // --- SECURITY IMPORTS ONLY ---
//        [DllImport( "Felina" )] private static extern void GetValidationURL( [MarshalAs( UnmanagedType.LPStr )] string i, StringBuilder b, int m );
//        [DllImport( "Felina" )] private static extern bool ValidateLicense( [MarshalAs( UnmanagedType.LPStr )] string i, [MarshalAs( UnmanagedType.LPStr )] string r );
//        [DllImport( "Felina" )] private static extern void WatermarkCheckin();
//        [DllImport( "Felina" )] private static extern IntPtr GetWatermarkData( out int s );

//        // --- SETTINGS ---
//        private const int CHECK_INTERVAL_DAYS = 30;
//        private const string POISON_FILE = "sys_config.dat";
//        private const string PREF_LAST_CHECK = "sys_check_ts";
//        private const string PREF_STATUS = "sys_status";
//        private const string PREF_CACHE = "sys_cache";

//        // --- PUBLIC ACCESSORS ---
//        public bool IsPro => _isPro;
//        public bool IsBanned => _isBanned;

//        // --- CONFIG ---
//        public LicenseMode buildMode = LicenseMode.Development;
//        public string invoiceNumber; // Filled by settings or inspector

//        // --- INTERNAL STATE ---
//        private bool _isPro = false;
//        private bool _isBanned = false;
//        private Texture2D _embeddedWatermark;

//        private void Awake()
//        {
//            Instance = this;
//            LoadNativeWatermark();
//            InitializeSecurity();
//        }

//        private void InitializeSecurity()
//        {
//            // 1. Load Settings
//            invoiceNumber = Settings.Instance.invoiceNumber;

//            // 2. Poison/Ban Check
//            string poisonPath = Path.Combine( Application.persistentDataPath, POISON_FILE );
//            if ( File.Exists( poisonPath ) || PlayerPrefs.GetString( PREF_STATUS ) == "BANNED" )
//            {
//                _isBanned = true;
//                _isPro = false;
//                Debug.LogWarning( "[Felina] ACCESS RESTRICTED." );
//            }

//            // 3. Offline Cache Check
//            string cachedJson = PlayerPrefs.GetString( PREF_CACHE, "" );
//            if ( !_isBanned && !string.IsNullOrEmpty( cachedJson ) && Settings.Instance.buildMode != LicenseMode.Development )
//            {
//                _isPro = ValidateLicense( invoiceNumber, cachedJson );
//                if ( _isPro ) Debug.Log( "[Felina] Offline Verified." );
//            }

//            // 4. Online Check
//            if ( Settings.Instance.buildMode != LicenseMode.Development )
//            {
//                PeriodicLicenseCheckRoutine().Forget();
//            }
//        }

//        private async UniTask PeriodicLicenseCheckRoutine()
//        {
//            string lastCheckStr = PlayerPrefs.GetString( PREF_LAST_CHECK, "" );
//            if ( !string.IsNullOrEmpty( lastCheckStr ) && long.TryParse( lastCheckStr, out long binDate ) )
//            {
//                if ( ( DateTime.Now - DateTime.FromBinary( binDate ) ).TotalDays < CHECK_INTERVAL_DAYS ) return;
//            }

//            StringBuilder urlBuilder = new StringBuilder( 256 );
//            GetValidationURL( invoiceNumber, urlBuilder, urlBuilder.Capacity );

//            using ( UnityWebRequest req = UnityWebRequest.Get( urlBuilder.ToString() ) )
//            {
//                await req.SendWebRequest();
//                if ( req.result != UnityWebRequest.Result.Success ) return;

//                string json = req.downloadHandler.text;
//                bool valid = ValidateLicense( invoiceNumber, json );

//                if ( valid )
//                {
//                    _isPro = true; _isBanned = false;
//                    PlayerPrefs.SetString( PREF_STATUS, "VALID" );
//                    PlayerPrefs.SetString( PREF_CACHE, json );
//                    PlayerPrefs.SetString( PREF_LAST_CHECK, DateTime.Now.ToBinary().ToString() );

//                    string pPath = Path.Combine( Application.persistentDataPath, POISON_FILE );
//                    if ( File.Exists( pPath ) ) File.Delete( pPath );
//                    PlayerPrefs.Save();
//                }
//                else if ( json.Contains( "REFUNDED" ) || json.Contains( "BANNED" ) )
//                {
//                    _isPro = false; _isBanned = true;
//                    PlayerPrefs.SetString( PREF_STATUS, "BANNED" );
//                    File.WriteAllText( Path.Combine( Application.persistentDataPath, POISON_FILE ), "0" );
//                    PlayerPrefs.Save();
//                }
//            }
//        }

//        private void LoadNativeWatermark()
//        {
//            int size;
//            IntPtr ptr = GetWatermarkData( out size );
//            if ( size > 0 && ptr != IntPtr.Zero )
//            {
//                byte[] imageData = new byte[ size ];
//                Marshal.Copy( ptr, imageData, 0, size );
//                _embeddedWatermark = new Texture2D( 2, 2 );
//                _embeddedWatermark.LoadImage( imageData );
//                _embeddedWatermark.Apply();
//            }
//        }

//        // --- GUI IS NOW HERE ---
//        private void OnGUI()
//        {
//            if ( _isPro && !_isBanned ) return;

//            // Security Heartbeat sent from here now
//            WatermarkCheckin();

//            if ( _embeddedWatermark != null )
//            {
//                GUI.color = new Color( 1, 1, 1, 0.7f );
//                float w = Screen.width * 0.25f;
//                float h = w * ( ( float ) _embeddedWatermark.height / _embeddedWatermark.width );
//                GUI.DrawTexture( new Rect( Screen.width - w - 20, Screen.height - h - 20, w, h ), _embeddedWatermark );
//            }

//            if ( _isBanned )
//            {
//                GUI.color = Color.red;
//                GUIStyle s = new GUIStyle( GUI.skin.label ) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };
//                GUI.Label( new Rect( 0, 50, Screen.width, 100 ), "UNLICENSED USE", s );
//            }
//            GUI.color = Color.white;
//        }
//    }
//}