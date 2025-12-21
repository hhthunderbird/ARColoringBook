using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Felina.ARColoringBook
{
    [RequireComponent( typeof( Renderer ) )]
    public class ARPaintableObject : MonoBehaviour
    {
        [Header( "AR Configuration" )]
        public XRReferenceImageLibrary referenceLibrary;

        [HideInInspector]
        public string referenceImageName;

        [Header( "Material Settings" )]
        [Tooltip( "Material index to apply texture to. Usually 0." )]
        public int materialIndex = 0;

        [Tooltip( "The property name in your shader. For Felina Multiply, use '_DrawingTex'." )]
        public string texturePropertyName = "_DrawingTex";

        private Renderer _renderer;

        void Start()
        {
            _renderer = GetComponent<Renderer>();

            if ( string.IsNullOrEmpty( referenceImageName ) )
            {
                Debug.LogWarning( $"[Felina] {name} has no Reference Image selected!" );
                return;
            }

            if ( ARScannerManager.Instance != null )
            {
                // 1. Subscribe to future updates
                ARScannerManager.Instance.OnTextureCaptured += OnTextureReceived;

                // --- FIX: CHECK IF TEXTURE ALREADY EXISTS (Late Join) ---
                Texture2D cachedTex = ARScannerManager.Instance.GetCapturedTexture( referenceImageName );
                if ( cachedTex != null )
                {
                    // Apply it immediately!
                    OnTextureReceived( referenceImageName, cachedTex, 1.0f );
                }
                // -------------------------------------------------------
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
            if ( targetName != referenceImageName ) return;

            if ( _renderer != null && materialIndex < _renderer.materials.Length )
            {
                _renderer.materials[ materialIndex ].SetTexture( texturePropertyName, newTexture );
            }
        }
    }
}