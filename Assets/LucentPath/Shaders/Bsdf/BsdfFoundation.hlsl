#ifndef LUCENT_PATH_BSDF_FOUNDATION_INCLUDED
#define LUCENT_PATH_BSDF_FOUNDATION_INCLUDED

#include "../LucentPathInteraction.hlsl"
#include "../LucentPathMicrofacet.hlsl"

#define BXDF_KIND_NONE 0u
#define BXDF_KIND_DIFFUSE 1u
#define BXDF_KIND_CONDUCTOR 2u
#define BXDF_KIND_DIELECTRIC 3u
#define BXDF_KIND_THIN_DIELECTRIC 4u
#define BXDF_KIND_CLEARCOAT 5u
#define BXDF_KIND_SHEEN 6u
#define BXDF_KIND_DIFFUSE_TRANSMISSION 7u

struct SurfaceData
{
    SurfaceInteraction interaction;
    float3 position;
    float3 normal;
    float3 geomNormal;
    float2 uv;
    float3 baseColor;
    float3 emission;
    float materialModel;
    uint frontFace;
    float alpha;
    float roughness;
    float anisotropy;
    float alphaX;
    float alphaY;
    float eta;
    float baseWeight;
    float baseMetalness;
    float baseDiffuseRoughness;
    float specularWeight;
    float3 specularColor;
    float specularRoughness;
    float specularAnisotropy;
    float transmissionWeight;
    float3 transmissionColor;
    float transmissionDepth;
    float diffuseWeight;
    float conductorWeight;
    float3 conductorEta;
    float3 conductorK;
    float3 conductorEdgeTint;
    float coatWeight;
    float3 coatColor;
    float coatRoughness;
    float coatAnisotropy;
    float coatIor;
    float coatDarkening;
    float coatAffectColor;
    float coatAffectRoughness;
    float sheenWeight;
    float sheenRoughness;
    float3 sheenColor;
    float thinFilmWeight;
    float thinFilmThickness;
    float thinFilmIor;
    float geometryOpacity;
    float geometryThinWalled;
    float subsurfaceWeight;
    float3 subsurfaceColor;
    float3 subsurfaceRadius;
    float subsurfaceAnisotropy;
    float subsurfaceScale;
};

struct BxDF
{
    uint kind;
    uint flags;
    uint surfaceFrontFace;
    float sampleWeight;
    float eta;
    float alphaX;
    float alphaY;
    float transmissionWeight;
    float3 scale;
    float3 slotA;
    float3 slotB;
    float3 f82EdgeTint;
    float thinFilmWeight;
    float thinFilmThickness;
    float thinFilmIor;
};

struct BSDF
{
    float3 tangent;
    float3 bitangent;
    float3 normal;
    float3 geomNormal;
    BxDF bxdf0;
    BxDF bxdf1;
    BxDF bxdf2;
    BxDF coat;
    BxDF sheen;
    uint bxdfCount;
    uint hasCoat;
    uint hasSheen;
};

struct BSDFSample
{
    float3 wi;
    float3 f;
    float pdf;
    uint flags;
    float eta;
};

float Pow5(float x)
{
    float x2 = x * x;
    return x2 * x2 * x;
}

void ConvertF0ToConductorParameters(float3 f0, out float3 eta, out float3 k)
{
    float3 reflectance = clamp(f0, 1e-4, 0.9999);
    eta = 1.0;
    k = 2.0 * sqrt(reflectance / max(1.0 - reflectance, 1e-4));
}

void ComputeStandardAlphaParameters(float roughness, float anisotropy, out float alphaX, out float alphaY)
{
    float alpha = max(saturate(roughness) * saturate(roughness), 1e-4);
    float magnitude = abs(clamp(anisotropy, -0.95, 0.95));
    float aspect = sqrt(max(1.0 - 0.9 * magnitude, 0.01));
    float ax = max(alpha / aspect, 1e-4);
    float ay = max(alpha * aspect, 1e-4);

    if (anisotropy >= 0.0)
    {
        alphaX = ax;
        alphaY = ay;
    }
    else
    {
        alphaX = ay;
        alphaY = ax;
    }
}

void ComputeOpenPbrAlphaParameters(float roughness, float anisotropy, out float alphaX, out float alphaY)
{
    float r = saturate(roughness);
    float a = saturate(anisotropy);
    float oneMinusA = 1.0 - a;
    float alphaT = max(r * r * sqrt(2.0 / max(1.0 + oneMinusA * oneMinusA, 1e-6)), 1e-4);
    float alphaB = max(oneMinusA * alphaT, 1e-4);
    alphaX = alphaT;
    alphaY = alphaB;
}

bool IsOpenPbrMaterialModel(float materialModel)
{
    return abs(materialModel - MATERIAL_MODEL_OPENPBR) < 0.25;
}

float OpenPbrCoatAffectedSpecularRoughness(float specularRoughness, float coatRoughness, float coatAffectRoughness)
{
    float specularRoughness4 = pow(saturate(specularRoughness), 4.0);
    float coatRoughness4 = pow(saturate(coatRoughness), 4.0);
    float gain = saturate(coatAffectRoughness) * 2.0;
    return pow(min(1.0, specularRoughness4 + gain * coatRoughness4), 0.25);
}

float3 OpenPbrCoatTintBase(SurfaceData surface, float3 baseColor)
{
    if (!IsOpenPbrMaterialModel(surface.materialModel) || surface.coatWeight <= 1e-4)
        return baseColor;
    float effect = saturate(surface.coatWeight) * saturate(surface.coatAffectColor);
    if (effect <= 1e-4)
        return baseColor;
    return pow(max(baseColor, 0.0), 1.0 + effect);
}

float SurfaceEffectiveSpecularRoughness(SurfaceData surface)
{
    if (!IsOpenPbrMaterialModel(surface.materialModel))
        return saturate(surface.roughness);

    return lerp(saturate(surface.specularRoughness),
        OpenPbrCoatAffectedSpecularRoughness(surface.specularRoughness, surface.coatRoughness, surface.coatAffectRoughness),
        saturate(surface.coatWeight));
}

void ComputeSurfaceSpecularAlphaParameters(SurfaceData surface, out float alphaX, out float alphaY)
{
    if (IsOpenPbrMaterialModel(surface.materialModel))
    {
        ComputeOpenPbrAlphaParameters(SurfaceEffectiveSpecularRoughness(surface), surface.specularAnisotropy, alphaX, alphaY);
        return;
    }

    alphaX = surface.alphaX;
    alphaY = surface.alphaY;
}

void ComputeSurfaceCoatAlphaParameters(SurfaceData surface, out float alphaX, out float alphaY)
{
    if (IsOpenPbrMaterialModel(surface.materialModel))
    {
        ComputeOpenPbrAlphaParameters(surface.coatRoughness, surface.coatAnisotropy, alphaX, alphaY);
        return;
    }

    ComputeStandardAlphaParameters(surface.coatRoughness, 0.0, alphaX, alphaY);
}

