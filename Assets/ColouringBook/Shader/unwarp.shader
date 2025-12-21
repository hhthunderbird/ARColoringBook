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
            float4x4 _Unwarp; 
            float4x4 _DisplayMatrix;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 targetPoint = float3(i.uv.x, i.uv.y, 1.0);
                float3 screenPointHomo = mul(_Unwarp, float4(targetPoint, 1.0)).xyz;
                if(abs(screenPointHomo.z) < 0.00001) 
                    return fixed4(0, 0, 0, 0);
                
                float2 screenPos = screenPointHomo.xy / screenPointHomo.z;

                if(screenPos.x < 0 || screenPos.x > 1 || screenPos.y < 0 || screenPos.y > 1)
                {
                    return fixed4(0, 0, 0, 1);
                }

                float2 textureCoord = mul(_DisplayMatrix, float4(screenPos.x, screenPos.y, 0.0, 1.0)).xy;

                fixed4 col = tex2D(_MainTex, textureCoord);
                
                return col;
            }
            ENDCG
        }
    }
}