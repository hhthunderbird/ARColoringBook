Shader "Felina/AR/HomographyUnwarp"
{
    Properties
    {
        // This property is usually set automatically by Graphics.Blit source
        // If we Blit(null, dest, mat), we must set this manually from C#
        _MainTex ("Camera Feed", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            // Standard Blit setup
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
            
            // The 3x3 Homography Matrix (packed into 4x4)
            // Maps: Output UV (0..1) --> Normalized Screen Coordinates (0..1)
            float4x4 _Homography; 

            // ARFoundation Display Matrix
            // Maps: Normalized Screen Coordinates (0..1) --> Camera Texture UV (0..1)
            // Handles rotation (portrait/landscape) and aspect fitting.
            float4x4 _DisplayMatrix;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv; // Pass through the quad UVs (0,0 to 1,1)
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 1. Construct Homogeneous Source Point
                // i.uv is the coordinate in the UNWARPED texture we are generating.
                float3 targetPoint = float3(i.uv.x, i.uv.y, 1.0);
                
                // 2. Apply Homography
                // H was calculated to map Source -> Screen.
                // Note on multiplication order: Unity matrices are column-major in C#, 
                // but usually HLSL treats mul(matrix, vector) as row-transform if not careful.
                // Our C# solver created H such that H * vector works.
                float3 screenPointHomogeneous = mul(_Homography, float4(targetPoint, 1.0)).xyz;

                // 3. Perspective Divide
                // Convert homogeneous coordinates back to Euclidean 2D space.
                // Guard against division by zero (points at infinity).
                if(abs(screenPointHomogeneous.z) < 0.00001) 
                    return fixed4(0, 0, 0, 0); // Transparent/Black if invalid
                
                float2 screenPos = screenPointHomogeneous.xy / screenPointHomogeneous.z;

                // 4. Bounds Checking
                // If the mapped point is outside the screen, it means this part of the 
                // marker is off-camera.
                if(screenPos.x < 0 || screenPos.x > 1 || screenPos.y < 0 || screenPos.y > 1)
                {
                    return fixed4(0, 0, 0, 1); // Return black
                }

                // 5. Apply Display Matrix Correction
                // The screenPos is 0..1 relative to the device screen.
                // The camera texture might be rotated or scaled differently.
                // The DisplayMatrix handles this transform.
                // We treat screenPos as a point (z=1, w=0 technically for affine, but mul logic varies)
                // Treat as a point (z=0, w=1) so translation in the 4x4 matrix is applied
                float2 textureCoord = mul(_DisplayMatrix, float4(screenPos.x, screenPos.y, 0.0, 1.0)).xy;

                // 6. Sample the Camera Feed
                fixed4 col = tex2D(_MainTex, textureCoord);
                
                return col;
            }
            ENDCG
        }
    }
}