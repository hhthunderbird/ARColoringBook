using UnityEngine;

namespace Felina.ARColoringBook
{
    public enum LicenseMode { Development, Production }

    [CreateAssetMenu( fileName = "Settings", menuName = "Felina/Create Settings", order = 1 )]
    public class Settings : ScriptableObject
    {
        private static Settings _instance;
        public static Settings Instance
        {
            get
            {
                if ( _instance == null ) _instance = Resources.Load<Settings>( "Settings" );
                return _instance;
            }
        }

        [Header( "License Credentials" )]
        public string InvoiceNumber; 

        [HideInInspector]
        public byte[] EncryptedInvoice;

        [Header( "Build Configuration" )]
        public LicenseMode BuildMode = LicenseMode.Development;
    }
}