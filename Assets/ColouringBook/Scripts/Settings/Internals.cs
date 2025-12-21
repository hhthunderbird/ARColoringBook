/********************************************************************/
/*DO NOT EDIT. INTEGRITY CHECK ENABLED. MODIFICATION VOIDS SUPPORT. */
/********************************************************************/
using System.Runtime.InteropServices;
using System.Text;
namespace Felina.ARColoringBook
{
    internal static class Internals
    {
        private static StringBuilder _sb;
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] internal static extern bool IsLicenseCheckDue( long lastCheck, long now );
        [DllImport("__Internal")] private static extern int GetConfigInt( int i );
        [DllImport("__Internal")] private static extern void GetConfigString( int i, StringBuilder s, int m );
#else
        [DllImport( "Felina" )] internal static extern bool IsLicenseCheckDue( long lastCheck, long now );
        [DllImport( "Felina" )] private static extern int GetConfigInt( int i );
        [DllImport( "Felina" )] private static extern void GetConfigString( int i, StringBuilder s, int m );
#endif

        internal static int CHECK_INTERVAL => GetConfigInt( 0 );

        internal static string PREF_STATUS => FetchString( 1 );

        internal static string PREF_LAST_CHECK => FetchString( 2 );

        internal static string PREF_CACHE => FetchString( 3 );

        // Render / capture defaults
        public const int DEFAULT_OUTPUT_RESOLUTION = 1024;
        public const int MAX_FEED_RES = 1920;
        public const int DEFAULT_TARGET_FRAME_RATE = 60;

        // Scanner thresholds
        public const float DEFAULT_CAPTURE_THRESHOLD = 0.85f;
        public const float DEFAULT_MAX_MOVE_SPEED = 0.05f;
        public const float DEFAULT_MAX_ROTATE_SPEED = 5.0f;

        // License status values
        public const string STATUS_VALID = "VALID";
        public const string STATUS_BANNED = "BANNED";

        // Misc
        public const float EPSILON_SMALL = 0.00001f;

        private static string FetchString( int id )
        {
            _sb ??= new StringBuilder( 64 );
            _sb.Clear();
            GetConfigString( id, _sb, _sb.Capacity );
            return _sb.ToString();
        }
    }
}