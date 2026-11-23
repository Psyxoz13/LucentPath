Shader "Hidden/LucentPath/Composite"
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

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings o;
                o.positionCS = GetFullScreenTriangleVertexPosition(vertexID);
                return o;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                int2 pixel = int2(i.positionCS.xy);
                return LOAD_TEXTURE2D(_BlitTexture, pixel);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ScaledCopy"

            ZWrite Off
            ZTest Always
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

            TEXTURE2D(_BlitTexture);
            SAMPLER(sampler_BlitTexture);
            float4 _BlitScaleBias;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings o;
                o.uv = float2((vertexID << 1) & 2, vertexID & 2) * _BlitScaleBias.xy + _BlitScaleBias.zw;
                o.positionCS = float4(float2((vertexID << 1) & 2, vertexID & 2) * 2.0 - 1.0, 0.0, 1.0);
                return o;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                return SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_BlitTexture, i.uv, 0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Tonemap"

            ZWrite Off
            ZTest Always
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            TEXTURE2D(_BlitTexture);
            SAMPLER(sampler_BlitTexture);
            TEXTURE2D(_AgxDefaultContrastLut);
            SAMPLER(sampler_AgxDefaultContrastLut);

            float4 _BlitScaleBias;
            float _AgxDefaultContrastLutSize;
            float _PresentationFlipY;
            int _TonemapMode;
            int _AgxLook;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings o;
                o.uv = float2((vertexID << 1) & 2, vertexID & 2) * _BlitScaleBias.xy + _BlitScaleBias.zw;
                o.uv.y = lerp(o.uv.y, 1.0 - o.uv.y, _PresentationFlipY);
                o.positionCS = float4(float2((vertexID << 1) & 2, vertexID & 2) * 2.0 - 1.0, 0.0, 1.0);
                return o;
            }

            float3 AgxInsetTransform(float3 color)
            {
                return float3(
                    dot(color, float3(0.842479062253094, 0.0784335999999992, 0.0792237451477643)),
                    dot(color, float3(0.0423282422610123, 0.878468636469772, 0.0791661274605434)),
                    dot(color, float3(0.0423756549057051, 0.0784336, 0.879142973793104)));
            }

            float3 AgxLogEncode(float3 color)
            {
                const float minEv = -12.47393;
                const float maxEv = 4.026069;
                color = max(color, 1e-10);
                return saturate((log2(color) - minEv) / (maxEv - minEv));
            }

            float3 SampleAgxDefaultContrast(float3 encoded)
            {
                float invSize = rcp(max(_AgxDefaultContrastLutSize, 1.0));
                float3 uv = encoded * ((_AgxDefaultContrastLutSize - 1.0) * invSize) + 0.5 * invSize;
                return float3(
                    SAMPLE_TEXTURE2D_LOD(_AgxDefaultContrastLut, sampler_AgxDefaultContrastLut, float2(uv.r, 0.5), 0).r,
                    SAMPLE_TEXTURE2D_LOD(_AgxDefaultContrastLut, sampler_AgxDefaultContrastLut, float2(uv.g, 0.5), 0).r,
                    SAMPLE_TEXTURE2D_LOD(_AgxDefaultContrastLut, sampler_AgxDefaultContrastLut, float2(uv.b, 0.5), 0).r);
            }

            float3 ApplyCdlLook(float3 color, float3 slope, float3 power, float saturation)
            {
                color = pow(max(color * slope, 0.0), power);
                float luma = dot(color, float3(0.2126, 0.7152, 0.0722));
                return saturate(luma + saturation * (color - luma));
            }

            float3 ApplyAgxLook(float3 color)
            {
                if (_AgxLook == 1)
                    return ApplyCdlLook(color, float3(1.0, 0.9, 0.5), float3(0.8, 0.8, 0.8), 1.3);

                if (_AgxLook == 2)
                    return ApplyCdlLook(color, float3(1.0, 1.0, 1.0), float3(1.35, 1.35, 1.35), 1.4);

                return saturate(color);
            }

            float3 AgxDisplayEncodedToUnityOutput(float3 color)
            {
                color = saturate(color);

            #if defined(UNITY_COLORSPACE_GAMMA)
                return color;
            #else
                return PositivePow(color, 2.2);
            #endif
            }

            float3 KhronosPbrNeutralTonemap(float3 color)
            {
                color = max(color, 0.0);

                const float startCompression = 0.8 - 0.04;
                const float desaturation = 0.15;
                float x = min(color.r, min(color.g, color.b));
                float offset = x < 0.08 ? x - 6.25 * x * x : 0.04;
                color -= offset;

                float peak = max(color.r, max(color.g, color.b));
                if (peak < startCompression)
                    return saturate(color);

                const float d = 1.0 - startCompression;
                float newPeak = 1.0 - d * d / (peak + d - startCompression);
                color *= newPeak / peak;
                float g = 1.0 - 1.0 / (desaturation * (peak - newPeak) + 1.0);
                return saturate(lerp(color, float3(newPeak, newPeak, newPeak), g));
            }

            float3 ApplyDocumentedTonemap(float3 color)
            {
                if (_TonemapMode == 1)
                {
                    float3 agx = AgxInsetTransform(color);
                    agx = AgxLogEncode(agx);
                    agx = SampleAgxDefaultContrast(agx);
                    agx = ApplyAgxLook(agx);
                    return AgxDisplayEncodedToUnityOutput(agx);
                }

                if (_TonemapMode == 2)
                    return saturate(AcesTonemap(unity_to_ACES(max(color, 0.0))));

                if (_TonemapMode == 3)
                    return saturate(NeutralTonemap(color));

                if (_TonemapMode == 4)
                    return KhronosPbrNeutralTonemap(color);

                return color;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                float4 source = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_BlitTexture, i.uv, 0);
                return float4(ApplyDocumentedTonemap(source.rgb), source.a);
            }
            ENDHLSL
        }
    }
}
