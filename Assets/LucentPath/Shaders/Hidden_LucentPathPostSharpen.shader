Shader "Hidden/LucentPath/PostSharpen"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Overlay" }

        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

            TEXTURE2D(_BlitTexture);
            float _PostSharpenAmount;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings o;
                float2 uv = float2((vertexID << 1) & 2, vertexID & 2);
                o.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                return o;
            }

            float4 LoadSource(int2 pixel)
            {
                uint width;
                uint height;
                _BlitTexture.GetDimensions(width, height);
                pixel = clamp(pixel, int2(0, 0), int2((int)width - 1, (int)height - 1));
                return LOAD_TEXTURE2D(_BlitTexture, pixel);
            }

            float4 Frag(Varyings i) : SV_Target
            {
                int2 pixel = int2(i.positionCS.xy);
                float4 center = LoadSource(pixel);

                float3 neighbors =
                    LoadSource(pixel + int2(-1, 0)).rgb +
                    LoadSource(pixel + int2(1, 0)).rgb +
                    LoadSource(pixel + int2(0, -1)).rgb +
                    LoadSource(pixel + int2(0, 1)).rgb;

                float3 blur = neighbors * 0.25;
                float amount = saturate(_PostSharpenAmount);
                float3 sharpened = center.rgb + (center.rgb - blur) * amount;
                return float4(max(sharpened, 0.0), center.a);
            }
            ENDHLSL
        }
    }
}
