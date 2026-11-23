#ifndef LUCENT_PATH_BSDF_LOBES_INCLUDED
#define LUCENT_PATH_BSDF_LOBES_INCLUDED

#include "BsdfFoundation.hlsl"
#include "BsdfMultiScatterLut.hlsl"
#include "BsdfClearcoat.hlsl"
#include "BsdfSheen.hlsl"

bool IsReflectionOnlySpecularBxDF(BxDF bxdf)
{
    return bxdf.kind == BXDF_KIND_CONDUCTOR || (bxdf.kind == BXDF_KIND_DIELECTRIC && bxdf.transmissionWeight <= 0.0);
}

float GetSpecularReflectionSelectionWeight(BxDF bxdf, float3 wo)
{
    float cosThetaO = AbsCosTheta(wo);
    float roughness = sqrt(sqrt(max(bxdf.alphaX * bxdf.alphaY, 1e-8)));
    float directionalAlbedo = MsSampleDirectionalAlbedo(cosThetaO, roughness);

    if (bxdf.kind == BXDF_KIND_CONDUCTOR)
    {
        float3 conductorF0 = FresnelConductor(1.0, bxdf.slotA, bxdf.slotB);
        float3 conductorFresnel = FresnelF82(cosThetaO, conductorF0, bxdf.f82EdgeTint);

        return max(Max3(bxdf.scale * conductorFresnel) * directionalAlbedo, 1e-4);
    }

    float etaI;
    float etaT;
    float relativeEta;
    GetLocalDielectricEta(bxdf.surfaceFrontFace, bxdf.eta, etaI, etaT, relativeEta);

    return max(Max3(bxdf.scale) * FresnelDielectric(cosThetaO, etaI, etaT) * directionalAlbedo, 1e-4);
}

float GetBaseBxDFSelectionWeight(BxDF bxdf, float3 wo)
{
    if (IsReflectionOnlySpecularBxDF(bxdf))
    {
        return GetSpecularReflectionSelectionWeight(bxdf, wo);
    }

    return bxdf.sampleWeight;
}

float BxDFSelectionWeight(BSDF bsdf, BxDF bxdf, float3 wo)
{
    if (bxdf.kind == BXDF_KIND_SHEEN)
        return SheenSelectionWeight(bxdf, wo);

    float sheenAttenuation = SheenBaseWeight(bsdf, wo);

    if (bxdf.kind == BXDF_KIND_CLEARCOAT)
    {
        float coatWeight = saturate(bxdf.scale.x);
        return coatWeight * max(ClearcoatFresnel(bxdf, AbsCosTheta(wo)), 1e-4) * sheenAttenuation;
    }

    return GetBaseBxDFSelectionWeight(bxdf, wo) * ClearcoatBaseWeight(bsdf, wo) * sheenAttenuation;
}

float3 ApplyLayeredAttenuation(BSDF bsdf, BxDF bxdf, float3 wo, float3 wi, float sheenWeight)
{
    if (bxdf.kind == BXDF_KIND_SHEEN)
        return float3(1.0, 1.0, 1.0);
    if (bxdf.kind == BXDF_KIND_CLEARCOAT)
        return sheenWeight.xxx;
    return ClearcoatBaseDirectionalAttenuation(bsdf, wo, wi) * sheenWeight;
}

float SheenBaseWeightSurface(SurfaceData surface, float3 wo)
{
    BxDF sheen = MakeSheenBxDF(surface);
    if (sheen.kind != BXDF_KIND_SHEEN || sheen.sampleWeight <= 0.0)
        return 1.0;

    float sheenAlbedo = SheenDirectionalAlbedo(sheen, AbsCosTheta(wo));
    float sheenStrength = saturate(Max3(sheen.scale));
    return saturate(1.0 - sheenStrength * sheenAlbedo);
}

float ClearcoatBaseWeightSurface(SurfaceData surface, float3 wo)
{
    BxDF coat = MakeClearcoatBxDF(surface);
    if (coat.kind != BXDF_KIND_CLEARCOAT || coat.sampleWeight <= 0.0)
        return 1.0;

    float coatWeight = saturate(Max3(coat.scale));
    float fresnel = ClearcoatFresnel(coat, AbsCosTheta(wo));
    float coatTransmission = saturate(1.0 - coatWeight * fresnel * saturate(coat.transmissionWeight));
    float3 coatTint = lerp(float3(1.0, 1.0, 1.0), ResolveClearcoatSubstrateTint(coat), coatWeight);
    return saturate(coatTransmission * ComputeLuminance(coatTint));
}

float3 ClearcoatBaseDirectionalAttenuationSurface(SurfaceData surface, float3 wo, float3 wi)
{
    BxDF coat = MakeClearcoatBxDF(surface);
    if (coat.kind != BXDF_KIND_CLEARCOAT || coat.sampleWeight <= 0.0)
        return float3(1.0, 1.0, 1.0);

    float coatWeight = saturate(Max3(coat.scale));
    float darkening = saturate(coat.transmissionWeight);
    float woTransmittance = 1.0 - darkening * ClearcoatFresnel(coat, AbsCosTheta(wo));
    float wiTransmittance = 1.0 - darkening * ClearcoatFresnel(coat, AbsCosTheta(wi));
    float coatTransmission = saturate(woTransmittance * wiTransmittance);
    float3 coatTint = ResolveClearcoatSubstrateTint(coat);
    return lerp(float3(1.0, 1.0, 1.0), coatTransmission.xxx * coatTint, coatWeight);
}

float BxDFSelectionWeight(SurfaceData surface, BxDF bxdf, float3 wo)
{
    if (bxdf.kind == BXDF_KIND_SHEEN)
        return SheenSelectionWeight(bxdf, wo);

    float sheenAttenuation = SheenBaseWeightSurface(surface, wo);

    if (bxdf.kind == BXDF_KIND_CLEARCOAT)
    {
        float coatWeight = saturate(bxdf.scale.x);
        return coatWeight * max(ClearcoatFresnel(bxdf, AbsCosTheta(wo)), 1e-4) * sheenAttenuation;
    }

    return GetBaseBxDFSelectionWeight(bxdf, wo) * ClearcoatBaseWeightSurface(surface, wo) * sheenAttenuation;
}

float3 ApplyLayeredAttenuation(SurfaceData surface, BxDF bxdf, float3 wo, float3 wi, float sheenWeight)
{
    if (bxdf.kind == BXDF_KIND_SHEEN)
        return float3(1.0, 1.0, 1.0);
    if (bxdf.kind == BXDF_KIND_CLEARCOAT)
        return sheenWeight.xxx;
    return ClearcoatBaseDirectionalAttenuationSurface(surface, wo, wi) * sheenWeight;
}

float FrameShadingNormalCorrection(ShadingFrame frame, float3 woWorld, float3 wiWorld, uint transportMode)
{
    if (transportMode == TRANSPORT_MODE_RADIANCE)
        return 1.0;

    float denominator = abs(dot(woWorld, frame.geomNormal)) * abs(dot(wiWorld, frame.normal));
    if (denominator <= 1e-6)
        return 0.0;

    float numerator = abs(dot(woWorld, frame.normal)) * abs(dot(wiWorld, frame.geomNormal));
    return numerator / denominator;
}

float3 Diffuse_f(BxDF bxdf, float3 wo, float3 wi)
{
    if (!SameHemisphere(wo, wi) || AbsCosTheta(wo) <= 0.0 || AbsCosTheta(wi) <= 0.0)
        return 0.0;

    float roughness = saturate(bxdf.alphaX);
    if (roughness <= 1e-4)
        return bxdf.scale * INV_PI;

    float noL = AbsCosTheta(wi);
    float noV = AbsCosTheta(wo);
    float s = dot(wi, wo) - noL * noV;
    float t = s <= 0.0 ? 1.0 : max(noL, noV);
    float roughnessDenom = PI + (0.5 * PI - 2.0 / 3.0) * roughness;
    float A = rcp(roughnessDenom);
    float B = roughness * A;
    return bxdf.scale * max(A + B * s / max(t, 1e-6), 0.0);
}

float Diffuse_PDF(float3 wo, float3 wi)
{
    if (!SameHemisphere(wo, wi))
        return 0.0;

    return AbsCosTheta(wi) * INV_PI;
}

