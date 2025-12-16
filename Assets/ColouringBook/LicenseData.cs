using System;

namespace Felina.ARColoringBook
{
    [Serializable]
    public class LicenseData
    {
        // Must match the Firebase keys exactly (case-sensitive)
        public string status;     // "VALID_STATUS_2025"
        public string key;        // The hashed key
        public string updatedAt;  // The date
    }
}