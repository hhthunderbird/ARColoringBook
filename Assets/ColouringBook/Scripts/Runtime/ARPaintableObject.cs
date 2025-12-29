using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Felina.ARColoringBook
{
    public class ARPaintableObject : MonoBehaviour
    {
        [Header( "Material Settings" )]
        [SerializeField, Tooltip( "Material index to apply texture to. Usually 0." )]
        private int _materialIndex = 0;

        private readonly string _texturePropertyName = "_DrawingTex";
        private Renderer _renderer;
        private string _referenceImageName;

        void Start()
        {
            _renderer = GetComponentInChildren<Renderer>();

            // Auto-detect image name from ARTrackedImage parent
            var trackedImage = GetComponentInParent<ARTrackedImage>();
            if ( trackedImage != null && ARScannerManager.Instance != null )
            {
                _referenceImageName = ARScannerManager.Instance.GetImageName( trackedImage.referenceImage.guid );
                
                if ( !string.IsNullOrEmpty( _referenceImageName ) )
                {
                    ARScannerManager.Instance.OnTextureCaptured += OnTextureReceived;
                    
                    // Check for existing texture (late join)
                    var cachedTex = ARScannerManager.Instance.GetCapturedTexture( _referenceImageName );
                    if ( cachedTex != null )
                    {
                        OnTextureReceived( _referenceImageName, cachedTex, 1.0f );
                    }
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
                Debug.LogWarning( $"[Felina] ARPaintableObject: Renderer is null on '{gameObject.name}'" );
                return;
            }
            
            // Get the material to check shader properties
            var sharedMats = _renderer.sharedMaterials;
            if ( _materialIndex < 0 || _materialIndex >= sharedMats.Length )
            {
                Debug.LogWarning( $"[Felina] ARPaintableObject: Material index {_materialIndex} out of range on '{gameObject.name}'" );
                return;
            }
            
            var mat = sharedMats[ _materialIndex ];
            if ( mat == null )
            {
                Debug.LogWarning( $"[Felina] ARPaintableObject: Material at index {_materialIndex} is null on '{gameObject.name}'" );
                return;
            }

            // Use MaterialPropertyBlock to avoid creating material instances
            var mpb = new MaterialPropertyBlock();
            _renderer.GetPropertyBlock( mpb, _materialIndex );

            // Try candidate property names in order of preference
            string[] candidates = { _texturePropertyName, "_BaseMap", "_MainTex", "_DrawingTex" };
            bool textureSet = false;
            
            foreach ( var propName in candidates )
            {
                if ( string.IsNullOrEmpty( propName ) ) continue;
                
                if ( mat.HasProperty( propName ) )
                {
                    mpb.SetTexture( propName, newTexture );
                    textureSet = true;
                    // Debug.Log( $"[Felina] ARPaintableObject: Set texture on property '{propName}' for '{gameObject.name}'" );
                    break;
                }
            }
            
            if ( !textureSet )
            {
                Debug.LogWarning( $"[Felina] ARPaintableObject: None of the texture properties found on material '{mat.name}'. Available properties: {string.Join( ", ", GetShaderPropertyNames( mat ) )}" );
                // Set anyway as fallback
                mpb.SetTexture( _texturePropertyName, newTexture );
            }

            // CRITICAL FIX: Ensure the material's base color/tint is white (not black)
            // This is often why objects appear black - the color multiplier is set to black
            if ( mat.HasProperty( "_Color" ) )
            {
                mpb.SetColor( "_Color", Color.white );
            }
            if ( mat.HasProperty( "_BaseColor" ) )
            {
                mpb.SetColor( "_BaseColor", Color.white );
            }
            if ( mat.HasProperty( "_TintColor" ) )
            {
                mpb.SetColor( "_TintColor", Color.white );
            }

            // Apply the property block
            _renderer.SetPropertyBlock( mpb, _materialIndex );
            
            // Debug.Log( $"[Felina] ARPaintableObject: Successfully applied texture for '{targetName}' on '{gameObject.name}'" );
        }

        // Helper method to get shader property names for debugging
        private string[] GetShaderPropertyNames( Material mat )
        {
            var shader = mat.shader;
            int propCount = shader.GetPropertyCount();
            var propNames = new string[ propCount ];
            
            for ( int i = 0; i < propCount; i++ )
            {
                propNames[ i ] = shader.GetPropertyName( i );
            }
            
            return propNames;
        }
    }
}