bool Diffuse_Sample(BxDF bxdf, float3 wo, float2 u, out BSDFSample sample)
{
    sample.wi = SampleCosineHemisphereLocal(u);
    if (wo.z < 0.0)
        sample.wi.z *= -1.0;

    sample.f = Diffuse_f(bxdf, wo, sample.wi);
    sample.pdf = Diffuse_PDF(wo, sample.wi);
    sample.flags = bxdf.flags;
    sample.eta = 1.0;
    return sample.pdf > 1e-6 && Max3(sample.f) > 1e-6;
}

float3 DiffuseTransmission_f(BxDF bxdf, float3 wo, float3 wi)
{
    if (SameHemisphere(wo, wi) || AbsCosTheta(wo) <= 0.0 || AbsCosTheta(wi) <= 0.0)
    {
        return 0.0;
    }

    return Diffuse_f(bxdf, wo, float3(wi.x, wi.y, -wi.z));
}

float DiffuseTransmission_PDF(float3 wo, float3 wi)
{
    if (SameHemisphere(wo, wi))
    {
        return 0.0;
    }

    return AbsCosTheta(wi) * INV_PI;
}

bool DiffuseTransmission_Sample(BxDF bxdf, float3 wo, float2 u, out BSDFSample sample)
{
    sample.wi = SampleCosineHemisphereLocal(u);
    if (wo.z >= 0.0)
    {
        sample.wi.z *= -1.0;
    }

    sample.f = DiffuseTransmission_f(bxdf, wo, sample.wi);
    sample.pdf = DiffuseTransmission_PDF(wo, sample.wi);
    sample.flags = bxdf.flags;
    sample.eta = 1.0;

    return sample.pdf > 1e-6 && Max3(sample.f) > 1e-6;
}

float3 Conductor_f(BxDF bxdf, float3 wo, float3 wi)
{
    if (!SameHemisphere(wo, wi) || AbsCosTheta(wo) <= 0.0 || AbsCosTheta(wi) <= 0.0)
        return 0.0;

    TrowbridgeReitzDistribution distribution;
    distribution.alphaX = bxdf.alphaX;
    distribution.alphaY = bxdf.alphaY;

    float3 wm = SafeNormalize(wo + wi, float3(0.0, 0.0, 1.0));
    if (wm.z < 0.0)
        wm = -wm;

    float woDotH = abs(dot(wo, wm));
    float wiDotH = abs(dot(wi, wm));
    if (woDotH <= 0.0 || wiDotH <= 0.0)
        return 0.0;

    float D = TrowbridgeReitz_D(distribution, wm);
    float G = TrowbridgeReitz_G(distribution, wo, wi);
    float3 F0 = FresnelConductor(1.0, bxdf.slotA, bxdf.slotB);
    float3 F = FresnelF82(woDotH, F0, bxdf.f82EdgeTint);
    F *= ThinFilmRgbInterference(woDotH, bxdf.thinFilmWeight, bxdf.thinFilmThickness, bxdf.thinFilmIor);
    float3 fSingle = bxdf.scale * F * (D * G / max(4.0 * AbsCosTheta(wo) * AbsCosTheta(wi), 1e-6));

    float roughness = sqrt(sqrt(max(bxdf.alphaX * bxdf.alphaY, 1e-8)));
    return MsMultiScatterCompensation(fSingle, F0 * bxdf.scale, AbsCosTheta(wo), AbsCosTheta(wi), roughness);
}

float Conductor_PDF(BxDF bxdf, float3 wo, float3 wi)
{
    if (!SameHemisphere(wo, wi) || AbsCosTheta(wo) <= 0.0 || AbsCosTheta(wi) <= 0.0)
        return 0.0;

    TrowbridgeReitzDistribution distribution;
    distribution.alphaX = bxdf.alphaX;
    distribution.alphaY = bxdf.alphaY;

    float3 wm = SafeNormalize(wo + wi, float3(0.0, 0.0, 1.0));
    if (wm.z < 0.0)
        wm = -wm;

    return TrowbridgeReitz_PDF(distribution, wo, wm) / max(4.0 * abs(dot(wo, wm)), 1e-6);
}

bool Conductor_Sample(BxDF bxdf, float3 wo, float2 u, out BSDFSample sample)
{
    TrowbridgeReitzDistribution distribution;
    distribution.alphaX = bxdf.alphaX;
    distribution.alphaY = bxdf.alphaY;

    float3 wm = TrowbridgeReitz_Sample_wm(distribution, wo, u);
    sample.wi = ReflectVector(wo, wm);
    if (!SameHemisphere(wo, sample.wi))
    {
        sample.f = 0.0;
        sample.pdf = 0.0;
        sample.flags = 0u;
        sample.eta = 1.0;
        return false;
    }
    sample.f = Conductor_f(bxdf, wo, sample.wi);
    sample.pdf = Conductor_PDF(bxdf, wo, sample.wi);
    sample.flags = BXDF_FLAG_REFLECTION | ((bxdf.flags & BXDF_FLAG_SPECULAR) != 0u ? BXDF_FLAG_SPECULAR : BXDF_FLAG_GLOSSY);
    sample.eta = 1.0;
    return sample.pdf > 1e-6 && Max3(sample.f) > 1e-6;
}

float3 Dielectric_f(BxDF bxdf, float3 wo, float3 wi, uint transportMode)
{
    float absCosWo = AbsCosTheta(wo);
    float absCosWi = AbsCosTheta(wi);
    if (absCosWo <= 0.0 || absCosWi <= 0.0)
        return 0.0;

    bool reflect = SameHemisphere(wo, wi);
    TrowbridgeReitzDistribution distribution;
    distribution.alphaX = bxdf.alphaX;
    distribution.alphaY = bxdf.alphaY;

    float etaI;
    float etaT;
    float relativeEta;
    GetLocalDielectricEta(bxdf.surfaceFrontFace, bxdf.eta, etaI, etaT, relativeEta);

    float etap = rcp(max(relativeEta, 1e-6));
    float3 wm = reflect ? SafeNormalize(wo + wi, float3(0.0, 0.0, 1.0)) : SafeNormalize(wo + wi * etap, float3(0.0, 0.0, 1.0));
    if (wm.z < 0.0)
        wm = -wm;

    float woDotH = dot(wo, wm);
    float wiDotH = dot(wi, wm);
    if (woDotH * wo.z <= 0.0 || wiDotH * wi.z <= 0.0)
        return 0.0;

    float D = TrowbridgeReitz_D(distribution, wm);
    float G = TrowbridgeReitz_G(distribution, wo, wi);
    float F = FresnelDielectric(abs(woDotH), etaI, etaT);
    if (reflect)
    {
        float3 thinFilm = ThinFilmRgbInterference(abs(woDotH), bxdf.thinFilmWeight, bxdf.thinFilmThickness, bxdf.thinFilmIor);
        float3 fSingle = thinFilm * (D * G * F / max(4.0 * absCosWo * absCosWi, 1e-6));
        float dielectricF0 = (etaT - etaI) / max(etaT + etaI, 1e-6);
        dielectricF0 = dielectricF0 * dielectricF0;
        float roughness = sqrt(sqrt(max(bxdf.alphaX * bxdf.alphaY, 1e-8)));
        return bxdf.scale * MsMultiScatterCompensation(fSingle, dielectricF0.xxx, absCosWo, absCosWi, roughness);
    }

    float denomTerm = wiDotH + woDotH * relativeEta;
    float denomSq = denomTerm * denomTerm;
    if (denomSq <= 1e-6 || bxdf.transmissionWeight <= 0.0)
        return 0.0;

    float ft = bxdf.transmissionWeight * (1.0 - F) * D * G * abs(wiDotH * woDotH) / max(absCosWo * absCosWi * denomSq, 1e-6);
    return bxdf.slotA * bxdf.slotB * ft.xxx;
}

