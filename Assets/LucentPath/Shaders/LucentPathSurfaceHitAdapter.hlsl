#ifndef LUCENT_PATH_SURFACE_HIT_ADAPTER_INCLUDED
#define LUCENT_PATH_SURFACE_HIT_ADAPTER_INCLUDED

#include "Assets/LucentPath/Shaders/LucentPathSurfaceHit.hlsl"
#include "Assets/LucentPath/Shaders/LucentPathMaterialTable.hlsl"

SurfaceInteraction CreateSurfaceInteraction(GpuSurfaceHit hit, Ray ray)
{
    SurfaceInteraction si;
    float3 geomNormal = SafeNormalize(GetSurfaceHitGeomNormal(hit), float3(0.0, 1.0, 0.0));
    float3 normal = SafeNormalize(GetSurfaceHitShadingNormal(hit), geomNormal);
    float3 tangentHint = SafeNormalize(GetSurfaceHitTangent(hit), abs(normal.y) < 0.999 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0));
    float tangentSign = GetSurfaceHitTangentSign(hit);
    float3 bitangentHint = cross(normal, tangentHint) * tangentSign;
    float3 tangent;
    float3 bitangent;
    OrthonormalizeFrame(normal, tangentHint, bitangentHint, tangent, bitangent);

    si.p = GetSurfaceHitPosition(hit);
    si.pError = max(abs(si.p) * 1e-6, float3(1e-7, 1e-7, 1e-7));
    si.wo = SafeNormalize(-ray.direction, normal);
    si.uv = hit.uv;
    si.n = geomNormal;
    si.dpdu = tangent;
    si.dpdv = bitangent;
    si.dndu = 0.0;
    si.dndv = 0.0;
    si.shading.n = normal;
    si.shading.dpdu = tangent;
    si.shading.dpdv = bitangent;
    si.shading.dndu = 0.0;
    si.shading.dndv = 0.0;
    si.materialIndex = -1;
    si.triIndex = -1;
    si.instanceIndex = -1;
    return si;
}