float ResolveSurfaceSpecularIor(SurfaceData surface)
{
    if (!IsOpenPbrMaterialModel(surface.materialModel))
        return ResolveMaterialIor(surface.eta);

    float specularIor = ResolveMaterialIor(surface.eta);
    float coatIor = ResolveMaterialIor(surface.coatIor);
    float specularToCoat = specularIor / max(coatIor, 1e-6);
    float coatToSpecular = coatIor / max(specularIor, 1e-6);
    float tirSafeCoatedIor = specularToCoat > 1.0 ? specularToCoat : coatToSpecular;
    float etaS = lerp(specularIor, tirSafeCoatedIor, saturate(surface.coatWeight));

    float etaMinusOne = etaS - 1.0;
    float etaPlusOne = etaS + 1.0;
    float f0Sqrt = etaMinusOne / max(etaPlusOne, 1e-6);
    float scaledF0 = clamp(max(surface.specularWeight, 0.0) * f0Sqrt * f0Sqrt, 0.0, 0.99999);
    float epsilon = sign(etaMinusOne) * sqrt(scaledF0);
    return ResolveMaterialIor((1.0 + epsilon) / max(1.0 - epsilon, 1e-6));
}

float OpenPbrBaseDiffuseWeight(float baseWeight, float metalness, float transmissionWeight, float subsurfaceWeight)
{
    return saturate(baseWeight) * saturate(1.0 - metalness) * saturate(1.0 - transmissionWeight) * saturate(1.0 - subsurfaceWeight);
}

float OpenPbrBaseSubsurfaceWeight(float metalness, float transmissionWeight, float subsurfaceWeight)
{
    return saturate(1.0 - metalness) * saturate(1.0 - transmissionWeight) * saturate(subsurfaceWeight);
}

float OpenPbrBaseMetalWeight(float metalness)
{
    return saturate(metalness);
}

bool IsThickDielectricSurface(SurfaceData surface)
{
    if (IsOpenPbrMaterialModel(surface.materialModel))
        return surface.transmissionWeight > 1e-4 && surface.geometryThinWalled <= 0.5;

    return abs(surface.materialModel - MATERIAL_MODEL_DIELECTRIC) < 0.25;
}

bool IsSubsurfaceSurface(SurfaceData surface)
{
    return surface.subsurfaceWeight > 1e-4 &&
        max(surface.subsurfaceRadius.x, max(surface.subsurfaceRadius.y, surface.subsurfaceRadius.z)) > SUBSURFACE_MIN_RADIUS_SCENE_UNITS;
}

float OpenPbrSubsurfaceMixWeight(SurfaceData surface)
{
    if (IsOpenPbrMaterialModel(surface.materialModel))
        return OpenPbrBaseSubsurfaceWeight(surface.baseMetalness, surface.transmissionWeight, surface.subsurfaceWeight);

    return saturate(surface.subsurfaceWeight) * saturate(surface.diffuseWeight);
}

float OpenPbrOpaqueDiffuseWeight(SurfaceData surface)
{
    if (IsOpenPbrMaterialModel(surface.materialModel))
        return OpenPbrBaseDiffuseWeight(surface.baseWeight, surface.baseMetalness, surface.transmissionWeight, surface.subsurfaceWeight);

    return saturate(surface.diffuseWeight) * (1.0 - saturate(surface.subsurfaceWeight));
}

bool IsThinDielectricSurface(SurfaceData surface)
{
    if (IsOpenPbrMaterialModel(surface.materialModel))
        return surface.transmissionWeight > 1e-4 && surface.geometryThinWalled > 0.5;

    return abs(surface.materialModel - MATERIAL_MODEL_THIN_DIELECTRIC) < 0.25;
}

bool IsThinWalledSurface(SurfaceData surface)
{
    if (IsOpenPbrMaterialModel(surface.materialModel))
    {
        return surface.geometryThinWalled > 0.5;
    }

    return abs(surface.materialModel - MATERIAL_MODEL_THIN_DIELECTRIC) < 0.25;
}

float GetThinTranslucencyWeight(SurfaceData surface)
{
    if (IsOpenPbrMaterialModel(surface.materialModel) == false || surface.geometryThinWalled <= 0.5)
    {
        return 0.0;
    }

    return OpenPbrBaseSubsurfaceWeight(surface.baseMetalness, surface.transmissionWeight, surface.subsurfaceWeight);
}

bool IsThinWalledTranslucentSurface(SurfaceData surface)
{
    return GetThinTranslucencyWeight(surface) > 1e-4;
}

float3 GetThinTranslucencyAlbedo(SurfaceData surface, float hemisphereSign)
{
    float anisotropy = clamp(surface.subsurfaceAnisotropy, -1.0, 1.0);

    return max(OpenPbrCoatTintBase(surface, surface.subsurfaceColor), 0.0) * GetThinTranslucencyWeight(surface) * (.5 * (1.0 + hemisphereSign * anisotropy));
}

float3 GetDiffuseReflectance(SurfaceData surface)
{
    float3 opaqueDiffuse = OpenPbrCoatTintBase(surface, surface.baseColor) * OpenPbrOpaqueDiffuseWeight(surface);

    return max(opaqueDiffuse, 0.0) + GetThinTranslucencyAlbedo(surface, -1.0);
}

float3 GetDiffuseTransmittance(SurfaceData surface)
{
    return GetThinTranslucencyAlbedo(surface, 1.0);
}

bool IsDielectricSurface(SurfaceData surface)
{
    return IsThickDielectricSurface(surface) || IsThinDielectricSurface(surface);
}

TrowbridgeReitzDistribution GetSurfaceSpecularDistribution(SurfaceData surface)
{
    float alphaX;
    float alphaY;
    ComputeSurfaceSpecularAlphaParameters(surface, alphaX, alphaY);

    TrowbridgeReitzDistribution distribution;
    distribution.alphaX = max(alphaX, 1e-4);
    distribution.alphaY = max(alphaY, 1e-4);
    return distribution;
}

float SurfaceGuideRoughness(SurfaceData surface)
{
    return SurfaceEffectiveSpecularRoughness(surface);
}

bool SurfaceSpecularEffectivelySmooth(SurfaceData surface)
{
    return TrowbridgeReitz_EffectivelySmooth(GetSurfaceSpecularDistribution(surface));
}

bool CanTraverseShadowDielectric(SurfaceData surface)
{
    return IsThickDielectricSurface(surface) && SurfaceSpecularEffectivelySmooth(surface) && surface.transmissionWeight > 1e-3;
}

bool IsDeltaSurface(SurfaceData surface)
{
    if (surface.diffuseWeight > 1e-4)
        return false;

    if (surface.coatWeight > 1e-4)
    {
        float coatAlphaX;
        float coatAlphaY;
        ComputeSurfaceCoatAlphaParameters(surface, coatAlphaX, coatAlphaY);
        TrowbridgeReitzDistribution coatDistribution;
        coatDistribution.alphaX = coatAlphaX;
        coatDistribution.alphaY = coatAlphaY;
        if (!TrowbridgeReitz_EffectivelySmooth(coatDistribution))
            return false;
    }

    if (IsThinDielectricSurface(surface))
        return SurfaceSpecularEffectivelySmooth(surface);

    if (IsThickDielectricSurface(surface))
        return SurfaceSpecularEffectivelySmooth(surface);

    if (surface.conductorWeight > 1e-4)
        return SurfaceSpecularEffectivelySmooth(surface);

    return false;
}

float FresnelDielectric(float cosThetaI, float etaI, float etaT)
{
    float clampedCosThetaI = clamp(cosThetaI, 0.0, 1.0);
    float eta = etaI / max(etaT, 1e-6);
    float sin2ThetaI = max(1.0 - clampedCosThetaI * clampedCosThetaI, 0.0);
    float sin2ThetaT = eta * eta * sin2ThetaI;
    if (sin2ThetaT > 1.0)
        return 1.0;

    float cosThetaT = sqrt(max(1.0 - sin2ThetaT, 0.0));
    float rs = (etaI * clampedCosThetaI - etaT * cosThetaT) / max(etaI * clampedCosThetaI + etaT * cosThetaT, 1e-6);
    float rp = (etaT * clampedCosThetaI - etaI * cosThetaT) / max(etaT * clampedCosThetaI + etaI * cosThetaT, 1e-6);
    return 0.5 * (rs * rs + rp * rp);
}