float Dielectric_PDF(BxDF bxdf, float3 wo, float3 wi, uint sampleFlags)
{
    float absCosWo = AbsCosTheta(wo);
    float absCosWi = AbsCosTheta(wi);
    if (absCosWo <= 0.0 || absCosWi <= 0.0)
        return 0.0;

    bool reflect = SameHemisphere(wo, wi);
    TrowbridgeReitzDistribution distribution;
    distribution.alphaX = bxdf.alphaX;
    distribution.alphaY = bxdf.alphaY;

    float etaI;
    float etaT;
    float relativeEta;
    GetLocalDielectricEta(bxdf.surfaceFrontFace, bxdf.eta, etaI, etaT, relativeEta);

    float etap = rcp(max(relativeEta, 1e-6));
    float3 wm = reflect ? SafeNormalize(wo + wi, float3(0.0, 0.0, 1.0)) : SafeNormalize(wo + wi * etap, float3(0.0, 0.0, 1.0));
    if (wm.z < 0.0)
        wm = -wm;

    float woDotH = dot(wo, wm);
    float wiDotH = dot(wi, wm);
    if (woDotH * wo.z <= 0.0 || wiDotH * wi.z <= 0.0)
        return 0.0;

    float F = FresnelDielectric(abs(woDotH), etaI, etaT);
    float reflectionProbability;
    float transmissionProbability;
    float totalWeight;
    GetDielectricLobeProbabilities(F, bxdf.transmissionWeight, sampleFlags, reflectionProbability, transmissionProbability, totalWeight);
    if (totalWeight <= 1e-6)
        return 0.0;

    float wmPdf = TrowbridgeReitz_PDF(distribution, wo, wm);
    if (reflect)
        return wmPdf * reflectionProbability / max(4.0 * abs(woDotH), 1e-6);

    float denomTerm = wiDotH + woDotH * relativeEta;
    float denomSq = denomTerm * denomTerm;
    if (denomSq <= 1e-6 || transmissionProbability <= 0.0)
        return 0.0;

    return wmPdf * abs(wiDotH) * transmissionProbability / denomSq;
}

bool Dielectric_Sample(BxDF bxdf, float3 wo, float uSelect, float2 u, uint transportMode, uint sampleFlags, out BSDFSample sample)
{
    sample.wi = 0.0;
    sample.f = 0.0;
    sample.pdf = 0.0;
    sample.flags = 0u;
    sample.eta = 1.0;

    TrowbridgeReitzDistribution distribution;
    distribution.alphaX = bxdf.alphaX;
    distribution.alphaY = bxdf.alphaY;

    float etaI;
    float etaT;
    float relativeEta;
    GetLocalDielectricEta(bxdf.surfaceFrontFace, bxdf.eta, etaI, etaT, relativeEta);

    if (AbsCosTheta(wo) <= 0.0)
        return false;

    float3 wm = TrowbridgeReitz_Sample_wm(distribution, wo, u);
    if (dot(wo, wm) * wo.z <= 0.0)
        return false;

    float fresnel = FresnelDielectric(abs(dot(wo, wm)), etaI, etaT);
    float reflectionProbability;
    float transmissionProbability;
    float totalWeight;
    GetDielectricLobeProbabilities(fresnel, bxdf.transmissionWeight, sampleFlags, reflectionProbability, transmissionProbability, totalWeight);
    if (totalWeight <= 1e-6)
        return false;

    uint specularFlag = (bxdf.flags & BXDF_FLAG_SPECULAR) != 0u ? BXDF_FLAG_SPECULAR : BXDF_FLAG_GLOSSY;

    if (transmissionProbability <= 0.0 || uSelect < reflectionProbability)
    {
        sample.wi = ReflectVector(wo, wm);
        if (!SameHemisphere(wo, sample.wi))
            return false;
        sample.f = Dielectric_f(bxdf, wo, sample.wi, transportMode);
        sample.pdf = Dielectric_PDF(bxdf, wo, sample.wi, sampleFlags);
        sample.flags = BXDF_FLAG_REFLECTION | specularFlag;
        sample.eta = 1.0;
        return sample.pdf > 1e-6 && Max3(sample.f) > 1e-6;
    }

    if (!RefractDirection(-wo, wm, etaI, etaT, sample.wi))
        return false;
    if (SameHemisphere(wo, sample.wi))
        return false;

    sample.f = Dielectric_f(bxdf, wo, sample.wi, transportMode);
    sample.pdf = Dielectric_PDF(bxdf, wo, sample.wi, sampleFlags);
    sample.flags = BXDF_FLAG_TRANSMISSION | specularFlag;
    sample.eta = etaT / max(etaI, 1e-6);
    return sample.pdf > 1e-6 && Max3(sample.f) > 1e-6;
}

float3 ThinDielectric_f(BxDF bxdf, float3 wo, float3 wi);
float ThinDielectric_PDF(BxDF bxdf, float3 wo, float3 wi, uint sampleFlags);

bool ThinDielectric_Sample(BxDF bxdf, float3 wo, float uSelect, float2 u, uint sampleFlags, out BSDFSample sample)
{
    sample.wi = 0.0;
    sample.f = 0.0;
    sample.pdf = 0.0;
    sample.flags = 0u;
    sample.eta = 1.0;

    float absCosWo = AbsCosTheta(wo);
    if (absCosWo <= 0.0)
        return false;

    float fresnel = FresnelDielectric(absCosWo, 1.0, bxdf.eta);
    float reflectionWeight = ThinDielectricReflectance(fresnel);
    float transmissionWeight = ThinDielectricTransmittance(fresnel) * bxdf.transmissionWeight;
    float reflectionProbability;
    float transmissionProbability;
    float totalWeight;
    GetReflectionTransmissionProbabilities(reflectionWeight, transmissionWeight, sampleFlags, reflectionProbability, transmissionProbability, totalWeight);
    if (totalWeight <= 1e-6)
        return false;

    if (transmissionProbability <= 0.0 || uSelect < reflectionProbability)
    {
        uint specularFlag = (bxdf.flags & BXDF_FLAG_GLOSSY) != 0u ? BXDF_FLAG_GLOSSY : BXDF_FLAG_SPECULAR;
        if ((bxdf.flags & BXDF_FLAG_GLOSSY) != 0u)
        {
            TrowbridgeReitzDistribution distribution;
            distribution.alphaX = bxdf.alphaX;
            distribution.alphaY = bxdf.alphaY;

            float3 wm = TrowbridgeReitz_Sample_wm(distribution, wo, u);
            sample.wi = ReflectVector(wo, wm);
            if (!SameHemisphere(wo, sample.wi))
                return false;
        }
        else
        {
            sample.wi = ReflectVector(wo, float3(0.0, 0.0, 1.0));
        }

        sample.f = ThinDielectric_f(bxdf, wo, sample.wi);
        sample.pdf = ThinDielectric_PDF(bxdf, wo, sample.wi, sampleFlags);
        sample.flags = BXDF_FLAG_REFLECTION | specularFlag;
        sample.eta = 1.0;
        return sample.pdf > 1e-6 && Max3(sample.f) > 1e-6;
    }

    uint transmissionFlag = (bxdf.flags & BXDF_FLAG_GLOSSY) != 0u ? BXDF_FLAG_GLOSSY : BXDF_FLAG_SPECULAR;
    if ((bxdf.flags & BXDF_FLAG_GLOSSY) != 0u)
    {
        TrowbridgeReitzDistribution distribution;
        distribution.alphaX = bxdf.alphaX;
        distribution.alphaY = bxdf.alphaY;

        float3 reflectedWo = ReflectVector(wo, float3(0.0, 0.0, 1.0));
        float3 wm = TrowbridgeReitz_Sample_wm(distribution, reflectedWo, u);
        sample.wi = ReflectVector(reflectedWo, wm);
        if (SameHemisphere(wo, sample.wi))
            return false;

        sample.f = ThinDielectric_f(bxdf, wo, sample.wi);
        sample.pdf = ThinDielectric_PDF(bxdf, wo, sample.wi, sampleFlags);
    }
    else
    {
        sample.wi = -wo;
        sample.f = bxdf.slotA * bxdf.slotB * (transmissionWeight / max(AbsCosTheta(sample.wi), 1e-6));
        sample.pdf = transmissionProbability;
    }

    sample.flags = BXDF_FLAG_TRANSMISSION | transmissionFlag;
    sample.eta = 1.0;
    return sample.pdf > 1e-6 && Max3(sample.f) > 1e-6;
}

