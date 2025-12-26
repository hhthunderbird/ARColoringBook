using UnityEngine;

namespace Felina.ARColoringBook
{
    public class ARPaintableObject : MonoBehaviour
    {
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
                //Debug.LogWarning( $"[Felina] {name} has no Reference Image selected!" );
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

            // Use MaterialPropertyBlock to avoid creating material instances at runtime.
            var mpb = new MaterialPropertyBlock();
            _renderer.GetPropertyBlock( mpb );

            // Try candidate property names and set the first that exists on the renderer's material.
            string[] candidates = new string[] { texturePropertyName, "_BaseMap", "_MainTex" };
            foreach ( var prop in candidates )
            {
                if ( string.IsNullOrEmpty( prop ) ) continue;
                // We can't call HasProperty on MaterialPropertyBlock, so check the shared material instead
                var sharedMats = _renderer.sharedMaterials;
                if ( materialIndex < 0 || materialIndex >= sharedMats.Length )
                {
                    // Invalid index
                    return;
                }
                var sharedMat = sharedMats[ materialIndex ];
                if ( sharedMat != null && sharedMat.HasProperty( prop ) )
                {
                    mpb.SetTexture( prop, newTexture );
                    _renderer.SetPropertyBlock( mpb );
                    return;
                }
            }

            // Fallback: set to requested property name without checks
            mpb.SetTexture( texturePropertyName, newTexture );
            _renderer.SetPropertyBlock( mpb );
        }
    }
}
