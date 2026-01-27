Shader "Felina/AR/XRCpuImageRotate"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _RotationType ("Rotation Type (0=0°, 1=90°, 2=180°, 3=270°, 4=arbitrary)", Int) = 0
        _RotationAngle ("Rotation Angle (radians)", Float) = 0
        _SrcSize ("Source Size (width, height)", Vector) = (0, 0, 0, 0)
        _DstSize ("Destination Size (width, height)", Vector) = (0, 0, 0, 0)
    }
    
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        
        Pass
        {
            Name "XRCpuImageRotate"
            ZTest Always
            Cull Off
            ZWrite Off
            Blend Off
            
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            int _RotationType;        // 0: 0°, 1: 90° CW, 2: 180°, 3: 270° CW, 4: arbitrary
            float _RotationAngle;
            float4 _SrcSize;         // x: src.width, y: src.height
            float4 _DstSize;         // x: dst.width, y: dst.height
            
            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 pixelPos : TEXCOORD1; // Pixel position in destination texture
            };
            
            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS);
                OUT.uv = IN.uv;
                
                // Calculate pixel position in destination texture
                OUT.pixelPos = float2(
                    floor(OUT.uv.x * _DstSize.x + 0.5), // Add 0.5 for pixel center
                    floor(OUT.uv.y * _DstSize.y + 0.5)
                );
                
                return OUT;
            }
            
            // Get source pixel color without interpolation (exact pixel copy)
            half4 GetSourcePixel(float2 srcPixelPos)
            {
                // Clamp to source texture bounds
                srcPixelPos.x = clamp(srcPixelPos.x, 0, _SrcSize.x - 1);
                srcPixelPos.y = clamp(srcPixelPos.y, 0, _SrcSize.y - 1);
                
                // Convert to UV coordinates (pixel center)
                float2 uv = float2(
                    (srcPixelPos.x + 0.5) / _SrcSize.x,
                    (srcPixelPos.y + 0.5) / _SrcSize.y
                );
                
                // Note: MirrorY is already applied in the C# conversion,
                // so we don't need to do it here
                return SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv, 0);
            }
            
            half4 Frag(Varyings IN) : SV_Target
            {
                // Convert to integer pixel coordinates
                int dstX = (int)IN.pixelPos.x;
                int dstY = (int)IN.pixelPos.y;
                
                // Ensure we're within bounds
                if (dstX < 0 || dstX >= _DstSize.x || dstY < 0 || dstY >= _DstSize.y)
                {
                    return half4(0, 0, 0, 0);
                }
                
                float2 srcPixelPos;
                int srcW = (int)_SrcSize.x;
                int srcH = (int)_SrcSize.y;
                
                if (_RotationType == 1) // 90° CW (rotationAngleRad = -π/2)
                {
                    // Inverse of: dstX = (srcH - 1) - y; dstY = x;
                    int y = (srcH - 1) - dstX;
                    int x = dstY;
                    srcPixelPos = float2(x, y);
                }
                else if (_RotationType == 2) // 180°
                {
                    // Inverse of: dstX = (srcW - 1) - x; dstY = (srcH - 1) - y;
                    int x = (srcW - 1) - dstX;
                    int y = (srcH - 1) - dstY;
                    srcPixelPos = float2(x, y);
                }
                else if (_RotationType == 3) // 270° CW (rotationAngleRad = π/2)
                {
                    // Inverse of: dstX = y; dstY = (srcW - 1) - x;
                    int x = (srcW - 1) - dstY;
                    int y = dstX;
                    srcPixelPos = float2(x, y);
                }
                else if (_RotationType == 4) // Arbitrary angle
                {
                    // Handle arbitrary rotation (same as 180° in C# code)
                    // This matches the else block in C# code
                    int x = (srcW - 1) - dstX;
                    int y = (srcH - 1) - dstY;
                    srcPixelPos = float2(x, y);
                }
                else // 0° (no rotation)
                {
                    srcPixelPos = float2(dstX, dstY);
                }
                
                return GetSourcePixel(srcPixelPos);
            }
            ENDHLSL
        }
    }
    
    FallBack Off
}