float3 ThinDielectric_f(BxDF bxdf, float3 wo, float3 wi)
{
    float absCosWo = AbsCosTheta(wo);
    float absCosWi = AbsCosTheta(wi);
    if (absCosWo <= 0.0 || absCosWi <= 0.0)
        return 0.0;

    bool reflect = SameHemisphere(wo, wi);
    float fresnel = FresnelDielectric(absCosWo, 1.0, bxdf.eta);
    if (reflect)
    {
        if ((bxdf.flags & BXDF_FLAG_GLOSSY) != 0u)
        {
            TrowbridgeReitzDistribution distribution;
            distribution.alphaX = bxdf.alphaX;
            distribution.alphaY = bxdf.alphaY;

            float3 wm = SafeNormalize(wo + wi, float3(0.0, 0.0, 1.0));
            if (wm.z < 0.0)
                wm = -wm;

            float woDotH = abs(dot(wo, wm));
            float wiDotH = abs(dot(wi, wm));
            if (woDotH <= 0.0 || wiDotH <= 0.0)
                return 0.0;

            float D = TrowbridgeReitz_D(distribution, wm);
            float G = TrowbridgeReitz_G(distribution, wo, wi);
            float F = ThinDielectricReflectance(FresnelDielectric(woDotH, 1.0, bxdf.eta));
            float3 thinFilm = ThinFilmRgbInterference(woDotH, bxdf.thinFilmWeight, bxdf.thinFilmThickness, bxdf.thinFilmIor);
            float roughness = sqrt(sqrt(max(bxdf.alphaX * bxdf.alphaY, 1e-8)));
            float ms = MsNonAbsorbingCompensation(absCosWo, absCosWi, roughness);
            return bxdf.scale * thinFilm * (F * (D * G / max(4.0 * absCosWo * absCosWi, 1e-6) + ms));
        }

        float3 mirrorWi = ReflectVector(wo, float3(0.0, 0.0, 1.0));
        if (dot(mirrorWi - wi, mirrorWi - wi) <= 1e-6)
        {
            float3 thinFilm = ThinFilmRgbInterference(absCosWo, bxdf.thinFilmWeight, bxdf.thinFilmThickness, bxdf.thinFilmIor);
            return bxdf.scale * thinFilm * (ThinDielectricReflectance(fresnel) / max(absCosWi, 1e-6));
        }

        return 0.0;
    }

    float transmissionWeight = ThinDielectricTransmittance(fresnel) * bxdf.transmissionWeight;
    if ((bxdf.flags & BXDF_FLAG_GLOSSY) != 0u)
    {
        TrowbridgeReitzDistribution distribution;
        distribution.alphaX = bxdf.alphaX;
        distribution.alphaY = bxdf.alphaY;

        float3 reflectedWo = ReflectVector(wo, float3(0.0, 0.0, 1.0));
        float3 wm = SafeNormalize(reflectedWo + wi, float3(0.0, 0.0, -1.0));
        if (wm.z > 0.0)
            wm = -wm;

        float woDotH = abs(dot(reflectedWo, wm));
        float wiDotH = abs(dot(wi, wm));
        if (woDotH <= 0.0 || wiDotH <= 0.0)
            return 0.0;

        float D = TrowbridgeReitz_D(distribution, wm);
        float G = TrowbridgeReitz_G(distribution, reflectedWo, wi);
        float roughness = sqrt(sqrt(max(bxdf.alphaX * bxdf.alphaY, 1e-8)));
        float ms = MsNonAbsorbingCompensation(absCosWo, absCosWi, roughness);
        return bxdf.slotA * bxdf.slotB *
            (transmissionWeight * (D * G / max(4.0 * absCosWo * absCosWi, 1e-6) + ms));
    }

    if (dot(wi + wo, wi + wo) <= 1e-6)
        return bxdf.slotA * bxdf.slotB * (transmissionWeight / max(absCosWi, 1e-6));

    return 0.0;
}

float ThinDielectric_PDF(BxDF bxdf, float3 wo, float3 wi, uint sampleFlags)
{
    float absCosWo = AbsCosTheta(wo);
    if (absCosWo <= 0.0 || AbsCosTheta(wi) <= 0.0)
        return 0.0;

    float fresnel = FresnelDielectric(absCosWo, 1.0, bxdf.eta);
    float reflectionProbability;
    float transmissionProbability;
    float totalWeight;
    GetReflectionTransmissionProbabilities(ThinDielectricReflectance(fresnel), ThinDielectricTransmittance(fresnel) * bxdf.transmissionWeight, sampleFlags, reflectionProbability, transmissionProbability, totalWeight);
    if (totalWeight <= 1e-6)
        return 0.0;

    TrowbridgeReitzDistribution distribution;
    distribution.alphaX = bxdf.alphaX;
    distribution.alphaY = bxdf.alphaY;

    if (!SameHemisphere(wo, wi))
    {
        if ((bxdf.flags & BXDF_FLAG_GLOSSY) == 0u)
            return transmissionProbability;

        float3 reflectedWo = ReflectVector(wo, float3(0.0, 0.0, 1.0));
        float3 wm = SafeNormalize(reflectedWo + wi, float3(0.0, 0.0, -1.0));
        if (wm.z > 0.0)
            wm = -wm;

        float woDotH = abs(dot(reflectedWo, wm));
        if (woDotH <= 0.0)
            return 0.0;

        return TrowbridgeReitz_PDF(distribution, reflectedWo, wm) * transmissionProbability / max(4.0 * woDotH, 1e-6);
    }

    if ((bxdf.flags & BXDF_FLAG_GLOSSY) == 0u)
        return reflectionProbability;

    float3 wm = SafeNormalize(wo + wi, float3(0.0, 0.0, 1.0));
    if (wm.z < 0.0)
        wm = -wm;

    float woDotH = abs(dot(wo, wm));
    if (woDotH <= 0.0)
        return 0.0;

    return TrowbridgeReitz_PDF(distribution, wo, wm) * reflectionProbability / max(4.0 * woDotH, 1e-6);
}

float3 BxDF_f(BxDF bxdf, float3 wo, float3 wi, uint transportMode)
{
    if (bxdf.kind == BXDF_KIND_DIFFUSE)
        return Diffuse_f(bxdf, wo, wi);

    if (bxdf.kind == BXDF_KIND_DIELECTRIC)
        return Dielectric_f(bxdf, wo, wi, transportMode);

#if !defined(LUCENT_SHADE_SIMPLE)
    if (bxdf.kind == BXDF_KIND_CONDUCTOR)
        return Conductor_f(bxdf, wo, wi);

    if (bxdf.kind == BXDF_KIND_THIN_DIELECTRIC)
        return ThinDielectric_f(bxdf, wo, wi);

    if (bxdf.kind == BXDF_KIND_CLEARCOAT)
        return Clearcoat_f(bxdf, wo, wi);

    if (bxdf.kind == BXDF_KIND_SHEEN)
        return Sheen_f(bxdf, wo, wi);

    if (bxdf.kind == BXDF_KIND_DIFFUSE_TRANSMISSION)
        return DiffuseTransmission_f(bxdf, wo, wi);
#endif

    return 0.0;
}

float BxDF_PDF(BxDF bxdf, float3 wo, float3 wi, uint sampleFlags)
{
    if (bxdf.kind == BXDF_KIND_DIFFUSE)
        return Diffuse_PDF(wo, wi);

    if (bxdf.kind == BXDF_KIND_DIELECTRIC)
        return Dielectric_PDF(bxdf, wo, wi, sampleFlags);

#if !defined(LUCENT_SHADE_SIMPLE)
    if (bxdf.kind == BXDF_KIND_CONDUCTOR)
        return Conductor_PDF(bxdf, wo, wi);

    if (bxdf.kind == BXDF_KIND_THIN_DIELECTRIC)
        return ThinDielectric_PDF(bxdf, wo, wi, sampleFlags);

    if (bxdf.kind == BXDF_KIND_CLEARCOAT)
        return Clearcoat_PDF(bxdf, wo, wi);

    if (bxdf.kind == BXDF_KIND_SHEEN)
        return Sheen_PDF(bxdf, wo, wi);

    if (bxdf.kind == BXDF_KIND_DIFFUSE_TRANSMISSION)
        return DiffuseTransmission_PDF(wo, wi);
#endif

    return 0.0;
}

