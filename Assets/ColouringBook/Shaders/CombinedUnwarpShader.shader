Shader "Felina/CombinedUnwarpShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
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
            
            sampler2D _MainTex;
            float4x4 _Unwarp;
            float4x4 _DisplayMatrix;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                
                // Apply homography transform
                float3 uvH = float3(v.uv.x, v.uv.y, 1.0);
                float3 transformed = mul((float3x3)_Unwarp, uvH);
                float2 unwarpedUV = transformed.xy / transformed.z;
                float4 finalUV = mul(_DisplayMatrix, float4(unwarpedUV, 0, 1));
                
                o.uv = finalUV.xy;
                
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = saturate(i.uv);
                fixed4 col = tex2D(_MainTex, uv);
                return col;
            }
            ENDCG
        }
    }
    
    FallBack "Diffuse"
}