float3 FresnelF82(float cosTheta, float3 F0, float3 edgeTint)
{
    const float MU_BAR = 1.0 / 7.0;
    const float ONE_MINUS_MU_BAR = 1.0 - MU_BAR;
    const float ONE_MINUS_MU_BAR_5 = ONE_MINUS_MU_BAR * ONE_MINUS_MU_BAR * ONE_MINUS_MU_BAR * ONE_MINUS_MU_BAR * ONE_MINUS_MU_BAR;
    const float ONE_MINUS_MU_BAR_6 = ONE_MINUS_MU_BAR_5 * ONE_MINUS_MU_BAR;
    const float MU_BAR_PEAK_DENOM = MU_BAR * ONE_MINUS_MU_BAR_6;

    float mu = saturate(cosTheta);
    float oneMinusMu = 1.0 - mu;
    float oneMinusMu5 = oneMinusMu * oneMinusMu * oneMinusMu * oneMinusMu * oneMinusMu;
    float oneMinusMu6 = oneMinusMu5 * oneMinusMu;

    float3 schlick = F0 + (1.0 - F0) * oneMinusMu5;
    float3 schlickAtMuBar = F0 + (1.0 - F0) * ONE_MINUS_MU_BAR_5;
    float3 f82Value = schlickAtMuBar * saturate(edgeTint);

    float peak = (mu * oneMinusMu6) / max(MU_BAR_PEAK_DENOM, 1e-6);
    return saturate(schlick + peak * (f82Value - schlickAtMuBar));
}

float3 FresnelConductor(float cosThetaI, float3 eta, float3 k)
{
    float cosTheta = saturate(abs(cosThetaI));
    float cosTheta2 = cosTheta * cosTheta;
    float sinTheta2 = 1.0 - cosTheta2;
    float3 eta2 = eta * eta;
    float3 k2 = k * k;

    float3 t0 = eta2 - k2 - sinTheta2;
    float3 a2plusb2 = sqrt(max(t0 * t0 + 4.0 * eta2 * k2, 0.0));
    float3 t1 = a2plusb2 + cosTheta2;
    float3 a = sqrt(max(0.5 * (a2plusb2 + t0), 0.0));
    float3 t2 = 2.0 * cosTheta * a;
    float3 rs = (t1 - t2) / max(t1 + t2, 1e-6);

    float3 t3 = cosTheta2 * a2plusb2 + sinTheta2 * sinTheta2;
    float3 t4 = t2 * sinTheta2;
    float3 rp = rs * (t3 - t4) / max(t3 + t4, 1e-6);
    return 0.5 * (rp + rs);
}

float3 BuildPrimaryDiffuseAlbedo(SurfaceData surface)
{
    return GetDiffuseReflectance(surface) + GetDiffuseTransmittance(surface);
}

float3 ResolveCoatSubstrateTint(SurfaceData surface)
{
    return lerp(float3(1.0, 1.0, 1.0), max(surface.coatColor, 0.0), saturate(surface.coatWeight));
}

float3 ResolveClearcoatSubstrateTint(BxDF clearcoat)
{
    return max(clearcoat.slotB, 0.0);
}

float3 BuildPrimaryF0(SurfaceData surface)
{
    float eta = ResolveSurfaceSpecularIor(surface);
    float dielectricFresnel = (eta - 1.0) / (eta + 1.0);
    float dielectricF0Scalar = dielectricFresnel * dielectricFresnel;
    float3 dielectricF0 = float3(dielectricF0Scalar, dielectricF0Scalar, dielectricF0Scalar);

    float3 conductorF0 = IsOpenPbrMaterialModel(surface.materialModel)
        ? max(surface.baseWeight * OpenPbrCoatTintBase(surface, surface.baseColor), 0.0)
        : FresnelConductor(1.0, max(surface.conductorEta, float3(1e-3, 1e-3, 1e-3)), max(surface.conductorK, float3(0.0, 0.0, 0.0)));

    float metalness = IsOpenPbrMaterialModel(surface.materialModel)
        ? saturate(surface.baseMetalness)
        : saturate(surface.conductorWeight);
    float3 baseF0 = max(lerp(dielectricF0, conductorF0, metalness), 0.0);

    if (surface.coatWeight <= 1e-4)
        return baseF0;

    float coatIor = ResolveMaterialIor(surface.coatIor);
    float coatF0Term = (coatIor - 1.0) / max(coatIor + 1.0, 1e-6);
    float3 coatF0 = (coatF0Term * coatF0Term).xxx;
    float coatWeight = saturate(surface.coatWeight);
    return lerp(baseF0 * ResolveCoatSubstrateTint(surface), coatF0, coatWeight);
}

float3 BuildPrimaryTransmissionTint(SurfaceData surface)
{
    return IsDielectricSurface(surface) ? max(surface.transmissionColor, 0.0) : float3(1.0, 1.0, 1.0);
}

float ResolvePrimarySurfaceAlpha(SurfaceData surface)
{
    return IsThickDielectricSurface(surface) ? 1.0 : saturate(surface.alpha);
}

#define WRITE_PRIMARY_DEMODULATORS(pixel, surface) \
    do { \
        _PrimaryDiffuseAlbedo[pixel] = float4(BuildPrimaryDiffuseAlbedo(surface), 1.0); \
        _PrimarySpecularF0[pixel] = float4(BuildPrimaryF0(surface), 1.0); \
        _PrimaryTransmissionTint[pixel] = float4(BuildPrimaryTransmissionTint(surface), 1.0); \
    } while (0)

#define WRITE_PRIMARY_SKY_DEMODULATORS(pixel) \
    do { \
        _PrimaryDiffuseAlbedo[pixel] = float4(1.0, 1.0, 1.0, 0.0); \
        _PrimarySpecularF0[pixel] = float4(0.0, 0.0, 0.0, 0.0); \
        _PrimaryTransmissionTint[pixel] = float4(1.0, 1.0, 1.0, 0.0); \
    } while (0)

bool RefractDirection(float3 incidentDirection, float3 incidentNormal, float etaI, float etaT, out float3 refractedDirection)
{
    float3 rayDirection = SafeNormalize(incidentDirection, -incidentNormal);
    refractedDirection = rayDirection;

    float eta = etaI / max(etaT, 1e-6);
    float cosThetaI = clamp(dot(-rayDirection, incidentNormal), 0.0, 1.0);
    float sin2ThetaI = max(1.0 - cosThetaI * cosThetaI, 0.0);
    float sin2ThetaT = eta * eta * sin2ThetaI;
    bool canRefract = sin2ThetaT <= 1.0;

    if (canRefract)
    {
        float cosThetaT = sqrt(max(1.0 - sin2ThetaT, 0.0));
        refractedDirection = SafeNormalize(eta * rayDirection + (eta * cosThetaI - cosThetaT) * incidentNormal, rayDirection);
    }

    return canRefract;
}