bool BxDF_Sample(BxDF bxdf, float3 wo, float uSelect, float2 u, uint transportMode, uint sampleFlags, out BSDFSample sample)
{
    sample.wi = 0.0;
    sample.f = 0.0;
    sample.pdf = 0.0;
    sample.flags = 0u;
    sample.eta = 1.0;

    if (bxdf.kind == BXDF_KIND_DIFFUSE)
        return Diffuse_Sample(bxdf, wo, u, sample);

    if (bxdf.kind == BXDF_KIND_DIELECTRIC)
        return Dielectric_Sample(bxdf, wo, uSelect, u, transportMode, sampleFlags, sample);

#if !defined(LUCENT_SHADE_SIMPLE)
    if (bxdf.kind == BXDF_KIND_CONDUCTOR)
        return Conductor_Sample(bxdf, wo, u, sample);

    if (bxdf.kind == BXDF_KIND_THIN_DIELECTRIC)
        return ThinDielectric_Sample(bxdf, wo, uSelect, u, sampleFlags, sample);

    if (bxdf.kind == BXDF_KIND_CLEARCOAT)
        return Clearcoat_Sample(bxdf, wo, u, sample);

    if (bxdf.kind == BXDF_KIND_SHEEN)
        return Sheen_Sample(bxdf, wo, u, sample);

    if (bxdf.kind == BXDF_KIND_DIFFUSE_TRANSMISSION)
        return DiffuseTransmission_Sample(bxdf, wo, u, sample);
#endif

    return false;
}

float ShadingNormalCorrection(BSDF bsdf, float3 woWorld, float3 wiWorld, uint transportMode)
{
    if (transportMode == TRANSPORT_MODE_RADIANCE)
        return 1.0;

    float denominator = abs(dot(woWorld, bsdf.geomNormal)) * abs(dot(wiWorld, bsdf.normal));
    if (denominator <= 1e-6)
        return 0.0;

    float numerator = abs(dot(woWorld, bsdf.normal)) * abs(dot(wiWorld, bsdf.geomNormal));
    return numerator / denominator;
}

float3 BSDF_f(BSDF bsdf, float3 woWorld, float3 wiWorld, uint transportMode, uint sampleFlags)
{
    if (BSDFIsEmpty(bsdf))
        return 0.0;

    OrientBSDFForView(bsdf, woWorld);
    float3 wo = BSDFWorldToLocal(bsdf, woWorld);
    float3 wi = BSDFWorldToLocal(bsdf, wiWorld);
    if (AbsCosTheta(wo) <= 0.0 || AbsCosTheta(wi) <= 0.0)
        return 0.0;

    bool reflect = dot(woWorld, bsdf.geomNormal) * dot(wiWorld, bsdf.geomNormal) > 0.0;
    float3 f = 0.0;
    float sheenWeight = SheenBaseWeight(bsdf, wo);
    for (uint evalIndex = 0u; evalIndex < 6u; evalIndex++)
    {
        if (evalIndex >= BSDFSelectableComponentCount(bsdf))
            break;

        BxDF bxdf = BSDFGetSelectableComponent(bsdf, evalIndex);
        if (!BxDFMatchesFlags(bxdf, sampleFlags))
            continue;

        bool componentReflects = (bxdf.flags & BXDF_FLAG_REFLECTION) != 0u;
        bool componentTransmits = (bxdf.flags & BXDF_FLAG_TRANSMISSION) != 0u;
        if ((reflect && !componentReflects) || (!reflect && !componentTransmits))
            continue;

        float3 contribution = BxDF_f(bxdf, wo, wi, transportMode);
        contribution *= ApplyLayeredAttenuation(bsdf, bxdf, wo, wi, sheenWeight);
        f += contribution;
    }

    return f * ShadingNormalCorrection(bsdf, woWorld, wiWorld, transportMode);
}

float3 BSDF_f(SurfaceData surface, ShadingFrame frame, LobeSet set, float3 woWorld, float3 wiWorld, uint transportMode, uint sampleFlags)
{
    if (SelectableLobeCount(set) == 0u)
        return 0.0;

    float3 wo = FrameWorldToLocal(frame, woWorld);
    float3 wi = FrameWorldToLocal(frame, wiWorld);
    if (AbsCosTheta(wo) <= 0.0 || AbsCosTheta(wi) <= 0.0)
        return 0.0;

    bool reflect = dot(woWorld, frame.geomNormal) * dot(wiWorld, frame.geomNormal) > 0.0;
    float3 f = 0.0;
    float sheenWeight = SheenBaseWeightSurface(surface, wo);
    for (uint evalIndex = 0u; evalIndex < 6u; evalIndex++)
    {
        if (evalIndex >= SelectableLobeCount(set))
            break;

        BxDF bxdf = BuildPresentLobe(surface, set, evalIndex);
        if (!BxDFMatchesFlags(bxdf, sampleFlags))
            continue;

        bool componentReflects = (bxdf.flags & BXDF_FLAG_REFLECTION) != 0u;
        bool componentTransmits = (bxdf.flags & BXDF_FLAG_TRANSMISSION) != 0u;
        if ((reflect && !componentReflects) || (!reflect && !componentTransmits))
            continue;

        float3 contribution = BxDF_f(bxdf, wo, wi, transportMode);
        contribution *= ApplyLayeredAttenuation(surface, bxdf, wo, wi, sheenWeight);
        f += contribution;
    }

    return f * FrameShadingNormalCorrection(frame, woWorld, wiWorld, transportMode);
}

float BSDF_PDF(BSDF bsdf, float3 woWorld, float3 wiWorld, uint sampleFlags)
{
    if (BSDFIsEmpty(bsdf))
        return 0.0;

    OrientBSDFForView(bsdf, woWorld);
    float3 wo = BSDFWorldToLocal(bsdf, woWorld);
    float3 wi = BSDFWorldToLocal(bsdf, wiWorld);
    if (AbsCosTheta(wo) <= 0.0 || AbsCosTheta(wi) <= 0.0)
        return 0.0;

    bool reflect = dot(woWorld, bsdf.geomNormal) * dot(wiWorld, bsdf.geomNormal) > 0.0;
    float totalSelectionWeight = 0.0;
    float pdf = 0.0;

    for (uint pdfWeightIndex = 0u; pdfWeightIndex < 6u; pdfWeightIndex++)
    {
        if (pdfWeightIndex >= BSDFSelectableComponentCount(bsdf))
            break;

        BxDF bxdf = BSDFGetSelectableComponent(bsdf, pdfWeightIndex);
        if (!BxDFMatchesFlags(bxdf, sampleFlags))
            continue;

        totalSelectionWeight += BxDFSelectionWeight(bsdf, bxdf, wo);
    }

    if (totalSelectionWeight <= 0.0)
        return 0.0;

    for (uint pdfEvalIndex = 0u; pdfEvalIndex < 6u; pdfEvalIndex++)
    {
        if (pdfEvalIndex >= BSDFSelectableComponentCount(bsdf))
            break;

        BxDF bxdf = BSDFGetSelectableComponent(bsdf, pdfEvalIndex);
        if (!BxDFMatchesFlags(bxdf, sampleFlags))
            continue;

        bool componentReflects = (bxdf.flags & BXDF_FLAG_REFLECTION) != 0u;
        bool componentTransmits = (bxdf.flags & BXDF_FLAG_TRANSMISSION) != 0u;
        if ((reflect && !componentReflects) || (!reflect && !componentTransmits))
            continue;

        pdf += (BxDFSelectionWeight(bsdf, bxdf, wo) / totalSelectionWeight) * BxDF_PDF(bxdf, wo, wi, sampleFlags);
    }

    return pdf;
}

