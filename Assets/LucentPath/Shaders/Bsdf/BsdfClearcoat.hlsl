#ifndef LUCENT_PATH_BSDF_CLEARCOAT_INCLUDED
#define LUCENT_PATH_BSDF_CLEARCOAT_INCLUDED

float ClearcoatFresnel(BxDF bxdf, float cosTheta)
{
    float eta = max(bxdf.eta, 1.0);
    float f0Term = (eta - 1.0) / max(eta + 1.0, 1e-6);
    float f0 = f0Term * f0Term;
    return f0 + (1.0 - f0) * Pow5(1.0 - saturate(abs(cosTheta)));
}

float ClearcoatBaseWeight(BSDF bsdf, float3 wo)
{
    if (bsdf.hasCoat == 0u || bsdf.coat.kind != BXDF_KIND_CLEARCOAT)
        return 1.0;

    float coatWeight = saturate(Max3(bsdf.coat.scale));
    float fresnel = ClearcoatFresnel(bsdf.coat, AbsCosTheta(wo));
    float coatTransmission = saturate(1.0 - coatWeight * fresnel * saturate(bsdf.coat.transmissionWeight));
    float3 coatTint = lerp(float3(1.0, 1.0, 1.0), ResolveClearcoatSubstrateTint(bsdf.coat), coatWeight);
    return saturate(coatTransmission * ComputeLuminance(coatTint));
}

float3 ClearcoatBaseDirectionalAttenuation(BSDF bsdf, float3 wo, float3 wi)
{
    if (bsdf.hasCoat == 0u || bsdf.coat.kind != BXDF_KIND_CLEARCOAT)
        return float3(1.0, 1.0, 1.0);

    float coatWeight = saturate(Max3(bsdf.coat.scale));
    float darkening = saturate(bsdf.coat.transmissionWeight);
    float woTransmittance = 1.0 - darkening * ClearcoatFresnel(bsdf.coat, AbsCosTheta(wo));
    float wiTransmittance = 1.0 - darkening * ClearcoatFresnel(bsdf.coat, AbsCosTheta(wi));
    float coatTransmission = saturate(woTransmittance * wiTransmittance);
    float3 coatTint = ResolveClearcoatSubstrateTint(bsdf.coat);
    return lerp(float3(1.0, 1.0, 1.0), coatTransmission.xxx * coatTint, coatWeight);
}

float3 Clearcoat_f(BxDF bxdf, float3 wo, float3 wi)
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
    float F = ClearcoatFresnel(bxdf, woDotH);
    float3 thinFilm = ThinFilmRgbInterference(woDotH, bxdf.thinFilmWeight, bxdf.thinFilmThickness, bxdf.thinFilmIor);
    return bxdf.scale * thinFilm * (F * D * G / max(4.0 * AbsCosTheta(wo) * AbsCosTheta(wi), 1e-6));
}

float Clearcoat_PDF(BxDF bxdf, float3 wo, float3 wi)
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

bool Clearcoat_Sample(BxDF bxdf, float3 wo, float2 u, out BSDFSample sample)
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

    sample.f = Clearcoat_f(bxdf, wo, sample.wi);
    sample.pdf = Clearcoat_PDF(bxdf, wo, sample.wi);
    sample.flags = BXDF_FLAG_REFLECTION | ((bxdf.flags & BXDF_FLAG_SPECULAR) != 0u ? BXDF_FLAG_SPECULAR : BXDF_FLAG_GLOSSY);
    sample.eta = 1.0;
    return sample.pdf > 1e-6 && Max3(sample.f) > 1e-6;
}

#endif
