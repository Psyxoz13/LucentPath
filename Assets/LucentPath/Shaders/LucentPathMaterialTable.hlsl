#ifndef LUCENT_PATH_MATERIAL_TABLE_INCLUDED
#define LUCENT_PATH_MATERIAL_TABLE_INCLUDED

#include "Assets/LucentPath/Shaders/LucentPathCommon.hlsl"
#include "Assets/LucentPath/Shaders/LucentPathSurfaceHit.hlsl"

#define LUCENT_MATERIAL_ROW_FLAG_OPENPBR 1u
#define LUCENT_MATERIAL_ROW_FLAG_EXPLICIT_CONDUCTOR 2u
#define LUCENT_MATERIAL_ROW_FLAG_THIN_WALLED 4u
#define LUCENT_MATERIAL_ROW_FLAG_ROUGHNESS_FLOOR 8u
#define LUCENT_MATERIAL_ROW_FLAG_COMPLEX 16u

struct GpuMaterialRow
{
    float4 baseColorFactor;
    float4 specularColorWeight;
    float4 transmissionColorWeight;
    float4 coatColorWeight;
    float4 emissionFactorMetalness;
    float4 fuzzColorWeight;
    float4 subsurfaceColorWeight;
    float4 coatExtrasOpacity;
    float4 scalarsA;
    float4 scalarsB;
    float4 iors;
    float4 conductorEtaThinFilmThickness;
    float4 conductorKTransmissionDepth;
    float4 conductorEdgeTintReserved;
    float4 subsurfaceRadiusScale;
    float4 baseMapST;
    float4 emissionMapST;
    uint4  layersFlags;
    uint4  modelReserved;
};

StructuredBuffer<GpuMaterialRow> _LucentMaterialTable;
int _LucentMaterialTableCount;

Texture2DArray<float4> _LucentBaseAtlas;
SamplerState sampler_LucentBaseAtlas;
Texture2DArray<float4> _LucentMaterialMapAtlas;
SamplerState sampler_LucentMaterialMapAtlas;
Texture2DArray<float4> _LucentEmissionAtlas;
SamplerState sampler_LucentEmissionAtlas;

GpuMaterialRow LoadLucentMaterialRow(uint materialIndex)
{
    uint index = materialIndex < (uint)_LucentMaterialTableCount ? materialIndex : 0u;
    return _LucentMaterialTable[index];
}

struct LucentResolvedMaterial
{
    float4 baseColor;
    float  baseWeight;
    float  baseMetalness;
    float  baseDiffuseRoughness;
    float3 specularColor;
    float  specularWeight;
    float  specularRoughness;
    float  specularAnisotropy;
    float  ior;
    float  conductorWeight;
    float3 conductorEta;
    float3 conductorK;
    float3 conductorEdgeTint;
    bool   hasExplicitConductor;
    float3 transmissionColor;
    float  transmissionWeight;
    float  transmissionDepth;
    float3 coatColor;
    float  coatWeight;
    float  coatRoughness;
    float  coatAnisotropy;
    float  coatIor;
    float  coatDarkening;
    float  coatAffectColor;
    float  coatAffectRoughness;
    float3 fuzzColor;
    float  fuzzWeight;
    float  fuzzRoughness;
    float  thinFilmWeight;
    float  thinFilmThickness;
    float  thinFilmIor;
    float3 subsurfaceColor;
    float  subsurfaceWeight;
    float3 subsurfaceRadius;
    float  subsurfaceAnisotropy;
    float3 emission;
    float  alpha;
    float  geometryOpacity;
    bool   thinWalled;
    uint   materialModel;
};

