using Felina.ARColoringBook;
using System;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Implements Pre and Post processing to secure the invoice
public class BuildEnforcer : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    // Import the C++ Encryptor
    [DllImport( "Felina" )] private static extern void EncryptInvoiceString( string i, byte[] o, out int l );

    // Temporary storage to restore the invoice after build
    private static string _tempInvoiceStorage;

    public void OnPreprocessBuild( BuildReport report )
    {
        Debug.Log( "<color=cyan>[Felina] Securing Assets for Build...</color>" );

        if ( Settings.Instance == null )
            throw new BuildFailedException( "[Felina] Settings file not found in Resources!" );

        // 1. Validation
        string plainInvoice = Settings.Instance.InvoiceNumber;
        if ( string.IsNullOrEmpty( plainInvoice ) )
            throw new BuildFailedException( "[Felina] Invoice Number is empty!" );

        // 2. Encrypt to Bytes (Using C++ DLL)
        try
        {
            var buffer = new byte[ plainInvoice.Length + 1 ];
            
            EncryptInvoiceString( plainInvoice, buffer, out var len );

            // Trim and Save
            var finalBytes = new byte[ len ];
            Array.Copy( buffer, finalBytes, len );
            Settings.Instance.EncryptedInvoice = finalBytes;
        }
        catch ( Exception e )
        {
            throw new BuildFailedException( $"[Felina] Encryption Failed. Is 'Felina.dll' present? Error: {e.Message}" );
        }

        // 3. WIPE PLAIN TEXT
        // We store it in memory to restore later, but remove it from the asset
        _tempInvoiceStorage = plainInvoice;
        Settings.Instance.InvoiceNumber = ""; // <--- Wipe!

        EditorUtility.SetDirty( Settings.Instance );
        AssetDatabase.SaveAssets();
    }

    public void OnPostprocessBuild( BuildReport report )
    {
        // 4. RESTORE PLAIN TEXT
        // Puts the invoice back so the developer can keep working
        if ( !string.IsNullOrEmpty( _tempInvoiceStorage ) )
        {
            Settings.Instance.InvoiceNumber = _tempInvoiceStorage;
            _tempInvoiceStorage = "";

            EditorUtility.SetDirty( Settings.Instance );
            AssetDatabase.SaveAssets();
            Debug.Log( "<color=cyan>[Felina] Build Complete. Invoice Restored.</color>" );
        }
    }
}