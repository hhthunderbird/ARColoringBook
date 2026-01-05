using UnityEngine;

namespace Felina.ARColoringBook
{
    /// <summary>
    /// Debug helper to visualize UV mapping issues.
    /// Attach to your truck model to verify UV coordinates are correct.
    /// </summary>
    [RequireComponent( typeof( MeshFilter ) )]
    public class UVDebugHelper : MonoBehaviour
    {
        [Header( "Debug Options" )]
        [SerializeField] private bool _showUVsInConsole = false;
        [SerializeField] private bool _generateUVTestTexture = true;
        [SerializeField] private int _textureSize = 1024;
        [SerializeField] private bool _checkMultiMaterialSetup = true;

        private void Start()
        {
            if ( _checkMultiMaterialSetup )
            {
                CheckMaterialSetup();
            }

            if ( _generateUVTestTexture )
            {
                GenerateUVGridTexture();
            }

            if ( _showUVsInConsole )
            {
                PrintUVCoordinates();
            }
        }

        /// <summary>
        /// Checks if this model uses multiple materials (e.g., body + tail lights)
        /// </summary>
        private void CheckMaterialSetup()
        {
            var renderer = GetComponent<Renderer>();
            if ( renderer == null ) return;

            var materials = renderer.sharedMaterials;
            Debug.Log( $"[UVDebugHelper] Model has {materials.Length} material slot(s):" );

            for ( int i = 0; i < materials.Length; i++ )
            {
                if ( materials[ i ] != null )
                {
                    Debug.Log( $"  Material[{i}]: '{materials[ i ].name}' (Shader: {materials[ i ].shader.name})" );
                }
                else
                {
                    Debug.LogWarning( $"  Material[{i}]: <NULL>" );
                }
            }

            if ( materials.Length > 1 )
            {
                Debug.Log( "[UVDebugHelper] Multi-material model detected!" );
                Debug.Log( "[UVDebugHelper] UV overflow is OK if it's on a separate material (e.g., tail lights)" );
                Debug.Log( "[UVDebugHelper] Only Material[0] should have UVs in 0-1 range if it receives AR texture" );
            }
        }

        /// <summary>
        /// Generates a UV grid texture to visualize mapping.
        /// Red = X axis, Green = Y axis
        /// </summary>
        private void GenerateUVGridTexture()
        {
            Texture2D uvTexture = new Texture2D( _textureSize, _textureSize, TextureFormat.RGB24, false );
            
            for ( int y = 0; y < _textureSize; y++ )
            {
                for ( int x = 0; x < _textureSize; x++ )
                {
                    // Normalize coordinates
                    float u = x / ( float ) _textureSize;
                    float v = y / ( float ) _textureSize;

                    // Grid lines every 10%
                    bool isGridLineU = ( x % ( _textureSize / 10 ) ) == 0;
                    bool isGridLineV = ( y % ( _textureSize / 10 ) ) == 0;

                    Color pixel;
                    if ( isGridLineU || isGridLineV )
                    {
                        pixel = Color.white; // Grid lines
                    }
                    else
                    {
                        // Color gradient: Red increases with U, Green increases with V
                        pixel = new Color( u, v, 0.2f, 1f );
                    }

                    uvTexture.SetPixel( x, y, pixel );
                }
            }

            uvTexture.Apply();

            // Apply to renderer
            var renderer = GetComponent<Renderer>();
            if ( renderer != null )
            {
                renderer.material.mainTexture = uvTexture;
                Debug.Log( "[UVDebugHelper] Applied UV test texture. Red gradient = U axis, Green gradient = V axis" );
            }
        }

        /// <summary>
        /// Prints UV coordinates to console for inspection
        /// </summary>
        private void PrintUVCoordinates()
        {
            var meshFilter = GetComponent<MeshFilter>();
            if ( meshFilter == null || meshFilter.sharedMesh == null )
            {
                Debug.LogError( "[UVDebugHelper] No mesh found!" );
                return;
            }

            var mesh = meshFilter.sharedMesh;
            var uvs = mesh.uv;

            if ( uvs == null || uvs.Length == 0 )
            {
                Debug.LogError( "[UVDebugHelper] Mesh has no UV coordinates!" );
                return;
            }

            Debug.Log( $"[UVDebugHelper] Mesh '{mesh.name}' has {uvs.Length} UV coordinates:" );

            // Show first 20 UVs
            int count = Mathf.Min( 20, uvs.Length );
            for ( int i = 0; i < count; i++ )
            {
                Debug.Log( $"  UV[{i}] = ({uvs[ i ].x:F3}, {uvs[ i ].y:F3})" );
            }

            if ( uvs.Length > 20 )
            {
                Debug.Log( $"  ... and {uvs.Length - 20} more" );
            }

            // Check UV range
            Vector2 min = new Vector2( float.MaxValue, float.MaxValue );
            Vector2 max = new Vector2( float.MinValue, float.MinValue );

            foreach ( var uv in uvs )
            {
                min.x = Mathf.Min( min.x, uv.x );
                min.y = Mathf.Min( min.y, uv.y );
                max.x = Mathf.Max( max.x, uv.x );
                max.y = Mathf.Max( max.y, uv.y );
            }

            Debug.Log( $"[UVDebugHelper] UV Range: Min=({min.x:F3}, {min.y:F3}), Max=({max.x:F3}, {max.y:F3})" );

            // Warn if UVs are outside 0-1 range
            if ( min.x < 0f || min.y < 0f || max.x > 1f || max.y > 1f )
            {
                Debug.LogWarning( "[UVDebugHelper] WARNING: UVs are outside 0-1 range! This will cause texture wrapping/tiling." );
                Debug.LogWarning( $"[UVDebugHelper] UV overflow - U: {( max.x > 1f ? "+" + ( max.x - 1f ).ToString( "F3" ) : "OK" )}, V: {( max.y > 1f ? "+" + ( max.y - 1f ).ToString( "F3" ) : "OK" )}" );
                
                // Check if this is a multi-material model
                var renderer = GetComponent<Renderer>();
                if ( renderer != null && renderer.sharedMaterials.Length > 1 )
                {
                    Debug.LogWarning( "[UVDebugHelper] MULTI-MATERIAL DETECTED: UV overflow might be intentional (e.g., tail lights)" );
                    Debug.LogWarning( "[UVDebugHelper] If overflow is on a separate material that doesn't need AR texture, this is OK!" );
                    Debug.LogWarning( "[UVDebugHelper] Use MaterialPropertyInspector to check which material gets the AR texture" );
                }
                else
                {
                    Debug.LogWarning( "[UVDebugHelper] You need to re-unwrap your model in Blender to fit 0-1 space!" );
                }
            }
            else
            {
                Debug.Log( "[UVDebugHelper] SUCCESS: All UVs are within valid 0-1 range!" );
            }
        }

#if UNITY_EDITOR
        [ContextMenu( "Generate UV Test Texture" )]
        private void DebugGenerateTexture()
        {
            GenerateUVGridTexture();
        }

        [ContextMenu( "Print UV Coordinates" )]
        private void DebugPrintUVs()
        {
            PrintUVCoordinates();
        }
#endif
    }
}