SurfaceData GetSurface(GpuSurfaceHit hit, Ray ray)
{
    uint hitFlags = GetSurfaceHitFlags(hit);
    uint materialModelInt = GetSurfaceHitMaterialModel(hit);
    GpuMaterialRow row = LoadLucentMaterialRow(hit.materialIndex);
    LucentResolvedMaterial m = ResolveLucentMaterial(row, materialModelInt, hit.uv);

    float roughness = saturate(m.specularRoughness);
    float anisotropy = clamp(m.specularAnisotropy, -0.95, 0.95);
    float alphaX;
    float alphaY;
    ComputeStandardAlphaParameters(roughness, anisotropy, alphaX, alphaY);

    float materialModel = float(materialModelInt);
    bool openPbrModel = materialModelInt == LUCENT_HIT_MATERIAL_OPENPBR;
    bool thickDielectricModel = materialModelInt == LUCENT_HIT_MATERIAL_DIELECTRIC;

    float conductorWeight = m.conductorWeight;
    float transmissionWeight = m.transmissionWeight;

    float diffuseWeight = 0.0;
    if (openPbrModel)
    {
        float baseWeight = m.baseWeight;
        float subsurfaceWeightLocal = m.subsurfaceWeight;
        diffuseWeight = OpenPbrBaseDiffuseWeight(baseWeight, m.baseMetalness, transmissionWeight, subsurfaceWeightLocal) +
                        OpenPbrBaseSubsurfaceWeight(m.baseMetalness, transmissionWeight, subsurfaceWeightLocal);
        conductorWeight = OpenPbrBaseMetalWeight(m.baseMetalness);
    }
    else if (materialModelInt == LUCENT_HIT_MATERIAL_STANDARD)
    {
        diffuseWeight = saturate(1.0 - conductorWeight);
    }
    else if (materialModelInt == LUCENT_HIT_MATERIAL_DIFFUSE)
    {
        diffuseWeight = 1.0;
    }
    else if (materialModelInt == LUCENT_HIT_MATERIAL_CONDUCTOR)
    {
        conductorWeight = 1.0;
    }

    SurfaceData surface;
    surface.interaction = CreateSurfaceInteraction(hit, ray);
    surface.position = GetSurfaceHitPosition(hit);
    surface.normal = surface.interaction.shading.n;
    surface.geomNormal = surface.interaction.n;
    surface.uv = hit.uv;
    surface.baseColor = m.baseColor.rgb;
    surface.emission = m.emission;
    surface.materialModel = materialModel;
    surface.frontFace = (hitFlags & SURFACE_HIT_FLAG_FRONT_FACE) != 0u ? 1u : 0u;
    surface.alpha = thickDielectricModel ? 1.0 : saturate(m.alpha);
    surface.roughness = roughness;
    surface.anisotropy = anisotropy;
    surface.alphaX = alphaX;
    surface.alphaY = alphaY;
    surface.eta = ResolveMaterialIor(m.ior);
    surface.baseWeight = openPbrModel ? m.baseWeight : 1.0;
    surface.baseMetalness = openPbrModel ? m.baseMetalness : saturate(conductorWeight);
    surface.baseDiffuseRoughness = openPbrModel ? m.baseDiffuseRoughness : roughness;
    surface.specularWeight = openPbrModel ? m.specularWeight : 1.0;
    surface.specularColor = openPbrModel ? m.specularColor : float3(1.0, 1.0, 1.0);
    surface.specularRoughness = roughness;
    surface.specularAnisotropy = anisotropy;
    surface.transmissionWeight = transmissionWeight;
    surface.transmissionColor = m.transmissionColor;
    surface.transmissionDepth = openPbrModel ? max(m.transmissionDepth, 0.0) : 1.0;
    surface.diffuseWeight = diffuseWeight;
    surface.conductorWeight = conductorWeight;
    if ((hitFlags & SURFACE_HIT_FLAG_EXPLICIT_CONDUCTOR) != 0u)
    {
        surface.conductorEta = max(m.conductorEta, 1e-3);
        surface.conductorK = max(m.conductorK, 0.0);
    }
    else
    {
        ConvertF0ToConductorParameters(surface.baseColor, surface.conductorEta, surface.conductorK);
    }
    surface.conductorEdgeTint = saturate(m.conductorEdgeTint);
    surface.coatWeight = saturate(m.coatWeight);
    surface.coatColor = openPbrModel ? m.coatColor : float3(1.0, 1.0, 1.0);
    surface.coatRoughness = saturate(m.coatRoughness);
    surface.coatAnisotropy = openPbrModel ? saturate(m.coatAnisotropy) : 0.0;
    surface.coatIor = ResolveMaterialIor(m.coatIor);
    surface.coatDarkening = openPbrModel ? saturate(m.coatDarkening) : 1.0;
    surface.coatAffectColor = openPbrModel ? saturate(m.coatAffectColor) : 0.0;
    surface.coatAffectRoughness = openPbrModel ? saturate(m.coatAffectRoughness) : 0.0;
    surface.sheenWeight = openPbrModel ? saturate(m.fuzzWeight) : 0.0;
    surface.sheenRoughness = openPbrModel ? saturate(m.fuzzRoughness) : 0.3;
    surface.sheenColor = openPbrModel ? m.fuzzColor : float3(1.0, 1.0, 1.0);
    surface.thinFilmWeight = openPbrModel ? saturate(m.thinFilmWeight) : 0.0;
    surface.thinFilmThickness = openPbrModel ? max(m.thinFilmThickness, 0.0) : 0.5;
    surface.thinFilmIor = openPbrModel ? ResolveMaterialIor(m.thinFilmIor) : 1.5;
    surface.geometryOpacity = openPbrModel ? saturate(m.geometryOpacity) : surface.alpha;
    surface.geometryThinWalled = openPbrModel ? ((hitFlags & SURFACE_HIT_FLAG_THIN_WALLED) != 0u ? 1.0 : 0.0) : (IsThinDielectricSurface(surface) ? 1.0 : 0.0);
    surface.subsurfaceWeight = saturate(m.subsurfaceWeight);
    surface.subsurfaceColor = saturate(m.subsurfaceColor);
    surface.subsurfaceRadius = max(
        m.subsurfaceRadius,
        float3(SUBSURFACE_MIN_RADIUS_SCENE_UNITS, SUBSURFACE_MIN_RADIUS_SCENE_UNITS, SUBSURFACE_MIN_RADIUS_SCENE_UNITS));
    surface.subsurfaceAnisotropy = clamp(m.subsurfaceAnisotropy, -0.99, 0.99);
    surface.subsurfaceScale = 1.0;
    return surface;
}

#endif
