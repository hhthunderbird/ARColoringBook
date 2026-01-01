Shader "Felina/AR/Unwarp"
{
    Properties
    {
        _MainTex ("Camera Feed", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            ZTest Always 
            Cull Off 
            ZWrite Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            
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

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            float4x4 _Unwarp; 
            float4x4 _DisplayMatrix;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // High-quality bicubic texture sampling for better image quality
            float4 tex2D_bicubic(sampler2D tex, float2 uv)
            {
                float2 texSize = _MainTex_TexelSize.zw;
                float2 invTexSize = _MainTex_TexelSize.xy;
                
                uv = uv * texSize - 0.5;
                float2 fxy = frac(uv);
                uv -= fxy;

                float4 xcubic = float4(fxy.x * fxy.x * fxy.x, fxy.x * fxy.x, fxy.x, 1.0);
                float4 ycubic = float4(fxy.y * fxy.y * fxy.y, fxy.y * fxy.y, fxy.y, 1.0);
                
                float4 c = float4(1.0, 0.0, -3.0, 2.0);
                float4 s = float4(-0.5, 1.5, -1.5, 0.5);
                
                float4 wx = c + xcubic * s;
                float4 wy = c + ycubic * s;
                
                float4 col = float4(0, 0, 0, 0);
                for(int y = -1; y <= 2; y++)
                {
                    float fy = wy[y + 1];
                    for(int x = -1; x <= 2; x++)
                    {
                        float fx = wx[x + 1];
                        float2 offset = float2(x, y);
                        float2 sampleUV = (uv + offset + 0.5) * invTexSize;
                        col += tex2Dlod(_MainTex, float4(sampleUV, 0, 0)) * fx * fy;
                    }
                }
                return col;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Use higher precision for homography calculations
                float3 targetPoint = float3(i.uv.x, i.uv.y, 1.0);
                float3 screenPointHomo = mul(_Unwarp, float4(targetPoint, 1.0)).xyz;
                
                // More robust divide-by-zero check
                if(abs(screenPointHomo.z) < 0.00001) 
                    return fixed4(1, 0, 1, 1);  // Magenta for debugging invalid pixels
                
                float2 screenPos = screenPointHomo.xy / screenPointHomo.z;

                // Check if coordinates are out of bounds
                if(screenPos.x < 0 || screenPos.x > 1 || screenPos.y < 0 || screenPos.y > 1)
                {
                    return fixed4(0, 1, 1, 1);  // Cyan for out of bounds
                }

                float2 textureCoord = mul(_DisplayMatrix, float4(screenPos.x, screenPos.y, 0.0, 1.0)).xy;

                // Validate final texture coordinates
                if(textureCoord.x < 0 || textureCoord.x > 1 || textureCoord.y < 0 || textureCoord.y > 1)
                {
                    return fixed4(1, 1, 0, 1);  // Yellow for invalid texture coords
                }

                // Use simple tex2D first to debug - bicubic might be the problem!
                fixed4 col = tex2D(_MainTex, textureCoord);
                
                //fixed4 col = tex2D_bicubic(_MainTex, textureCoord);

                // 2. ADD Manual Gamma Correction (Fixes the "Bad" Dark/Muddy Quality)
                // This converts Linear data to sRGB, matching how your eyes (and PNGs) expect color.
                // 0.4545 is roughly 1.0 / 2.2
                //col.rgb = pow(col.rgb, 0.4545);

                return col;
            }
            ENDCG
        }
    }
}