void GetDielectricInterface(
    float3 rayDirection,
    float3 surfaceNormal,
    bool surfaceFrontFace,
    float surfaceEta,
    out float3 orientedNormal,
    out bool frontFace,
    out float etaI,
    out float etaT,
    out float cosThetaI,
    out bool totalInternalReflection,
    out float fresnel)
{
    orientedNormal = surfaceNormal;
    frontFace = true;
    etaI = 1.0;
    etaT = max(surfaceEta, 1.0);
    cosThetaI = 1.0;
    totalInternalReflection = false;
    fresnel = 1.0;

    float3 normalizedRayDirection = SafeNormalize(rayDirection, -surfaceNormal);
    frontFace = surfaceFrontFace;
    orientedNormal = surfaceNormal;
    etaI = frontFace ? 1.0 : surfaceEta;
    etaT = frontFace ? surfaceEta : 1.0;
    cosThetaI = clamp(dot(-normalizedRayDirection, orientedNormal), 0.0, 1.0);

    float eta = etaI / max(etaT, 1e-6);
    float sin2ThetaT = eta * eta * max(1.0 - cosThetaI * cosThetaI, 0.0);
    totalInternalReflection = sin2ThetaT > 1.0;
    fresnel = totalInternalReflection ? 1.0 : FresnelDielectric(cosThetaI, etaI, etaT);
}

float ThinDielectricReflectance(float fresnel)
{
    float transmission = 1.0 - fresnel;
    return fresnel + transmission * transmission * fresnel / max(1.0 - fresnel * fresnel, 1e-6);
}

float ThinDielectricTransmittance(float fresnel)
{
    float transmission = 1.0 - fresnel;
    return transmission * transmission / max(1.0 - fresnel * fresnel, 1e-6);
}

BxDF MakeEmptyBxDF()
{
    BxDF bxdf;
    bxdf.kind = BXDF_KIND_NONE;
    bxdf.flags = 0u;
    bxdf.surfaceFrontFace = 1u;
    bxdf.sampleWeight = 0.0;
    bxdf.eta = 1.0;
    bxdf.alphaX = 1.0;
    bxdf.alphaY = 1.0;
    bxdf.transmissionWeight = 0.0;
    bxdf.scale = 0.0;
    bxdf.slotA = 1.0;
    bxdf.slotB = float3(1.0, 1.0, 1.0);
    bxdf.f82EdgeTint = float3(1.0, 1.0, 1.0);
    bxdf.thinFilmWeight = 0.0;
    bxdf.thinFilmThickness = 0.5;
    bxdf.thinFilmIor = 1.5;
    return bxdf;
}

float ThinFilmFresnelDielectric(float cosThetaI, float etaI, float etaT)
{
    float sinThetaI2 = max(1.0 - cosThetaI * cosThetaI, 0.0);
    float sinThetaT2 = (etaI * etaI / max(etaT * etaT, 1e-6)) * sinThetaI2;
    if (sinThetaT2 >= 1.0)
        return 1.0;

    float cosThetaT = sqrt(max(1.0 - sinThetaT2, 0.0));
    float rs = (etaI * cosThetaI - etaT * cosThetaT) / max(etaI * cosThetaI + etaT * cosThetaT, 1e-6);
    float rp = (etaT * cosThetaI - etaI * cosThetaT) / max(etaT * cosThetaI + etaI * cosThetaT, 1e-6);
    return saturate(0.5 * (rs * rs + rp * rp));
}

float3 BelcourBarlaIridescenceRgb(float cosThetaI, float thicknessMicrometers, float filmIor, float baseIor)
{
    float eta1 = 1.0;
    float eta2 = max(filmIor, 1.0);
    float eta3 = max(baseIor, 1.0);

    float sin1Sq = max(1.0 - cosThetaI * cosThetaI, 0.0);
    float sin2Sq = (eta1 * eta1 / max(eta2 * eta2, 1e-6)) * sin1Sq;
    if (sin2Sq >= 1.0)
        return float3(1.0, 1.0, 1.0);
    float cos2 = sqrt(max(1.0 - sin2Sq, 0.0));

    float r12 = ThinFilmFresnelDielectric(cosThetaI, eta1, eta2);
    float r23 = ThinFilmFresnelDielectric(cos2, eta2, eta3);
    float t12 = 1.0 - r12;

    float opticalPathNm = 2.0 * eta2 * max(thicknessMicrometers, 0.0) * 1000.0 * cos2;
    float3 wavelengthsNm = float3(680.0, 550.0, 440.0);
    float3 phase = 2.0 * PI * opticalPathNm / wavelengthsNm;

    float r12r23 = r12 * r23;
    float denom = max(1.0 + r12r23 * r12r23 - 2.0 * r12r23 * cos(phase * 2.0).r, 1e-6);
    float3 airyR = r12 * r12 + (t12 * t12 * r23 * (1.0 - r12 * r12)) / max(1.0 - r12 * r23, 1e-6);
    float3 airyMod = float3(
        (r12 * r12 + r23 * r23 - 2.0 * r12 * r23 * cos(phase.x)) / max(1.0 + r12r23 * r12r23 - 2.0 * r12r23 * cos(phase.x), 1e-6),
        (r12 * r12 + r23 * r23 - 2.0 * r12 * r23 * cos(phase.y)) / max(1.0 + r12r23 * r12r23 - 2.0 * r12r23 * cos(phase.y), 1e-6),
        (r12 * r12 + r23 * r23 - 2.0 * r12 * r23 * cos(phase.z)) / max(1.0 + r12r23 * r12r23 - 2.0 * r12r23 * cos(phase.z), 1e-6));
    return saturate(airyMod / max(r12, 1e-4));
}

float3 ThinFilmRgbInterference(float cosTheta, float weight, float thicknessMicrometers, float filmIor)
{
    float enabled = saturate(weight) * step(1e-5, thicknessMicrometers);
    if (enabled <= 0.0)
        return float3(1.0, 1.0, 1.0);

    float3 iridescence = BelcourBarlaIridescenceRgb(saturate(cosTheta), thicknessMicrometers, filmIor, 1.5);
    return lerp(float3(1.0, 1.0, 1.0), iridescence, enabled);
}

BxDF MakeDiffuseBxDF(float3 reflectance, float roughness)
{
    BxDF bxdf = MakeEmptyBxDF();
    bxdf.kind = BXDF_KIND_DIFFUSE;
    bxdf.flags = BXDF_FLAG_REFLECTION | BXDF_FLAG_DIFFUSE;
    bxdf.alphaX = saturate(roughness);
    bxdf.scale = max(reflectance, 0.0);
    bxdf.sampleWeight = Max3(bxdf.scale);
    return bxdf;
}

BxDF MakeDiffuseTransmissionBxDF(float3 transmittance, float roughness)
{
    BxDF bxdf = MakeEmptyBxDF();
    bxdf.kind = BXDF_KIND_DIFFUSE_TRANSMISSION;
    bxdf.flags = BXDF_FLAG_TRANSMISSION | BXDF_FLAG_DIFFUSE;
    bxdf.alphaX = saturate(roughness);
    bxdf.scale = max(transmittance, 0.0);
    bxdf.sampleWeight = Max3(bxdf.scale);

    return bxdf;
}