float BSDF_PDF(SurfaceData surface, ShadingFrame frame, LobeSet set, float3 woWorld, float3 wiWorld, uint sampleFlags)
{
    if (SelectableLobeCount(set) == 0u)
        return 0.0;

    float3 wo = FrameWorldToLocal(frame, woWorld);
    float3 wi = FrameWorldToLocal(frame, wiWorld);
    if (AbsCosTheta(wo) <= 0.0 || AbsCosTheta(wi) <= 0.0)
        return 0.0;

    bool reflect = dot(woWorld, frame.geomNormal) * dot(wiWorld, frame.geomNormal) > 0.0;
    float totalSelectionWeight = 0.0;
    float pdf = 0.0;

    [loop]
    for (uint pdfWeightIndex = 0u; pdfWeightIndex < 6u; pdfWeightIndex++)
    {
        if (pdfWeightIndex >= SelectableLobeCount(set))
            break;

        BxDF bxdf = BuildPresentLobe(surface, set, pdfWeightIndex);
        if (!BxDFMatchesFlags(bxdf, sampleFlags))
            continue;

        totalSelectionWeight += BxDFSelectionWeight(surface, bxdf, wo);
    }

    if (totalSelectionWeight <= 0.0)
        return 0.0;

    [loop]
    for (uint pdfEvalIndex = 0u; pdfEvalIndex < 6u; pdfEvalIndex++)
    {
        if (pdfEvalIndex >= SelectableLobeCount(set))
            break;

        BxDF bxdf = BuildPresentLobe(surface, set, pdfEvalIndex);
        if (!BxDFMatchesFlags(bxdf, sampleFlags))
            continue;

        bool componentReflects = (bxdf.flags & BXDF_FLAG_REFLECTION) != 0u;
        bool componentTransmits = (bxdf.flags & BXDF_FLAG_TRANSMISSION) != 0u;
        if ((reflect && !componentReflects) || (!reflect && !componentTransmits))
            continue;

        pdf += (BxDFSelectionWeight(surface, bxdf, wo) / totalSelectionWeight) * BxDF_PDF(bxdf, wo, wi, sampleFlags);
    }

    return pdf;
}

void BSDF_f_and_PDF(BSDF bsdf, float3 woWorld, float3 wiWorld, uint transportMode, uint sampleFlags, out float3 f, out float pdf)
{
    f = 0.0;
    pdf = 0.0;
    if (BSDFIsEmpty(bsdf))
        return;

    OrientBSDFForView(bsdf, woWorld);
    float3 wo = BSDFWorldToLocal(bsdf, woWorld);
    float3 wi = BSDFWorldToLocal(bsdf, wiWorld);
    if (AbsCosTheta(wo) <= 0.0 || AbsCosTheta(wi) <= 0.0)
        return;

    bool reflect = dot(woWorld, bsdf.geomNormal) * dot(wiWorld, bsdf.geomNormal) > 0.0;

    float totalSelectionWeight = 0.0;
    for (uint weightIndex = 0u; weightIndex < 6u; weightIndex++)
    {
        if (weightIndex >= BSDFSelectableComponentCount(bsdf))
            break;

        BxDF weightBxdf = BSDFGetSelectableComponent(bsdf, weightIndex);
        if (!BxDFMatchesFlags(weightBxdf, sampleFlags))
            continue;

        totalSelectionWeight += BxDFSelectionWeight(bsdf, weightBxdf, wo);
    }

    float3 fAccum = 0.0;
    float pdfAccum = 0.0;
    float sheenWeight = SheenBaseWeight(bsdf, wo);
    for (uint evalIndex = 0u; evalIndex < 6u; evalIndex++)
    {
        if (evalIndex >= BSDFSelectableComponentCount(bsdf))
            break;

        BxDF bxdf = BSDFGetSelectableComponent(bsdf, evalIndex);
        if (!BxDFMatchesFlags(bxdf, sampleFlags))
            continue;

        bool componentReflects = (bxdf.flags & BXDF_FLAG_REFLECTION) != 0u;
        bool componentTransmits = (bxdf.flags & BXDF_FLAG_TRANSMISSION) != 0u;
        if ((reflect && !componentReflects) || (!reflect && !componentTransmits))
            continue;

        fAccum += BxDF_f(bxdf, wo, wi, transportMode) * ApplyLayeredAttenuation(bsdf, bxdf, wo, wi, sheenWeight);
        if (totalSelectionWeight > 0.0)
            pdfAccum += (BxDFSelectionWeight(bsdf, bxdf, wo) / totalSelectionWeight) * BxDF_PDF(bxdf, wo, wi, sampleFlags);
    }

    f = fAccum * ShadingNormalCorrection(bsdf, woWorld, wiWorld, transportMode);
    pdf = pdfAccum;
}

void BSDF_f_and_PDF(SurfaceData surface, ShadingFrame frame, LobeSet set, float3 woWorld, float3 wiWorld, uint transportMode, uint sampleFlags, out float3 f, out float pdf)
{
    f = 0.0;
    pdf = 0.0;

    if (SelectableLobeCount(set) == 0u)
        return;

    float3 wo = FrameWorldToLocal(frame, woWorld);
    float3 wi = FrameWorldToLocal(frame, wiWorld);
    if (AbsCosTheta(wo) <= 0.0 || AbsCosTheta(wi) <= 0.0)
        return;

    bool reflect = dot(woWorld, frame.geomNormal) * dot(wiWorld, frame.geomNormal) > 0.0;

    float totalSelectionWeight = 0.0;
    for (uint weightIndex = 0u; weightIndex < 6u; weightIndex++)
    {
        if (weightIndex >= SelectableLobeCount(set))
            break;

        BxDF weightBxdf = BuildPresentLobe(surface, set, weightIndex);
        if (!BxDFMatchesFlags(weightBxdf, sampleFlags))
            continue;

        totalSelectionWeight += BxDFSelectionWeight(surface, weightBxdf, wo);
    }

    float3 fAccum = 0.0;
    float pdfAccum = 0.0;
    float sheenWeight = SheenBaseWeightSurface(surface, wo);
    for (uint evalIndex = 0u; evalIndex < 6u; evalIndex++)
    {
        if (evalIndex >= SelectableLobeCount(set))
            break;

        BxDF bxdf = BuildPresentLobe(surface, set, evalIndex);
        if (!BxDFMatchesFlags(bxdf, sampleFlags))
            continue;

        bool componentReflects = (bxdf.flags & BXDF_FLAG_REFLECTION) != 0u;
        bool componentTransmits = (bxdf.flags & BXDF_FLAG_TRANSMISSION) != 0u;
        if ((reflect && !componentReflects) || (!reflect && !componentTransmits))
            continue;

        fAccum += BxDF_f(bxdf, wo, wi, transportMode) * ApplyLayeredAttenuation(surface, bxdf, wo, wi, sheenWeight);
        if (totalSelectionWeight > 0.0)
            pdfAccum += (BxDFSelectionWeight(surface, bxdf, wo) / totalSelectionWeight) * BxDF_PDF(bxdf, wo, wi, sampleFlags);
    }

    f = fAccum * FrameShadingNormalCorrection(frame, woWorld, wiWorld, transportMode);
    pdf = pdfAccum;
}

float3 BSDF_f_Signal(BSDF bsdf, float3 woWorld, float3 wiWorld, uint transportMode, uint signalKind)
{
    if (BSDFIsEmpty(bsdf))
        return 0.0;

    OrientBSDFForView(bsdf, woWorld);
    float3 wo = BSDFWorldToLocal(bsdf, woWorld);
    float3 wi = BSDFWorldToLocal(bsdf, wiWorld);
    if (AbsCosTheta(wo) <= 0.0 || AbsCosTheta(wi) <= 0.0)
        return 0.0;

    bool reflect = dot(woWorld, bsdf.geomNormal) * dot(wiWorld, bsdf.geomNormal) > 0.0;
    float3 f = 0.0;
    float sheenWeight = SheenBaseWeight(bsdf, wo);
    for (uint evalIndex = 0u; evalIndex < 6u; evalIndex++)
    {
        if (evalIndex >= BSDFSelectableComponentCount(bsdf))
            break;

        BxDF bxdf = BSDFGetSelectableComponent(bsdf, evalIndex);
        if (!BxDFMatchesSignal(bxdf, signalKind, reflect))
            continue;

        float3 contribution = BxDF_f(bxdf, wo, wi, transportMode);
        contribution *= ApplyLayeredAttenuation(bsdf, bxdf, wo, wi, sheenWeight);
        f += contribution;
    }

    return f * ShadingNormalCorrection(bsdf, woWorld, wiWorld, transportMode);
}

