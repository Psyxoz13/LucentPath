Shader "Hidden/LucentPath/Bloom"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Overlay" }

        Pass
        {
            Name "PrefilterDownsample"

            ZWrite Off
            ZTest Always
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragPrefilterDownsample
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

            TEXTURE2D(_BlitTexture);
            SAMPLER(sampler_BlitTexture);
            float4 _BlitTexture_TexelSize;
            float4 _BlitScaleBias;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings o;
                o.uv = GetFullScreenTriangleTexCoord(vertexID) * _BlitScaleBias.xy + _BlitScaleBias.zw;
                o.positionCS = GetFullScreenTriangleVertexPosition(vertexID);
                return o;
            }

            float3 SampleSource(float2 uv, float2 offset)
            {
                return SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_BlitTexture, uv + offset * _BlitTexture_TexelSize.xy, 0).rgb;
            }

            float3 Downsample13(float2 uv)
            {
                float3 a = SampleSource(uv, float2(-1.0, -1.0));
                float3 b = SampleSource(uv, float2( 0.0, -1.0));
                float3 c = SampleSource(uv, float2( 1.0, -1.0));
                float3 d = SampleSource(uv, float2(-0.5, -0.5));
                float3 e = SampleSource(uv, float2( 0.5, -0.5));
                float3 f = SampleSource(uv, float2(-1.0,  0.0));
                float3 g = SampleSource(uv, float2( 0.0,  0.0));
                float3 h = SampleSource(uv, float2( 1.0,  0.0));
                float3 i = SampleSource(uv, float2(-0.5,  0.5));
                float3 j = SampleSource(uv, float2( 0.5,  0.5));
                float3 k = SampleSource(uv, float2(-1.0,  1.0));
                float3 l = SampleSource(uv, float2( 0.0,  1.0));
                float3 m = SampleSource(uv, float2( 1.0,  1.0));

                float2 div = (1.0 / 4.0) * float2(0.5, 0.125);
                float3 color = (d + e + i + j) * div.x;
                color += (a + b + g + f) * div.y;
                color += (b + c + h + g) * div.y;
                color += (f + g + l + k) * div.y;
                color += (g + h + m + l) * div.y;
                return max(color, 0.0);
            }

            float4 FragPrefilterDownsample(Varyings i) : SV_Target
            {
                return float4(Downsample13(i.uv), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Downsample"

            ZWrite Off
            ZTest Always
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDownsample
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

            TEXTURE2D(_BlitTexture);
            SAMPLER(sampler_BlitTexture);
            float4 _BlitTexture_TexelSize;
            float4 _BlitScaleBias;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings o;
                o.uv = GetFullScreenTriangleTexCoord(vertexID) * _BlitScaleBias.xy + _BlitScaleBias.zw;
                o.positionCS = GetFullScreenTriangleVertexPosition(vertexID);
                return o;
            }

            float3 SampleSource(float2 uv, float2 offset)
            {
                return SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_BlitTexture, uv + offset * _BlitTexture_TexelSize.xy, 0).rgb;
            }

            float4 FragDownsample(Varyings i) : SV_Target
            {
                float3 color = SampleSource(i.uv, float2( 0.0,  0.0)) * 0.25;
                color += SampleSource(i.uv, float2( 0.5,  0.5)) * 0.1875;
                color += SampleSource(i.uv, float2(-0.5,  0.5)) * 0.1875;
                color += SampleSource(i.uv, float2(-0.5, -0.5)) * 0.1875;
                color += SampleSource(i.uv, float2( 0.5, -0.5)) * 0.1875;
                return float4(max(color, 0.0), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Seed"

            ZWrite Off
            ZTest Always
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragSeed
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

            TEXTURE2D(_BlitTexture);
            SAMPLER(sampler_BlitTexture);
            float4 _BlitScaleBias;
            float4 _BloomBandWeight;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings o;
                o.uv = GetFullScreenTriangleTexCoord(vertexID) * _BlitScaleBias.xy + _BlitScaleBias.zw;
                o.positionCS = GetFullScreenTriangleVertexPosition(vertexID);
                return o;
            }

            float4 FragSeed(Varyings i) : SV_Target
            {
                float3 color = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_BlitTexture, i.uv, 0).rgb;
                return float4(max(color, 0.0) * _BloomBandWeight.rgb, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "UpsampleCombine"

            ZWrite Off
            ZTest Always
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragUpsampleCombine
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

            TEXTURE2D(_BlitTexture);
            SAMPLER(sampler_BlitTexture);
            TEXTURE2D(_SourceTexLowMip);
            SAMPLER(sampler_SourceTexLowMip);
            float4 _SourceTexLowMip_TexelSize;
            float4 _BlitScaleBias;
            float4 _BloomBandWeight;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings o;
                o.uv = GetFullScreenTriangleTexCoord(vertexID) * _BlitScaleBias.xy + _BlitScaleBias.zw;
                o.positionCS = GetFullScreenTriangleVertexPosition(vertexID);
                return o;
            }

            float3 SampleLowMipTent(float2 uv)
            {
                float2 texel = _SourceTexLowMip_TexelSize.xy;
                float3 center = SAMPLE_TEXTURE2D_LOD(_SourceTexLowMip, sampler_SourceTexLowMip, uv, 0).rgb * 0.5;
                float3 corners =
                    SAMPLE_TEXTURE2D_LOD(_SourceTexLowMip, sampler_SourceTexLowMip, uv + float2(-0.5, -0.5) * texel, 0).rgb +
                    SAMPLE_TEXTURE2D_LOD(_SourceTexLowMip, sampler_SourceTexLowMip, uv + float2( 0.5, -0.5) * texel, 0).rgb +
                    SAMPLE_TEXTURE2D_LOD(_SourceTexLowMip, sampler_SourceTexLowMip, uv + float2(-0.5,  0.5) * texel, 0).rgb +
                    SAMPLE_TEXTURE2D_LOD(_SourceTexLowMip, sampler_SourceTexLowMip, uv + float2( 0.5,  0.5) * texel, 0).rgb;
                return center + corners * 0.125;
            }

            float4 FragUpsampleCombine(Varyings i) : SV_Target
            {
                float3 current = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_BlitTexture, i.uv, 0).rgb * _BloomBandWeight.rgb;
                float3 lowMip = SampleLowMipTent(i.uv);
                return float4(max(current + lowMip, 0.0), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Composite"

            ZWrite Off
            ZTest Always
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

            TEXTURE2D(_BlitTexture);
            SAMPLER(sampler_BlitTexture);
            TEXTURE2D(_SourceTexLowMip);
            SAMPLER(sampler_SourceTexLowMip);
            float4 _SourceTexLowMip_TexelSize;
            float4 _BlitScaleBias;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings o;
                o.uv = GetFullScreenTriangleTexCoord(vertexID) * _BlitScaleBias.xy + _BlitScaleBias.zw;
                o.positionCS = GetFullScreenTriangleVertexPosition(vertexID);
                return o;
            }

            float3 SampleLowMipTent(float2 uv)
            {
                float2 texel = _SourceTexLowMip_TexelSize.xy;
                float3 center = SAMPLE_TEXTURE2D_LOD(_SourceTexLowMip, sampler_SourceTexLowMip, uv, 0).rgb * 0.5;
                float3 corners =
                    SAMPLE_TEXTURE2D_LOD(_SourceTexLowMip, sampler_SourceTexLowMip, uv + float2(-0.5, -0.5) * texel, 0).rgb +
                    SAMPLE_TEXTURE2D_LOD(_SourceTexLowMip, sampler_SourceTexLowMip, uv + float2( 0.5, -0.5) * texel, 0).rgb +
                    SAMPLE_TEXTURE2D_LOD(_SourceTexLowMip, sampler_SourceTexLowMip, uv + float2(-0.5,  0.5) * texel, 0).rgb +
                    SAMPLE_TEXTURE2D_LOD(_SourceTexLowMip, sampler_SourceTexLowMip, uv + float2( 0.5,  0.5) * texel, 0).rgb;
                return center + corners * 0.125;
            }

            float4 FragComposite(Varyings i) : SV_Target
            {
                float4 source = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_BlitTexture, i.uv, 0);
                float3 bloom = SampleLowMipTent(i.uv);
                return float4(max(source.rgb, 0.0) + max(bloom, 0.0), source.a);
            }
            ENDHLSL
        }
    }
}