BxDF MakeConductorBxDF(SurfaceData surface)
{
    BxDF bxdf = MakeEmptyBxDF();
    bxdf.kind = BXDF_KIND_CONDUCTOR;
    float alphaX = surface.alphaX;
    float alphaY = surface.alphaY;
    ComputeSurfaceSpecularAlphaParameters(surface, alphaX, alphaY);

    bxdf.alphaX = alphaX;
    bxdf.alphaY = alphaY;
    bool openPbrModel = IsOpenPbrMaterialModel(surface.materialModel);
    float openPbrMetalness = saturate(surface.baseMetalness);
    float3 conductorF0 = openPbrModel
        ? max(surface.baseWeight * OpenPbrCoatTintBase(surface, surface.baseColor), 0.0)
        : FresnelConductor(1.0, max(surface.conductorEta, 1e-3), max(surface.conductorK, 0.0));
    ConvertF0ToConductorParameters(conductorF0, bxdf.slotA, bxdf.slotB);
    bxdf.scale = ((openPbrModel ? openPbrMetalness : surface.conductorWeight) * max(surface.specularWeight, 0.0)).xxx;
    bxdf.f82EdgeTint = openPbrModel ? saturate(surface.specularColor) : saturate(surface.conductorEdgeTint);
    bxdf.thinFilmWeight = surface.thinFilmWeight;
    bxdf.thinFilmThickness = surface.thinFilmThickness;
    bxdf.thinFilmIor = surface.thinFilmIor;
    bxdf.sampleWeight = max(Max3(bxdf.scale * conductorF0), 1e-4);
    bxdf.flags = BXDF_FLAG_REFLECTION | (SurfaceSpecularEffectivelySmooth(surface) ? BXDF_FLAG_SPECULAR : BXDF_FLAG_GLOSSY);
    return bxdf;
}

BxDF MakeDielectricBxDF(SurfaceData surface)
{
    BxDF bxdf = MakeEmptyBxDF();
    float alphaX = surface.alphaX;
    float alphaY = surface.alphaY;
    ComputeSurfaceSpecularAlphaParameters(surface, alphaX, alphaY);

    bxdf.kind = BXDF_KIND_DIELECTRIC;
    bxdf.flags = BXDF_FLAG_REFLECTION | BXDF_FLAG_TRANSMISSION | (SurfaceSpecularEffectivelySmooth(surface) ? BXDF_FLAG_SPECULAR : BXDF_FLAG_GLOSSY);
    bxdf.surfaceFrontFace = surface.frontFace;
    bxdf.eta = ResolveSurfaceSpecularIor(surface);
    bxdf.alphaX = alphaX;
    bxdf.alphaY = alphaY;
    bxdf.transmissionWeight = surface.transmissionWeight;
    bool openPbrModel = IsOpenPbrMaterialModel(surface.materialModel);
    bxdf.scale = openPbrModel ? max(surface.specularColor, 0.0) * saturate(1.0 - surface.baseMetalness) * max(surface.specularWeight, 0.0) : max(surface.specularColor, 0.0);
    bxdf.slotA = 1.0;
    bxdf.slotB = max(surface.transmissionColor, 0.0);
    bxdf.thinFilmWeight = surface.thinFilmWeight;
    bxdf.thinFilmThickness = surface.thinFilmThickness;
    bxdf.thinFilmIor = surface.thinFilmIor;
    bxdf.sampleWeight = max(Max3(bxdf.scale) + bxdf.transmissionWeight * Max3(bxdf.slotA * bxdf.slotB), 1e-4);
    return bxdf;
}

BxDF MakeStandardDielectricReflectionBxDF(SurfaceData surface)
{
    BxDF bxdf = MakeDielectricBxDF(surface);
    float dielectricWeight = saturate(surface.diffuseWeight);
    bxdf.flags = BXDF_FLAG_REFLECTION | (SurfaceSpecularEffectivelySmooth(surface) ? BXDF_FLAG_SPECULAR : BXDF_FLAG_GLOSSY);
    bxdf.transmissionWeight = 0.0;
    bxdf.scale = IsOpenPbrMaterialModel(surface.materialModel)
        ? max(surface.specularColor, 0.0) * saturate(1.0 - surface.baseMetalness) * max(surface.specularWeight, 0.0)
        : dielectricWeight.xxx;

    float eta = ResolveMaterialIor(surface.eta);
    if (IsOpenPbrMaterialModel(surface.materialModel))
        eta = ResolveSurfaceSpecularIor(surface);
    float f0Sqrt = (eta - 1.0) / max(eta + 1.0, 1e-6);
    float f0 = f0Sqrt * f0Sqrt;
    float specularScaleWeight = IsOpenPbrMaterialModel(surface.materialModel) ? Max3(bxdf.scale) : dielectricWeight;
    bxdf.sampleWeight = max(specularScaleWeight * f0, 1e-4);
    return bxdf;
}

BxDF MakeThinDielectricBxDF(SurfaceData surface)
{
    BxDF bxdf = MakeEmptyBxDF();
    bxdf.kind = BXDF_KIND_THIN_DIELECTRIC;
    float alphaX = surface.alphaX;
    float alphaY = surface.alphaY;
    ComputeSurfaceSpecularAlphaParameters(surface, alphaX, alphaY);

    bool smooth = SurfaceSpecularEffectivelySmooth(surface);
    bxdf.flags = BXDF_FLAG_REFLECTION | BXDF_FLAG_TRANSMISSION | (smooth ? BXDF_FLAG_SPECULAR : BXDF_FLAG_GLOSSY);
    bxdf.surfaceFrontFace = surface.frontFace;
    bxdf.eta = ResolveSurfaceSpecularIor(surface);
    bxdf.alphaX = alphaX;
    bxdf.alphaY = alphaY;
    bxdf.transmissionWeight = surface.transmissionWeight;
    bxdf.scale = IsOpenPbrMaterialModel(surface.materialModel)
        ? max(surface.specularColor, 0.0) * saturate(1.0 - surface.baseMetalness) * max(surface.specularWeight, 0.0)
        : max(surface.specularColor, 0.0);
    bxdf.slotA = 1.0;
    bxdf.slotB = max(surface.transmissionColor, 0.0);
    bxdf.thinFilmWeight = surface.thinFilmWeight;
    bxdf.thinFilmThickness = surface.thinFilmThickness;
    bxdf.thinFilmIor = surface.thinFilmIor;
    bxdf.sampleWeight = max(Max3(bxdf.scale) + bxdf.transmissionWeight * Max3(bxdf.slotA * bxdf.slotB), 1e-4);
    return bxdf;
}

BxDF MakeClearcoatBxDF(SurfaceData surface)
{
    BxDF bxdf = MakeEmptyBxDF();
    float coatWeight = saturate(surface.coatWeight);
    if (coatWeight <= 1e-4)
        return bxdf;

    float alphaX;
    float alphaY;
    ComputeSurfaceCoatAlphaParameters(surface, alphaX, alphaY);

    TrowbridgeReitzDistribution distribution;
    distribution.alphaX = alphaX;
    distribution.alphaY = alphaY;

    float eta = ResolveMaterialIor(surface.coatIor);
    float f0Term = (eta - 1.0) / max(eta + 1.0, 1e-6);
    float f0 = f0Term * f0Term;

    bxdf.kind = BXDF_KIND_CLEARCOAT;
    bxdf.flags = BXDF_FLAG_REFLECTION | (TrowbridgeReitz_EffectivelySmooth(distribution) ? BXDF_FLAG_SPECULAR : BXDF_FLAG_GLOSSY);
    bxdf.eta = eta;
    bxdf.alphaX = alphaX;
    bxdf.alphaY = alphaY;
    bxdf.scale = coatWeight.xxx;
    bxdf.slotB = max(surface.coatColor, 0.0);
    bxdf.thinFilmWeight = surface.thinFilmWeight;
    bxdf.thinFilmThickness = surface.thinFilmThickness;
    bxdf.thinFilmIor = surface.thinFilmIor;
    bxdf.transmissionWeight = surface.coatDarkening;
    bxdf.sampleWeight = coatWeight * max(f0, 1e-4);
    return bxdf;
}