void BSDF_f_AllSignals(BSDF bsdf, float3 woWorld, float3 wiWorld, uint transportMode,
    out float3 fSurface, out float3 fReflection, out float3 fTransmission)
{
    fSurface = 0.0;
    fReflection = 0.0;
    fTransmission = 0.0;

    if (BSDFIsEmpty(bsdf))
        return;

    OrientBSDFForView(bsdf, woWorld);
    float3 wo = BSDFWorldToLocal(bsdf, woWorld);
    float3 wi = BSDFWorldToLocal(bsdf, wiWorld);
    if (AbsCosTheta(wo) <= 0.0 || AbsCosTheta(wi) <= 0.0)
        return;

    bool reflect = dot(woWorld, bsdf.geomNormal) * dot(wiWorld, bsdf.geomNormal) > 0.0;
    float correction = ShadingNormalCorrection(bsdf, woWorld, wiWorld, transportMode);
    float sheenWeight = SheenBaseWeight(bsdf, wo);

    for (uint evalIndex = 0u; evalIndex < 6u; evalIndex++)
    {
        if (evalIndex >= BSDFSelectableComponentCount(bsdf))
            break;

        BxDF bxdf = BSDFGetSelectableComponent(bsdf, evalIndex);
        if (bxdf.kind == BXDF_KIND_NONE)
            continue;

        float3 contribution = BxDF_f(bxdf, wo, wi, transportMode) * correction;
        contribution *= ApplyLayeredAttenuation(bsdf, bxdf, wo, wi, sheenWeight);

        if (bxdf.kind == BXDF_KIND_DIFFUSE || bxdf.kind == BXDF_KIND_DIFFUSE_TRANSMISSION)
        {
            fSurface += contribution;
        }
        else if (reflect && (bxdf.flags & BXDF_FLAG_REFLECTION) != 0u && (bxdf.flags & (BXDF_FLAG_GLOSSY | BXDF_FLAG_SPECULAR)) != 0u)
        {
            fReflection += contribution;
        }
        else if (!reflect && BxDFHasTransmissionSignal(bxdf))
        {
            fTransmission += contribution;
        }
    }
}

void BSDF_f_AllSignals(SurfaceData surface, ShadingFrame frame, LobeSet set, float3 woWorld, float3 wiWorld, uint transportMode,
    out float3 fSurface, out float3 fReflection, out float3 fTransmission)
{
    fSurface = 0.0;
    fReflection = 0.0;
    fTransmission = 0.0;

    if (SelectableLobeCount(set) == 0u)
        return;

    float3 wo = FrameWorldToLocal(frame, woWorld);
    float3 wi = FrameWorldToLocal(frame, wiWorld);
    if (AbsCosTheta(wo) <= 0.0 || AbsCosTheta(wi) <= 0.0)
        return;

    bool reflect = dot(woWorld, frame.geomNormal) * dot(wiWorld, frame.geomNormal) > 0.0;
    float correction = FrameShadingNormalCorrection(frame, woWorld, wiWorld, transportMode);
    float sheenWeight = SheenBaseWeightSurface(surface, wo);

    [loop]
    for (uint evalIndex = 0u; evalIndex < 6u; evalIndex++)
    {
        if (evalIndex >= SelectableLobeCount(set))
            break;

        BxDF bxdf = BuildPresentLobe(surface, set, evalIndex);
        if (bxdf.kind == BXDF_KIND_NONE)
            continue;

        float3 contribution = BxDF_f(bxdf, wo, wi, transportMode) * correction;
        contribution *= ApplyLayeredAttenuation(surface, bxdf, wo, wi, sheenWeight);

        if (bxdf.kind == BXDF_KIND_DIFFUSE || bxdf.kind == BXDF_KIND_DIFFUSE_TRANSMISSION)
        {
            fSurface += contribution;
        }
        else if (reflect && (bxdf.flags & BXDF_FLAG_REFLECTION) != 0u && (bxdf.flags & (BXDF_FLAG_GLOSSY | BXDF_FLAG_SPECULAR)) != 0u)
        {
            fReflection += contribution;
        }
        else if (!reflect && BxDFHasTransmissionSignal(bxdf))
        {
            fTransmission += contribution;
        }
    }
}


bool BSDF_Sample_f(BSDF bsdf, float3 woWorld, float componentSelector, float2 u, uint transportMode, uint sampleFlags, out BSDFSample sample)
{
    sample.wi = 0.0;
    sample.f = 0.0;
    sample.pdf = 0.0;
    sample.flags = 0u;
    sample.eta = 1.0;

    if (BSDFIsEmpty(bsdf))
        return false;

    OrientBSDFForView(bsdf, woWorld);

    float3 wo = BSDFWorldToLocal(bsdf, woWorld);
    if (AbsCosTheta(wo) <= 0.0)
        return false;

    float matchedWeight0 = 0.0;
    float matchedWeight1 = 0.0;
    float matchedWeight2 = 0.0;
    float matchedWeight3 = 0.0;
    float matchedWeight4 = 0.0;
    float matchedWeight5 = 0.0;
    uint matchedIndex0 = 0u;
    uint matchedIndex1 = 0u;
    uint matchedIndex2 = 0u;
    uint matchedIndex3 = 0u;
    uint matchedIndex4 = 0u;
    uint matchedIndex5 = 0u;
    uint matchedCount = 0u;

    for (uint sampleIndex = 0u; sampleIndex < 6u; sampleIndex++)
    {
        if (sampleIndex >= BSDFSelectableComponentCount(bsdf))
            break;

        BxDF bxdf = BSDFGetSelectableComponent(bsdf, sampleIndex);
        if (!BxDFMatchesFlags(bxdf, sampleFlags))
            continue;

        float effectiveWeight = BxDFSelectionWeight(bsdf, bxdf, wo);
        if (effectiveWeight <= 0.0)
            continue;

        if (matchedCount == 0u)
        {
            matchedIndex0 = sampleIndex;
            matchedWeight0 = effectiveWeight;
        }
        else if (matchedCount == 1u)
        {
            matchedIndex1 = sampleIndex;
            matchedWeight1 = effectiveWeight;
        }
        else if (matchedCount == 2u)
        {
            matchedIndex2 = sampleIndex;
            matchedWeight2 = effectiveWeight;
        }
        else if (matchedCount == 3u)
        {
            matchedIndex3 = sampleIndex;
            matchedWeight3 = effectiveWeight;
        }
        else if (matchedCount == 4u)
        {
            matchedIndex4 = sampleIndex;
            matchedWeight4 = effectiveWeight;
        }
        else if (matchedCount == 5u)
        {
            matchedIndex5 = sampleIndex;
            matchedWeight5 = effectiveWeight;
        }

        matchedCount++;
    }

    float totalMatchedWeight = matchedWeight0 + matchedWeight1 + matchedWeight2 + matchedWeight3 + matchedWeight4 + matchedWeight5;
    if (matchedCount == 0u || totalMatchedWeight <= 0.0)
        return false;

    uint selectedIndex = matchedIndex0;
    float selectedWeight = matchedWeight0;
    float selector = saturate(componentSelector) * totalMatchedWeight;
    float remappedSelector = 0.0;
    if (matchedCount > 5u && selector >= matchedWeight0 + matchedWeight1 + matchedWeight2 + matchedWeight3 + matchedWeight4)
    {
        selectedIndex = matchedIndex5;
        selectedWeight = matchedWeight5;
        remappedSelector = (selector - matchedWeight0 - matchedWeight1 - matchedWeight2 - matchedWeight3 - matchedWeight4) / max(selectedWeight, 1e-6);
    }
    else if (matchedCount > 4u && selector >= matchedWeight0 + matchedWeight1 + matchedWeight2 + matchedWeight3)
    {
        selectedIndex = matchedIndex4;
        selectedWeight = matchedWeight4;
        remappedSelector = (selector - matchedWeight0 - matchedWeight1 - matchedWeight2 - matchedWeight3) / max(selectedWeight, 1e-6);
    }
    else if (matchedCount > 3u && selector >= matchedWeight0 + matchedWeight1 + matchedWeight2)
    {
        selectedIndex = matchedIndex3;
        selectedWeight = matchedWeight3;
        remappedSelector = (selector - matchedWeight0 - matchedWeight1 - matchedWeight2) / max(selectedWeight, 1e-6);
    }
    else if (matchedCount > 2u && selector >= matchedWeight0 + matchedWeight1)
    {
        selectedIndex = matchedIndex2;
        selectedWeight = matchedWeight2;
        remappedSelector = (selector - matchedWeight0 - matchedWeight1) / max(selectedWeight, 1e-6);
    }
    else if (matchedCount > 1u && selector >= matchedWeight0)
    {
        selectedIndex = matchedIndex1;
        selectedWeight = matchedWeight1;
        remappedSelector = (selector - matchedWeight0) / max(selectedWeight, 1e-6);
    }
    else
    {
        remappedSelector = selector / max(selectedWeight, 1e-6);
    }

    BxDF selected = BSDFGetSelectableComponent(bsdf, selectedIndex);
    BSDFSample localSample;
    if (!BxDF_Sample(selected, wo, saturate(remappedSelector), u, transportMode, sampleFlags, localSample))
        return false;

    float3 wiWorld = BSDFLocalToWorld(bsdf, localSample.wi);
    if (dot(wiWorld, wiWorld) <= 1e-20)
        return false;

    sample = localSample;
    sample.wi = wiWorld;
    sample.pdf *= selectedWeight / max(totalMatchedWeight, 1e-6);

    if ((sample.flags & BXDF_FLAG_SPECULAR) == 0u && matchedCount > 1u)
    {
        BSDF_f_and_PDF(bsdf, woWorld, wiWorld, transportMode, sampleFlags, sample.f, sample.pdf);
    }
    else
    {
        float sheenWeightDelta = SheenBaseWeight(bsdf, wo);
        sample.f *= ApplyLayeredAttenuation(bsdf, selected, wo, localSample.wi, sheenWeightDelta);
        sample.f *= ShadingNormalCorrection(bsdf, woWorld, wiWorld, transportMode);
    }

    return sample.pdf > 1e-6 && Max3(sample.f) > 1e-6;
}

