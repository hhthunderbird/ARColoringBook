using UnityEngine;

namespace Felina.ARColoringBook
{
    /// <summary>
    /// Runtime inspector for material properties.
    /// Helps debug which texture slots are available for AR texture application.
    /// </summary>
    [RequireComponent( typeof( Renderer ) )]
    public class MaterialPropertyInspector : MonoBehaviour
    {
        [Header( "Inspector" )]
        [SerializeField] private bool _logOnStart = true;
        [SerializeField] private int _materialIndex = 0;

        private void Start()
        {
            if ( _logOnStart )
            {
                InspectMaterial();
            }
        }

        [ContextMenu( "Inspect Material Properties" )]
        public void InspectMaterial()
        {
            var renderer = GetComponent<Renderer>();
            if ( renderer == null )
            {
                Debug.LogError( "[MaterialInspector] No Renderer found!" );
                return;
            }

            var materials = renderer.sharedMaterials;
            if ( _materialIndex < 0 || _materialIndex >= materials.Length )
            {
                Debug.LogError( $"[MaterialInspector] Material index {_materialIndex} out of range (0-{materials.Length - 1})" );
                return;
            }

            var mat = materials[ _materialIndex ];
            if ( mat == null )
            {
                Debug.LogError( $"[MaterialInspector] Material at index {_materialIndex} is null!" );
                return;
            }

            Debug.Log( $"=== Material Inspector: '{mat.name}' ===" );
            Debug.Log( $"Shader: {mat.shader.name}" );

            var textureProps = new[] { "_BaseMap", "_MainTex", "_DrawingTex", "_EmissionMap", "_BumpMap" };
            
            Debug.Log( "\n--- Texture Properties ---" );
            foreach ( var propName in textureProps )
            {
                if ( mat.HasProperty( propName ) )
                {
                    var tex = mat.GetTexture( propName );
                    if ( tex != null )
                    {
                        Debug.Log( $"? {propName}: {tex.name} ({tex.width}x{tex.height})" );
                    }
                    else
                    {
                        Debug.Log( $"?? {propName}: <NULL> (slot exists but no texture assigned)" );
                    }
                }
            }

            // Check color properties
            var colorProps = new[] { "_Color", "_BaseColor", "_TintColor", "_EmissionColor" };
            
            Debug.Log( "\n--- Color Properties ---" );
            foreach ( var propName in colorProps )
            {
                if ( mat.HasProperty( propName ) )
                {
                    var color = mat.GetColor( propName );
                    Debug.Log( $"? {propName}: {color}" );
                }
            }

            // List ALL shader properties (for debugging)
            Debug.Log( "\n--- All Shader Properties ---" );
            int propertyCount = mat.shader.GetPropertyCount();
            for ( int i = 0; i < propertyCount; i++ )
            {
                var propName = mat.shader.GetPropertyName( i );
                var propType = mat.shader.GetPropertyType( i );
                Debug.Log( $"  [{i}] {propName} ({propType})" );
            }

            Debug.Log( "==================================\n" );
        }

        /// <summary>
        /// Test if ARContentSpawner can apply textures to this material
        /// </summary>
        [ContextMenu( "Test ARContentSpawner Compatibility" )]
        public void TestARCompatibility()
        {
            var renderer = GetComponent<Renderer>();
            if ( renderer == null ) return;

            var materials = renderer.sharedMaterials;
            if ( _materialIndex >= materials.Length ) return;

            var mat = materials[ _materialIndex ];
            if ( mat == null ) return;

            Debug.Log( $"=== AR Compatibility Test: '{mat.name}' ===" );

            // These are the properties ARContentSpawner looks for
            var candidates = new[] { "_BaseMap", "_MainTex", "_DrawingTex" };

            bool foundSlot = false;
            foreach ( var propName in candidates )
            {
                if ( mat.HasProperty( propName ) )
                {
                    Debug.Log( $"? COMPATIBLE: Material has '{propName}' property" );
                    foundSlot = true;
                    break;
                }
            }

            if ( !foundSlot )
            {
                Debug.LogError( "? INCOMPATIBLE: Material doesn't have _BaseMap, _MainTex, or _DrawingTex!" );
                Debug.LogError( "   ARContentSpawner won't be able to apply textures to this material." );
                Debug.LogError( "   Solution: Change shader or add a custom texture property." );
            }

            // Check color properties
            var colorCandidates = new[] { "_Color", "_BaseColor", "_TintColor" };
            bool hasColorProp = false;
            foreach ( var propName in colorCandidates )
            {
                if ( mat.HasProperty( propName ) )
                {
                    hasColorProp = true;
                    break;
                }
            }

            if ( !hasColorProp )
            {
                Debug.LogWarning( "?? WARNING: Material doesn't have _Color, _BaseColor, or _TintColor" );
                Debug.LogWarning( "   ARContentSpawner sets these to white - texture might appear too bright/dark" );
            }

            Debug.Log( "==================================\n" );
        }
    }
}