BxDF MakeSheenBxDF(SurfaceData surface)
{
    BxDF bxdf = MakeEmptyBxDF();
    float sheenWeight = saturate(surface.sheenWeight);
    if (sheenWeight <= 1e-4)
        return bxdf;

    float roughness = clamp(surface.sheenRoughness, 0.07, 1.0);
    bxdf.kind = BXDF_KIND_SHEEN;
    bxdf.flags = BXDF_FLAG_REFLECTION | BXDF_FLAG_GLOSSY;
    bxdf.alphaX = roughness;
    bxdf.alphaY = roughness;
    bxdf.scale = max(surface.sheenColor, 0.0) * sheenWeight;
    bxdf.sampleWeight = sheenWeight * max(Max3(bxdf.scale), 1e-4);
    return bxdf;
}

float3 ComputePrimaryReflectionWeight(SurfaceData surface, float3 woWorld)
{
    float cosTheta = saturate(abs(dot(SafeNormalize(woWorld, surface.normal), SafeNormalize(surface.normal, float3(0.0, 1.0, 0.0)))));
    float3 baseWeight = 0.0.xxx;
    float dielectricSpecularWeight = IsOpenPbrMaterialModel(surface.materialModel) ? max(surface.specularWeight, 0.0) : 1.0;
    if (IsThinDielectricSurface(surface))
    {
        float fresnel = FresnelDielectric(cosTheta, 1.0, ResolveSurfaceSpecularIor(surface));
        baseWeight = max(surface.specularColor, 0.0) * ThinDielectricReflectance(fresnel) * dielectricSpecularWeight;
    }
    else if (IsThickDielectricSurface(surface))
    {
        baseWeight = max(surface.specularColor, 0.0) * FresnelDielectric(cosTheta, 1.0, ResolveSurfaceSpecularIor(surface)) * dielectricSpecularWeight;
    }
    else if (surface.conductorWeight > 1e-4)
    {
        if (IsOpenPbrMaterialModel(surface.materialModel))
        {
            float metalness = saturate(surface.baseMetalness);
            float3 conductorF0 = max(surface.baseWeight * OpenPbrCoatTintBase(surface, surface.baseColor), 0.0);
            baseWeight = metalness * max(surface.specularWeight, 0.0) * FresnelF82(cosTheta, conductorF0, saturate(surface.specularColor));
        }
        else
        {
            float3 conductorEta = max(surface.conductorEta, 1e-3);
            float3 conductorK = max(surface.conductorK, 0.0);
            float3 conductorF0 = FresnelConductor(1.0, conductorEta, conductorK);
            baseWeight = surface.conductorWeight * FresnelF82(cosTheta, conductorF0, surface.conductorEdgeTint);
        }
    }
    else if (IsOpenPbrMaterialModel(surface.materialModel) || abs(surface.materialModel - MATERIAL_MODEL_STANDARD) < 0.25)
    {
        float opaqueDielectricWeight = IsOpenPbrMaterialModel(surface.materialModel)
            ? saturate(1.0 - surface.baseMetalness) * saturate(1.0 - surface.transmissionWeight)
            : saturate(surface.diffuseWeight);
        float fresnel = FresnelDielectric(cosTheta, 1.0, ResolveSurfaceSpecularIor(surface));
        baseWeight = opaqueDielectricWeight * max(surface.specularColor, 0.0) * fresnel * dielectricSpecularWeight;
    }

    if (surface.coatWeight > 1e-4)
    {
        float coatF = saturate(surface.coatWeight) * FresnelDielectric(cosTheta, 1.0, ResolveMaterialIor(surface.coatIor));
        float coatBaseTransmission = saturate(1.0 - coatF * saturate(surface.coatDarkening));
        baseWeight = baseWeight * coatBaseTransmission * ResolveCoatSubstrateTint(surface) + coatF.xxx;
    }

    return baseWeight;
}

float3 ComputeClearcoatBaseAttenuation(SurfaceData surface, float3 woWorld)
{
    if (surface.coatWeight <= 1e-4)
        return float3(1.0, 1.0, 1.0);

    float3 view = SafeNormalize(woWorld, surface.normal);
    float3 normal = SafeNormalize(surface.normal, float3(0.0, 1.0, 0.0));
    float cosTheta = saturate(abs(dot(view, normal)));
    float coatTransmission = saturate(1.0 - FresnelDielectric(cosTheta, 1.0, ResolveMaterialIor(surface.coatIor)));
    return lerp(float3(1.0, 1.0, 1.0), coatTransmission.xxx * max(surface.coatColor, 0.0), saturate(surface.coatWeight));
}

bool BxDFMatchesFlags(BxDF bxdf, uint sampleFlags)
{
    return bxdf.kind != BXDF_KIND_NONE && (bxdf.flags & sampleFlags) != 0u;
}

bool BxDFHasTransmissionSignal(BxDF bxdf)
{
    return (bxdf.flags & BXDF_FLAG_TRANSMISSION) != 0u &&
        bxdf.transmissionWeight > 1e-4 &&
        Max3(bxdf.slotA * bxdf.slotB) > 1e-4;
}

bool BxDFMatchesSignal(BxDF bxdf, uint signalKind, bool reflect)
{
    if (bxdf.kind == BXDF_KIND_NONE)
        return false;

    if (signalKind == PATH_SIGNAL_SURFACE)
        return bxdf.kind == BXDF_KIND_DIFFUSE || bxdf.kind == BXDF_KIND_DIFFUSE_TRANSMISSION;

    if (signalKind == PATH_SIGNAL_REFLECTION)
        return reflect &&
            (bxdf.flags & BXDF_FLAG_REFLECTION) != 0u &&
            (bxdf.flags & (BXDF_FLAG_GLOSSY | BXDF_FLAG_SPECULAR)) != 0u;

    if (signalKind == PATH_SIGNAL_TRANSMISSION)
        return !reflect && BxDFHasTransmissionSignal(bxdf);

    return false;
}

void BSDFAddComponent(inout BSDF bsdf, BxDF bxdf)
{
    if (bxdf.kind == BXDF_KIND_NONE || bxdf.sampleWeight <= 0.0)
        return;

    if (bsdf.bxdfCount == 0u)
        bsdf.bxdf0 = bxdf;
    else if (bsdf.bxdfCount == 1u)
        bsdf.bxdf1 = bxdf;
    else if (bsdf.bxdfCount == 2u)
        bsdf.bxdf2 = bxdf;

    bsdf.bxdfCount = min(bsdf.bxdfCount + 1u, 3u);
}

BxDF BSDFGetComponent(BSDF bsdf, uint index)
{
    if (index == 0u)
        return bsdf.bxdf0;

    if (index == 1u)
        return bsdf.bxdf1;

    return bsdf.bxdf2;
}

uint BSDFSelectableComponentCount(BSDF bsdf)
{
    return bsdf.bxdfCount + (bsdf.hasCoat != 0u ? 1u : 0u) + (bsdf.hasSheen != 0u ? 1u : 0u);
}

bool BSDFIsEmpty(BSDF bsdf)
{
    return BSDFSelectableComponentCount(bsdf) == 0u;
}

BxDF BSDFGetSelectableComponent(BSDF bsdf, uint index)
{
    if (index < bsdf.bxdfCount)
        return BSDFGetComponent(bsdf, index);

    uint coatEnd = bsdf.bxdfCount + (bsdf.hasCoat != 0u ? 1u : 0u);
    if (index < coatEnd)
        return bsdf.coat;

    return bsdf.sheen;
}