bool BSDF_Sample_f(SurfaceData surface, ShadingFrame frame, LobeSet set, float3 woWorld, float componentSelector, float2 u, uint transportMode, uint sampleFlags, out BSDFSample sample)
{
    sample.wi = 0.0;
    sample.f = 0.0;
    sample.pdf = 0.0;
    sample.flags = 0u;
    sample.eta = 1.0;

    if (SelectableLobeCount(set) == 0u)
        return false;

    float3 wo = FrameWorldToLocal(frame, woWorld);
    if (AbsCosTheta(wo) <= 0.0)
        return false;

    float matchedWeight0 = 0.0;
    float matchedWeight1 = 0.0;
    float matchedWeight2 = 0.0;
    float matchedWeight3 = 0.0;
    float matchedWeight4 = 0.0;
    float matchedWeight5 = 0.0;
    uint matchedIndex0 = 0u;
    uint matchedIndex1 = 0u;
    uint matchedIndex2 = 0u;
    uint matchedIndex3 = 0u;
    uint matchedIndex4 = 0u;
    uint matchedIndex5 = 0u;
    uint matchedCount = 0u;

    for (uint sampleIndex = 0u; sampleIndex < 6u; sampleIndex++)
    {
        if (sampleIndex >= SelectableLobeCount(set))
            break;

        BxDF bxdf = BuildPresentLobe(surface, set, sampleIndex);
        if (!BxDFMatchesFlags(bxdf, sampleFlags))
            continue;

        float effectiveWeight = BxDFSelectionWeight(surface, bxdf, wo);
        if (effectiveWeight <= 0.0)
            continue;

        if (matchedCount == 0u)
        {
            matchedIndex0 = sampleIndex;
            matchedWeight0 = effectiveWeight;
        }
        else if (matchedCount == 1u)
        {
            matchedIndex1 = sampleIndex;
            matchedWeight1 = effectiveWeight;
        }
        else if (matchedCount == 2u)
        {
            matchedIndex2 = sampleIndex;
            matchedWeight2 = effectiveWeight;
        }
        else if (matchedCount == 3u)
        {
            matchedIndex3 = sampleIndex;
            matchedWeight3 = effectiveWeight;
        }
        else if (matchedCount == 4u)
        {
            matchedIndex4 = sampleIndex;
            matchedWeight4 = effectiveWeight;
        }
        else if (matchedCount == 5u)
        {
            matchedIndex5 = sampleIndex;
            matchedWeight5 = effectiveWeight;
        }

        matchedCount++;
    }

    float totalMatchedWeight = matchedWeight0 + matchedWeight1 + matchedWeight2 + matchedWeight3 + matchedWeight4 + matchedWeight5;
    if (matchedCount == 0u || totalMatchedWeight <= 0.0)
        return false;

    uint selectedIndex = matchedIndex0;
    float selectedWeight = matchedWeight0;
    float selector = saturate(componentSelector) * totalMatchedWeight;
    float remappedSelector = 0.0;
    if (matchedCount > 5u && selector >= matchedWeight0 + matchedWeight1 + matchedWeight2 + matchedWeight3 + matchedWeight4)
    {
        selectedIndex = matchedIndex5;
        selectedWeight = matchedWeight5;
        remappedSelector = (selector - matchedWeight0 - matchedWeight1 - matchedWeight2 - matchedWeight3 - matchedWeight4) / max(selectedWeight, 1e-6);
    }
    else if (matchedCount > 4u && selector >= matchedWeight0 + matchedWeight1 + matchedWeight2 + matchedWeight3)
    {
        selectedIndex = matchedIndex4;
        selectedWeight = matchedWeight4;
        remappedSelector = (selector - matchedWeight0 - matchedWeight1 - matchedWeight2 - matchedWeight3) / max(selectedWeight, 1e-6);
    }
    else if (matchedCount > 3u && selector >= matchedWeight0 + matchedWeight1 + matchedWeight2)
    {
        selectedIndex = matchedIndex3;
        selectedWeight = matchedWeight3;
        remappedSelector = (selector - matchedWeight0 - matchedWeight1 - matchedWeight2) / max(selectedWeight, 1e-6);
    }
    else if (matchedCount > 2u && selector >= matchedWeight0 + matchedWeight1)
    {
        selectedIndex = matchedIndex2;
        selectedWeight = matchedWeight2;
        remappedSelector = (selector - matchedWeight0 - matchedWeight1) / max(selectedWeight, 1e-6);
    }
    else if (matchedCount > 1u && selector >= matchedWeight0)
    {
        selectedIndex = matchedIndex1;
        selectedWeight = matchedWeight1;
        remappedSelector = (selector - matchedWeight0) / max(selectedWeight, 1e-6);
    }
    else
    {
        remappedSelector = selector / max(selectedWeight, 1e-6);
    }

    BxDF selected = BuildPresentLobe(surface, set, selectedIndex);
    BSDFSample localSample;
    if (!BxDF_Sample(selected, wo, saturate(remappedSelector), u, transportMode, sampleFlags, localSample))
        return false;

    float3 wiWorld = FrameLocalToWorld(frame, localSample.wi);
    if (dot(wiWorld, wiWorld) <= 1e-20)
        return false;

    sample = localSample;
    sample.wi = wiWorld;
    sample.pdf *= selectedWeight / max(totalMatchedWeight, 1e-6);

    if ((sample.flags & BXDF_FLAG_SPECULAR) == 0u && matchedCount > 1u)
    {
        BSDF_f_and_PDF(surface, frame, set, woWorld, wiWorld, transportMode, sampleFlags, sample.f, sample.pdf);
    }
    else
    {
        float sheenWeightDelta = SheenBaseWeightSurface(surface, wo);
        sample.f *= ApplyLayeredAttenuation(surface, selected, wo, localSample.wi, sheenWeightDelta);
        sample.f *= FrameShadingNormalCorrection(frame, woWorld, wiWorld, transportMode);
    }

    return sample.pdf > 1e-6 && Max3(sample.f) > 1e-6;
}

#endif
