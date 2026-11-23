Shader "Path Tracing RP/Default"
{
    Properties
    {
        [MainTexture]_BaseMap("Base Map", 2D) = "white" {}
        [MainColor]_BaseColor("Base Color", Color) = (1,1,1,1)
        [NoScaleOffset]_MetallicRoughnessMap("Material Map (R=Base Metalness, G=Specular Roughness)", 2D) = "white" {}

        _OpenPbrBaseWeight("Base Weight", Range(0,1)) = 1
        _OpenPbrBaseDiffuseRoughness("Base Diffuse Roughness", Range(0,1)) = 0
        _OpenPbrBaseMetalness("Base Metalness", Range(0,1)) = 0

        _OpenPbrSpecularWeight("Specular Weight", Range(0,1)) = 1
        _OpenPbrSpecularColor("Specular Color", Color) = (1,1,1,1)
        _OpenPbrSpecularRoughness("Specular Roughness", Range(0,1)) = 0.3
        _OpenPbrSpecularIOR("Specular IOR", Range(1.0,2.5)) = 1.5
        _OpenPbrSpecularRoughnessAnisotropy("Specular Roughness Anisotropy", Range(0,1)) = 0
        _OpenPbrTransmissionDepth("Transmission Depth", Range(0,10)) = 1
        _OpenPbrTransmissionWeight("Transmission Weight", Range(0,1)) = 0
        _OpenPbrTransmissionColor("Transmission Color", Color) = (1,1,1,1)

        _OpenPbrSubsurfaceWeight("Subsurface Weight", Range(0,1)) = 0
        _OpenPbrSubsurfaceColor("Subsurface Color", Color) = (0.8,0.8,0.8,1)
        _OpenPbrSubsurfaceRadius("Subsurface Radius (mm)", Vector) = (1,0.5,0.25,0)
        _OpenPbrSubsurfaceRadiusScale("Subsurface Radius Scale", Range(0.001,10)) = 1
        _OpenPbrSubsurfaceScatterAnisotropy("Subsurface Scatter Anisotropy", Range(-0.95,0.95)) = 0

        _OpenPbrFuzzWeight("Fuzz Weight", Range(0,1)) = 0
        _OpenPbrFuzzColor("Fuzz Color", Color) = (1,1,1,1)
        _OpenPbrFuzzRoughness("Fuzz Roughness", Range(0,1)) = 0.5

        _OpenPbrCoatWeight("Coat Weight", Range(0,1)) = 0
        _OpenPbrCoatColor("Coat Color", Color) = (1,1,1,1)
        _OpenPbrCoatRoughness("Coat Roughness", Range(0,1)) = 0
        _OpenPbrCoatRoughnessAnisotropy("Coat Roughness Anisotropy", Range(0,1)) = 0
        _OpenPbrCoatIOR("Coat IOR", Range(1.0,2.5)) = 1.6
        _OpenPbrCoatDarkening("Coat Darkening", Range(0,1)) = 1
        _OpenPbrCoatAffectColor("Coat Affect Color", Range(0,1)) = 0
        _OpenPbrCoatAffectRoughness("Coat Affect Roughness", Range(0,1)) = 0

        _OpenPbrThinFilmWeight("Thin Film Weight", Range(0,1)) = 0
        _OpenPbrThinFilmThickness("Thin Film Thickness (um)", Range(0,2)) = 0.5
        _OpenPbrThinFilmIOR("Thin Film IOR", Range(1.0,2.5)) = 1.4

        _OpenPbrEmissionLuminance("Emission Luminance", Float) = 0
        [HDR]_OpenPbrEmissionColor("Emission Color", Color) = (1,1,1,1)
        _EmissionMap("Emission Map", 2D) = "white" {}

        _OpenPbrGeometryOpacity("Geometry Opacity", Range(0,1)) = 1
        [Toggle]_OpenPbrGeometryThinWalled("Geometry Thin Walled", Float) = 0

        [NoScaleOffset]_BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Range(0,2)) = 1
        [Toggle]_AlphaClip("Alpha Clip", Float) = 0
        _Cutoff("Cutoff", Range(0,1)) = 0.5
        [Enum(Off,0,Front,1,Back,2)] _Cull("Cull", Float) = 2
        [HideInInspector]_LucentMaterialIndex("Lucent Material Index", Integer) = 0
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    TEXTURE2D(_BaseMap);
    SAMPLER(sampler_BaseMap);
    TEXTURE2D(_MetallicRoughnessMap);
    SAMPLER(sampler_MetallicRoughnessMap);
    TEXTURE2D(_BumpMap);
    SAMPLER(sampler_BumpMap);
    TEXTURE2D(_EmissionMap);
    SAMPLER(sampler_EmissionMap);
    TEXTURE2D(_MCDirectLighting);
    TEXTURE2D(_MCIndirectLighting);
    TEXTURE2D(_MCSurfaceSignal);
    TEXTURE2D(_MCReflectionSignal);
    TEXTURE2D(_MCTransmissionSignal);
    TEXTURE2D(_MCHitMask);

    CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        float4 _EmissionMap_ST;
        float4 _BaseColor;
        float _OpenPbrBaseWeight;
        float _OpenPbrBaseDiffuseRoughness;
        float _OpenPbrBaseMetalness;
        float _OpenPbrSpecularWeight;
        float4 _OpenPbrSpecularColor;
        float _OpenPbrSpecularRoughness;
        float _OpenPbrSpecularIOR;
        float _OpenPbrSpecularRoughnessAnisotropy;
        float _OpenPbrTransmissionWeight;
        float4 _OpenPbrTransmissionColor;
        float _OpenPbrTransmissionDepth;
        float _OpenPbrSubsurfaceWeight;
        float4 _OpenPbrSubsurfaceColor;
        float4 _OpenPbrSubsurfaceRadius;
        float _OpenPbrSubsurfaceRadiusScale;
        float _OpenPbrSubsurfaceScatterAnisotropy;
        float _OpenPbrFuzzWeight;
        float4 _OpenPbrFuzzColor;
        float _OpenPbrFuzzRoughness;
        float _OpenPbrCoatWeight;
        float4 _OpenPbrCoatColor;
        float _OpenPbrCoatRoughness;
        float _OpenPbrCoatRoughnessAnisotropy;
        float _OpenPbrCoatIOR;
        float _OpenPbrCoatDarkening;
        float _OpenPbrCoatAffectColor;
        float _OpenPbrCoatAffectRoughness;
        float _OpenPbrThinFilmWeight;
        float _OpenPbrThinFilmThickness;
        float _OpenPbrThinFilmIOR;
        float _OpenPbrEmissionLuminance;
        float4 _OpenPbrEmissionColor;
        float _OpenPbrGeometryOpacity;
        float _OpenPbrGeometryThinWalled;
        float _BumpScale;
        float _AlphaClip;
        float _Cutoff;
        float _Cull;
        int _LucentMaterialIndex;
    CBUFFER_END

    struct Attributes
    {
        float4 positionOS : POSITION;
        float3 normalOS : NORMAL;
        float4 tangentOS : TANGENT;
        float2 uv : TEXCOORD0;
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float3 positionWS : TEXCOORD0;
        float3 normalWS : TEXCOORD1;
        float3 tangentWS : TEXCOORD2;
        float3 bitangentWS : TEXCOORD3;
        float2 uv0 : TEXCOORD4;
    };

    float3 UnpackOpenPbrNormal(float4 packedNormal, float scale)
    {
        return UnpackNormalScale(packedNormal, max(scale, 0.0));
    }

    Varyings Vert(Attributes i)
    {
        Varyings o;
        float3 positionWS = TransformObjectToWorld(i.positionOS.xyz);
        float3 normalWS = TransformObjectToWorldNormal(i.normalOS);
        float3 tangentWS = TransformObjectToWorldDir(i.tangentOS.xyz);
        o.positionCS = TransformWorldToHClip(positionWS);
        o.positionWS = positionWS;
        o.normalWS = normalize(normalWS);
        o.tangentWS = normalize(tangentWS);
        o.bitangentWS = normalize(cross(o.normalWS, o.tangentWS) * i.tangentOS.w);
        o.uv0 = i.uv;
        return o;
    }

    void LoadOpenPbrPreviewLighting(uint2 pixel, out float3 directLighting, out float3 indirectLighting, out float hitMask)
    {
        directLighting = LOAD_TEXTURE2D(_MCSurfaceSignal, pixel).rgb;
        indirectLighting = LOAD_TEXTURE2D(_MCReflectionSignal, pixel).rgb + LOAD_TEXTURE2D(_MCTransmissionSignal, pixel).rgb;
        hitMask = LOAD_TEXTURE2D(_MCHitMask, pixel).r;
    }

    uint2 GetOpenPbrLightingPixel(float4 positionCS)
    {
        float2 screenSizeF = max(_ScreenParams.xy, 1.0.xx);
        uint2 screenSize = (uint2)screenSizeF;
        uint2 pixel = (uint2)floor(positionCS.xy);
        if (_ProjectionParams.x > 0.0)
            pixel.y = (screenSize.y - 1u) - pixel.y;

        return min(pixel, screenSize - 1u);
    }

    float4 FragForward(Varyings i) : SV_Target
    {
        float2 uv = i.uv0 * _BaseMap_ST.xy + _BaseMap_ST.zw;
        float4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
        float alpha = saturate(baseColor.a * _OpenPbrGeometryOpacity);
        if (_AlphaClip > 0.5)
            clip(alpha - _Cutoff);

        float4 materialSample = SAMPLE_TEXTURE2D(_MetallicRoughnessMap, sampler_MetallicRoughnessMap, uv);
        float metalness = saturate(_OpenPbrBaseMetalness * materialSample.r);
        float roughness = saturate(max(0.02, _OpenPbrSpecularRoughness * materialSample.g));
        float3 normalTS = UnpackOpenPbrNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _BumpScale);
        float3x3 tbn = float3x3(normalize(i.tangentWS), normalize(i.bitangentWS), normalize(i.normalWS));
        float3 n = normalize(mul(normalTS, tbn));
        float3 v = normalize(_WorldSpaceCameraPos.xyz - i.positionWS);
        float ndv = saturate(dot(n, v));
        float3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, i.uv0 * _EmissionMap_ST.xy + _EmissionMap_ST.zw).rgb *
            _OpenPbrEmissionColor.rgb * _OpenPbrEmissionLuminance;

        float3 fallback = baseColor.rgb * (0.05 + 0.35 * ndv) * (1.0 - metalness) +
            lerp(0.04.xxx, baseColor.rgb, metalness) * pow(1.0 - ndv, 5.0) * (1.0 - roughness);

        uint2 pixel = GetOpenPbrLightingPixel(i.positionCS);
        float3 mcDirect;
        float3 mcIndirect;
        float mcHit;
        LoadOpenPbrPreviewLighting(pixel, mcDirect, mcIndirect, mcHit);
        float3 color = lerp(fallback, emission + mcDirect + mcIndirect, saturate(mcHit));
        return float4(color, alpha);
    }
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "SRPDefaultUnlit"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull [_Cull]
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment FragForward            ENDHLSL
        }

        Pass
        {
            Name "RayTracing"
            Tags { "LightMode" = "RayTracing" }

            HLSLPROGRAM
            #pragma target 6.3
            #pragma require RayTracing
            #pragma require int64
            #pragma raytracing DXRLucentPathPass
            #define LUCENT_PATH_OPENPBR_SHADER 1
            #include "Assets/LucentPath/Shaders/DXR/DXRLucentPathMaterialClosestHit.hlsl"
            ENDHLSL
        }
    }

    CustomEditor "LucentPath.EditorUi.Materials.LucentPathOpenPbrShaderGUI"
}