BSDF BuildBSDF(SurfaceData surface)
{
    BSDF bsdf;
    OrthonormalizeFrame(surface.normal, surface.interaction.shading.dpdu, surface.interaction.shading.dpdv, bsdf.tangent, bsdf.bitangent);
    bsdf.normal = surface.normal;
    bsdf.geomNormal = surface.geomNormal;
    bsdf.bxdf0 = MakeEmptyBxDF();
    bsdf.bxdf1 = MakeEmptyBxDF();
    bsdf.bxdf2 = MakeEmptyBxDF();
    bsdf.coat = MakeEmptyBxDF();
    bsdf.sheen = MakeEmptyBxDF();
    bsdf.bxdfCount = 0u;
    bsdf.hasCoat = 0u;
    bsdf.hasSheen = 0u;

    float3 diffuseReflectance = GetDiffuseReflectance(surface);
    if (Max3(diffuseReflectance) > 1e-4)
        BSDFAddComponent(bsdf, MakeDiffuseBxDF(diffuseReflectance, surface.baseDiffuseRoughness));

    bool hasOpaqueDielectricReflection = IsOpenPbrMaterialModel(surface.materialModel)
        ? saturate(1.0 - surface.baseMetalness) > 1e-4
        : surface.diffuseWeight > 1e-4;
    if ((abs(surface.materialModel - MATERIAL_MODEL_STANDARD) < 0.25 || IsOpenPbrMaterialModel(surface.materialModel)) &&
        hasOpaqueDielectricReflection &&
        surface.transmissionWeight <= 1e-4)
        BSDFAddComponent(bsdf, MakeStandardDielectricReflectionBxDF(surface));

    if (surface.conductorWeight > 1e-4)
        BSDFAddComponent(bsdf, MakeConductorBxDF(surface));

    if (IsThickDielectricSurface(surface))
        BSDFAddComponent(bsdf, MakeDielectricBxDF(surface));

    if (IsThinDielectricSurface(surface))
        BSDFAddComponent(bsdf, MakeThinDielectricBxDF(surface));

    float3 diffuseTransmittance = GetDiffuseTransmittance(surface);
    if (Max3(diffuseTransmittance) > 1e-4)
        BSDFAddComponent(bsdf, MakeDiffuseTransmissionBxDF(diffuseTransmittance, surface.baseDiffuseRoughness));

    BxDF coat = MakeClearcoatBxDF(surface);
    if (coat.kind != BXDF_KIND_NONE && coat.sampleWeight > 0.0)
    {
        bsdf.coat = coat;
        bsdf.hasCoat = 1u;
    }

    BxDF sheen = MakeSheenBxDF(surface);
    if (sheen.kind != BXDF_KIND_NONE && sheen.sampleWeight > 0.0)
    {
        bsdf.sheen = sheen;
        bsdf.hasSheen = 1u;
    }

    return bsdf;
}

struct ShadingFrame
{
    float3 tangent;
    float3 bitangent;
    float3 normal;
    float3 geomNormal;
};

struct LobeSet
{
    uint baseCandidate0;
    uint baseCandidate1;
    uint baseCandidate2;
    uint baseCount;
    uint hasCoat;
    uint hasSheen;
};

ShadingFrame ComputeShadingFrame(SurfaceData surface)
{
    ShadingFrame frame;
    OrthonormalizeFrame(surface.normal, surface.interaction.shading.dpdu, surface.interaction.shading.dpdv, frame.tangent, frame.bitangent);
    frame.normal = surface.normal;
    frame.geomNormal = surface.geomNormal;
    return frame;
}

void OrientShadingFrameForView(inout ShadingFrame frame, float3 woWorld)
{
    float cosG = dot(woWorld, frame.geomNormal);
    float cosS = dot(woWorld, frame.normal);
    if (cosS * cosG > 0.0 && abs(cosS) > 1e-4)
        return;

    float3 fallbackNormal = (cosG >= 0.0) ? frame.geomNormal : -frame.geomNormal;
    float3 t;
    float3 b;
    BuildBasis(fallbackNormal, t, b);
    frame.tangent = t;
    frame.bitangent = b;
    frame.normal = fallbackNormal;
}

float3 FrameWorldToLocal(ShadingFrame frame, float3 direction)
{
    return float3(dot(direction, frame.tangent), dot(direction, frame.bitangent), dot(direction, frame.normal));
}

float3 FrameLocalToWorld(ShadingFrame frame, float3 direction)
{
    return frame.tangent * direction.x + frame.bitangent * direction.y + frame.normal * direction.z;
}

BxDF BuildBaseCandidate(SurfaceData surface, uint candidateId)
{
    if (candidateId == 0u)
    {
        float3 diffuseReflectance = GetDiffuseReflectance(surface);
        if (Max3(diffuseReflectance) > 1e-4)
            return MakeDiffuseBxDF(diffuseReflectance, surface.baseDiffuseRoughness);
        return MakeEmptyBxDF();
    }
    if (candidateId == 1u)
    {
        bool hasOpaqueDielectricReflection = IsOpenPbrMaterialModel(surface.materialModel)
            ? saturate(1.0 - surface.baseMetalness) > 1e-4
            : surface.diffuseWeight > 1e-4;
        if ((abs(surface.materialModel - MATERIAL_MODEL_STANDARD) < 0.25 || IsOpenPbrMaterialModel(surface.materialModel)) &&
            hasOpaqueDielectricReflection &&
            surface.transmissionWeight <= 1e-4)
            return MakeStandardDielectricReflectionBxDF(surface);
        return MakeEmptyBxDF();
    }
#if !defined(LUCENT_SHADE_SIMPLE)
    if (candidateId == 2u)
    {
        if (surface.conductorWeight > 1e-4)
            return MakeConductorBxDF(surface);
        return MakeEmptyBxDF();
    }
    if (candidateId == 3u)
    {
        if (IsThickDielectricSurface(surface))
            return MakeDielectricBxDF(surface);
        return MakeEmptyBxDF();
    }
    if (candidateId == 4u)
    {
        if (IsThinDielectricSurface(surface))
            return MakeThinDielectricBxDF(surface);
        return MakeEmptyBxDF();
    }
    if (candidateId == 5u)
    {
        float3 diffuseTransmittance = GetDiffuseTransmittance(surface);
        if (Max3(diffuseTransmittance) > 1e-4)
            return MakeDiffuseTransmissionBxDF(diffuseTransmittance, surface.baseDiffuseRoughness);
        return MakeEmptyBxDF();
    }
#endif
    return MakeEmptyBxDF();
}

bool BaseCandidatePresent(SurfaceData surface, uint candidateId)
{
    if (candidateId == 0u)
        return Max3(GetDiffuseReflectance(surface)) > 1e-4;
    if (candidateId == 1u)
    {
        bool hasOpaqueDielectricReflection = IsOpenPbrMaterialModel(surface.materialModel)
            ? saturate(1.0 - surface.baseMetalness) > 1e-4
            : surface.diffuseWeight > 1e-4;
        return (abs(surface.materialModel - MATERIAL_MODEL_STANDARD) < 0.25 || IsOpenPbrMaterialModel(surface.materialModel)) &&
            hasOpaqueDielectricReflection &&
            surface.transmissionWeight <= 1e-4;
    }
#if !defined(LUCENT_SHADE_SIMPLE)
    if (candidateId == 2u)
        return surface.conductorWeight > 1e-4;
    if (candidateId == 3u)
        return IsThickDielectricSurface(surface);
    if (candidateId == 4u)
        return IsThinDielectricSurface(surface);
    if (candidateId == 5u)
        return Max3(GetDiffuseTransmittance(surface)) > 1e-4;
#endif
    return false;
}

