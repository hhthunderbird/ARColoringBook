Shader "Felina/AR/ARColoringComposite"
{
    Properties
    {
        _MainTex ("Captured Camera Feed", 2D) = "white" {}
        _RefTex ("Original Reference Marker", 2D) = "white" {} // The digital PNG
        
        // Clean up dirty paper shadows
        _WhitePoint ("Paper White Threshold", Range(0.5, 1.0)) = 0.75 
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex; // The Unwarped Camera Capture
            sampler2D _RefTex;  // The Perfect Digital Image
            float _WhitePoint;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 1. Sample the Camera Capture (User's Coloring)
                fixed4 col = tex2D(_MainTex, i.uv);
                
                // 2. Sample the Reference Image (Ground Truth)
                fixed4 ref = tex2D(_RefTex, i.uv);

                // 3. CLEANUP: Brighten the camera feed to remove shadows
                // Anything brighter than _WhitePoint becomes pure white
                col.rgb = smoothstep(0.1, _WhitePoint, col.rgb);

                // 4. ALIGNMENT TRICK: Multiply them!
                // Capture(Color) * Reference(White) = Color (User's Drawing)
                // Capture(Any)   * Reference(Black) = Black (Perfect Outline)
                // This forces the "lines" to be where the digital reference says they are,
                // hiding any slight sliding of the camera feed.
                
                return col * ref;
            }
            ENDCG
        }
    }
}