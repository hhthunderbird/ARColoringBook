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
        [DllImport( "Felina" )] internal static extern bool IsLicenseCheckDue( long lastCheck, long now );
        [DllImport( "Felina" )] private static extern int GetConfigInt( int i );
        [DllImport( "Felina" )] private static extern void GetConfigString( int i, StringBuilder s, int m );

        internal static int CHECK_INTERVAL => GetConfigInt( 0 );

        internal static string PREF_STATUS => FetchString( 1 );

        internal static string PREF_LAST_CHECK => FetchString( 2 );

        internal static string PREF_CACHE => FetchString( 3 );

        private static string FetchString( int id )
        {
            _sb ??= new StringBuilder( 64 );
            _sb.Clear();
            GetConfigString( id, _sb, _sb.Capacity );
            return _sb.ToString();
        }
    }
}