LobeSet ComputeLobeSet(SurfaceData surface)
{
    LobeSet set;
    set.baseCandidate0 = 0u;
    set.baseCandidate1 = 0u;
    set.baseCandidate2 = 0u;
    set.baseCount = 0u;
    set.hasCoat = 0u;
    set.hasSheen = 0u;

#if defined(LUCENT_SHADE_SIMPLE)
    const uint candidateCount = 2u;
#else
    const uint candidateCount = 6u;
#endif

    [unroll]
    for (uint c = 0u; c < candidateCount; c++)
    {
        if (set.baseCount >= 3u)
            break;

        if (!BaseCandidatePresent(surface, c))
            continue;

        if (set.baseCount == 0u)
            set.baseCandidate0 = c;
        else if (set.baseCount == 1u)
            set.baseCandidate1 = c;
        else
            set.baseCandidate2 = c;
        set.baseCount += 1u;
    }

#if defined(LUCENT_SHADE_SIMPLE)
    set.hasCoat = 0u;
    set.hasSheen = 0u;
#else
    set.hasCoat = (saturate(surface.coatWeight) > 1e-4) ? 1u : 0u;
    set.hasSheen = (saturate(surface.sheenWeight) > 1e-4) ? 1u : 0u;
#endif

    return set;
}

uint SelectableLobeCount(LobeSet set)
{
    return set.baseCount + set.hasCoat + set.hasSheen;
}

bool SurfaceBsdfIsEmpty(SurfaceData surface)
{
    return SelectableLobeCount(ComputeLobeSet(surface)) == 0u;
}

BxDF BuildPresentLobe(SurfaceData surface, LobeSet set, uint index)
{
    if (index < set.baseCount)
    {
        uint candidateId = (index == 0u) ? set.baseCandidate0 : ((index == 1u) ? set.baseCandidate1 : set.baseCandidate2);
        return BuildBaseCandidate(surface, candidateId);
    }

#if !defined(LUCENT_SHADE_SIMPLE)
    uint coatEnd = set.baseCount + set.hasCoat;
    if (index < coatEnd)
        return MakeClearcoatBxDF(surface);

    return MakeSheenBxDF(surface);
#else
    return MakeEmptyBxDF();
#endif
}

bool BSDFHasReflectionSignal(BSDF bsdf)
{
    for (uint i = 0u; i < BSDFSelectableComponentCount(bsdf); i++)
    {
        if (BxDFMatchesSignal(BSDFGetSelectableComponent(bsdf, i), PATH_SIGNAL_REFLECTION, true))
            return true;
    }

    return false;
}

bool SurfaceHasReflectionSignal(SurfaceData surface)
{
    LobeSet set = ComputeLobeSet(surface);
    uint count = SelectableLobeCount(set);
    for (uint i = 0u; i < 6u; i++)
    {
        if (i >= count)
            break;
        if (BxDFMatchesSignal(BuildPresentLobe(surface, set, i), PATH_SIGNAL_REFLECTION, true))
            return true;
    }
    return false;
}

bool BSDFHasTransmissionSignal(BSDF bsdf)
{
    for (uint i = 0u; i < bsdf.bxdfCount; i++)
    {
        if (BxDFMatchesSignal(BSDFGetComponent(bsdf, i), PATH_SIGNAL_TRANSMISSION, false))
            return true;
    }

    return false;
}

bool SurfaceHasTransmissionSignal(SurfaceData surface)
{
    LobeSet set = ComputeLobeSet(surface);
    for (uint i = 0u; i < 3u; i++)
    {
        if (i >= set.baseCount)
            break;
        if (BxDFMatchesSignal(BuildPresentLobe(surface, set, i), PATH_SIGNAL_TRANSMISSION, false))
            return true;
    }
    return false;
}

bool SurfaceHasDeltaTransmission(SurfaceData surface)
{
    return SurfaceHasTransmissionSignal(surface) && SurfaceSpecularEffectivelySmooth(surface);
}

float3 BSDFWorldToLocal(BSDF bsdf, float3 direction)
{
    return float3(dot(direction, bsdf.tangent), dot(direction, bsdf.bitangent), dot(direction, bsdf.normal));
}

void OrientBSDFForView(inout BSDF bsdf, float3 woWorld)
{
    float cosG = dot(woWorld, bsdf.geomNormal);
    float cosS = dot(woWorld, bsdf.normal);
    if (cosS * cosG > 0.0 && abs(cosS) > 1e-4)
        return;

    float3 fallbackNormal = (cosG >= 0.0) ? bsdf.geomNormal : -bsdf.geomNormal;
    float3 t;
    float3 b;
    BuildBasis(fallbackNormal, t, b);
    bsdf.tangent = t;
    bsdf.bitangent = b;
    bsdf.normal = fallbackNormal;
}

float3 BSDFLocalToWorld(BSDF bsdf, float3 direction)
{
    return bsdf.tangent * direction.x + bsdf.bitangent * direction.y + bsdf.normal * direction.z;
}

float3 ReflectVector(float3 wo, float3 n)
{
    return -wo + 2.0 * dot(wo, n) * n;
}

void GetReflectionTransmissionProbabilities(float reflectionWeight, float transmissionWeight, uint sampleFlags, out float reflectionProbability, out float transmissionProbability, out float totalWeight)
{
    float allowedReflection = (sampleFlags & BXDF_FLAG_REFLECTION) != 0u ? reflectionWeight : 0.0;
    float allowedTransmission = (sampleFlags & BXDF_FLAG_TRANSMISSION) != 0u ? transmissionWeight : 0.0;
    totalWeight = allowedReflection + allowedTransmission;
    if (totalWeight <= 1e-6)
    {
        reflectionProbability = 1.0;
        transmissionProbability = 0.0;
        totalWeight = 0.0;
        return;
    }

    reflectionProbability = allowedReflection / totalWeight;
    transmissionProbability = allowedTransmission / totalWeight;
}

void GetDielectricLobeProbabilities(float fresnel, float transmissionWeight, uint sampleFlags, out float reflectionProbability, out float transmissionProbability, out float totalWeight)
{
    GetReflectionTransmissionProbabilities(saturate(fresnel), saturate((1.0 - fresnel) * transmissionWeight), sampleFlags, reflectionProbability, transmissionProbability, totalWeight);
}

void GetLocalDielectricEta(uint surfaceFrontFace, float eta, out float etaI, out float etaT, out float relativeEta)
{
    bool frontFace = surfaceFrontFace != 0u;
    etaI = frontFace ? 1.0 : eta;
    etaT = frontFace ? eta : 1.0;
    relativeEta = etaI / max(etaT, 1e-6);
}

float3 ComputeVolumeAttenuationFromTint(float3 tint, float distanceInVolume, float transmissionDepth)
{
    float3 safeTint = max(saturate(tint), float3(1e-3, 1e-3, 1e-3));
    float depth = transmissionDepth > 0.0 ? transmissionDepth : 1.0;
    float3 sigmaA = -log(safeTint) / max(depth, 1e-6);
    return exp(-sigmaA * max(distanceInVolume, 0.0));
}

float3 ComputeThinDielectricTransmission(SurfaceData surface, float3 rayDirection)
{
    float cosTheta = abs(dot(surface.normal, -rayDirection));
    float fresnel = FresnelDielectric(cosTheta, 1.0, ResolveSurfaceSpecularIor(surface));
    float transmission = ThinDielectricTransmittance(fresnel) * surface.transmissionWeight;
    return surface.transmissionColor * transmission;
}

#endif