LucentResolvedMaterial ResolveLucentMaterial(GpuMaterialRow row, uint materialModel, float2 uvRaw)
{
    float2 uvBase = uvRaw * row.baseMapST.xy + row.baseMapST.zw;
    float2 uvEmission = uvRaw * row.emissionMapST.xy + row.emissionMapST.zw;

    float4 baseSample = _LucentBaseAtlas.SampleLevel(sampler_LucentBaseAtlas, float3(uvBase, (float)row.layersFlags.x), 0.0);
    float4 materialSample = _LucentMaterialMapAtlas.SampleLevel(sampler_LucentMaterialMapAtlas, float3(uvBase, (float)row.layersFlags.y), 0.0);
    float3 emissionSample = _LucentEmissionAtlas.SampleLevel(sampler_LucentEmissionAtlas, float3(uvEmission, (float)row.layersFlags.z), 0.0).rgb;

    uint rowFlags = row.layersFlags.w;
    bool openPbr = (rowFlags & LUCENT_MATERIAL_ROW_FLAG_OPENPBR) != 0u;
    bool dielectricKind = materialModel == LUCENT_HIT_MATERIAL_DIELECTRIC || materialModel == LUCENT_HIT_MATERIAL_THIN_DIELECTRIC;

    LucentResolvedMaterial m;
    m.materialModel = materialModel;
    m.baseColor = max(baseSample * row.baseColorFactor, 0.0);
    m.baseWeight = saturate(row.scalarsA.x);
    m.baseMetalness = saturate(row.emissionFactorMetalness.w * materialSample.r);
    m.baseDiffuseRoughness = saturate(row.scalarsA.y);
    float roughness = row.scalarsA.z * materialSample.g;
    if ((rowFlags & LUCENT_MATERIAL_ROW_FLAG_ROUGHNESS_FLOOR) != 0u)
        roughness = max(0.02, roughness);
    m.specularRoughness = saturate(roughness);
    m.specularAnisotropy = openPbr ? saturate(row.scalarsA.w) : clamp(row.scalarsA.w, -0.95, 0.95);
    m.specularColor = saturate(row.specularColorWeight.rgb);
    m.specularWeight = max(row.specularColorWeight.w, 0.0);
    m.ior = ResolveMaterialIor(row.iors.x);
    m.hasExplicitConductor = (rowFlags & LUCENT_MATERIAL_ROW_FLAG_EXPLICIT_CONDUCTOR) != 0u;
    m.conductorEta = max(row.conductorEtaThinFilmThickness.rgb, 1e-3);
    m.conductorK = max(row.conductorKTransmissionDepth.rgb, 0.0);
    m.conductorEdgeTint = saturate(row.conductorEdgeTintReserved.rgb);
    m.transmissionColor = openPbr
        ? saturate(row.transmissionColorWeight.rgb)
        : (dielectricKind ? m.baseColor.rgb : float3(1.0, 1.0, 1.0));
    m.transmissionWeight = openPbr
        ? saturate(row.transmissionColorWeight.w)
        : (dielectricKind ? saturate(row.transmissionColorWeight.w * materialSample.r) : 0.0);
    m.transmissionDepth = max(row.conductorKTransmissionDepth.w, 0.0);
    m.coatColor = saturate(row.coatColorWeight.rgb);
    m.coatWeight = saturate(row.coatColorWeight.w);
    m.coatRoughness = saturate(row.scalarsB.x);
    m.coatAnisotropy = saturate(row.scalarsB.y);
    m.coatIor = ResolveMaterialIor(row.iors.y);
    m.coatDarkening = saturate(row.coatExtrasOpacity.x);
    m.coatAffectColor = saturate(row.coatExtrasOpacity.y);
    m.coatAffectRoughness = saturate(row.coatExtrasOpacity.z);
    m.fuzzColor = saturate(row.fuzzColorWeight.rgb);
    m.fuzzWeight = saturate(row.fuzzColorWeight.w);
    m.fuzzRoughness = saturate(row.scalarsB.w);
    m.thinFilmWeight = saturate(row.scalarsB.z);
    m.thinFilmThickness = max(row.conductorEtaThinFilmThickness.w, 0.0);
    m.thinFilmIor = ResolveMaterialIor(row.iors.z);
    m.subsurfaceColor = saturate(row.subsurfaceColorWeight.rgb);
    m.subsurfaceWeight = saturate(row.subsurfaceColorWeight.w);
    m.subsurfaceRadius = SubsurfaceRadiusMmToSceneUnits(row.subsurfaceRadiusScale.rgb, row.subsurfaceRadiusScale.w);
    m.subsurfaceAnisotropy = clamp(row.iors.w, -0.99, 0.99);
    m.emission = max(emissionSample * row.emissionFactorMetalness.rgb, 0.0);
    m.alpha = openPbr ? saturate(m.baseColor.a * row.coatExtrasOpacity.w) : saturate(m.baseColor.a);
    m.geometryOpacity = m.alpha;
    m.thinWalled = (rowFlags & LUCENT_MATERIAL_ROW_FLAG_THIN_WALLED) != 0u;

    float conductorWeight = 0.0;
    if (materialModel == LUCENT_HIT_MATERIAL_STANDARD)
        conductorWeight = m.baseMetalness;
    else if (materialModel == LUCENT_HIT_MATERIAL_CONDUCTOR)
        conductorWeight = 1.0;
    else if (materialModel == LUCENT_HIT_MATERIAL_OPENPBR)
        conductorWeight = m.baseMetalness;
    m.conductorWeight = conductorWeight;

    return m;
}

#endif
