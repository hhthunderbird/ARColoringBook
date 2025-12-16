using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Felina.ARColoringBook
{
    public class ARPaintableObject : MonoBehaviour
    {
        [Header( "AR Configuration" )]
        public XRReferenceImageLibrary referenceLibrary;

        [SerializeField, HideInInspector]
        private string _referenceImageName;

        [Header( "Material Settings" )]
        [Tooltip( "Material index to apply texture to. Usually 0." )]
        public int materialIndex = 0;

        [Tooltip( "The property name in your shader. For Felina Multiply, use '_DrawingTex'." )]
        public string texturePropertyName = "_DrawingTex";

        private Renderer _renderer;

        void Start()
        {
            _renderer = GetComponentInChildren<Renderer>();

            if ( string.IsNullOrEmpty( _referenceImageName ) )
            {
                Debug.LogWarning( $"[Felina] {name} has no Reference Image selected!" );
                return;
            }

            if ( ARScannerManager.Instance != null )
            {
                ARScannerManager.Instance.OnTextureCaptured += OnTextureReceived;
                var cachedTex = ARScannerManager.Instance.GetCapturedTexture( _referenceImageName );
                if ( cachedTex != null )
                {
                    OnTextureReceived( _referenceImageName, cachedTex, 1.0f );
                }
            }
        }

        private void OnDestroy()
        {
            if ( ARScannerManager.Instance != null )
            {
                ARScannerManager.Instance.OnTextureCaptured -= OnTextureReceived;
            }
        }

        private void OnTextureReceived( string targetName, RenderTexture newTexture, float quality )
        {
            if ( targetName != _referenceImageName ) return;

            if ( _renderer == null )
            {
                Debug.LogWarning( $"[Felina] ARPaintableObject: Renderer missing on '{name}' when receiving texture for '{targetName}'" );
                return;
            }

            var mats = _renderer.materials;
            if ( materialIndex < 0 || materialIndex >= mats.Length )
            {
                Debug.LogWarning( $"[Felina] ARPaintableObject: materialIndex {materialIndex} out of range (materials: {mats.Length}) on '{name}'" );
                return;
            }

            var mat = mats[ materialIndex ];

            string[] candidates = new string[] { texturePropertyName, "_BaseMap", "_MainTex" };
            bool applied = false;
            foreach ( var prop in candidates )
            {
                if ( string.IsNullOrEmpty( prop ) ) continue;
                if ( mat.HasProperty( prop ) )
                {
                    mat.SetTexture( prop, newTexture );
                    applied = true;
                    break;
                }
            }

            if ( !applied )
            {
                try
                {
                    mat.SetTexture( texturePropertyName, newTexture );
                    applied = true;
                }
                catch ( System.Exception ex )
                {
                    Debug.LogWarning( $"[Felina] ARPaintableObject: Failed to apply texture to '{name}' for '{targetName}': {ex.Message}" );
                }
            }
        }
    }
}
