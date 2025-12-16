Shader "Felina/AR/URP_Multiply"
{
    Properties
    {
        _BaseMap ("Base Texture (Shadows/AO)", 2D) = "white" {}
        _DrawingTex ("Drawing (Captured)", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            // Texture Declarations
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            
            TEXTURE2D(_DrawingTex);
            SAMPLER(sampler_DrawingTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _Color;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 1. Sample the baked 3D model texture (Shadows/Details)
                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                
                // 2. Sample the captured drawing
                half4 drawingColor = SAMPLE_TEXTURE2D(_DrawingTex, sampler_DrawingTex, input.uv);

                // 3. MULTIPLY them together
                // This keeps the shadows of the Base Texture while coloring it with the Drawing.
                return baseColor * drawingColor * _Color;
            }
            ENDHLSL
        }
    }
}