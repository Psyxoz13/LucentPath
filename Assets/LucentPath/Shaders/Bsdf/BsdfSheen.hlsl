#ifndef LUCENT_PATH_BSDF_SHEEN_INCLUDED
#define LUCENT_PATH_BSDF_SHEEN_INCLUDED

#define SHEEN_PI 3.14159265358979323846

float SheenCharlieD(float NoH, float r)
{
    float invR = 1.0 / max(r, 1e-3);
    float cos2h = NoH * NoH;
    float sin2h = max(1.0 - cos2h, 0.0078125);
    return (2.0 + invR) * pow(sin2h, invR * 0.5) / (2.0 * SHEEN_PI);
}

float SheenCharlieLTerm(float x, float r)
{
    float oneMinusR = 1.0 - r;
    float oneMinusR2 = oneMinusR * oneMinusR;
    float a = lerp(25.3245, 21.5473, oneMinusR2);
    float b = lerp(3.32435, 3.82987, oneMinusR2);
    float c = lerp(0.16801, 0.19823, oneMinusR2);
    float d = lerp(-1.27393, -1.97760, oneMinusR2);
    float e = lerp(-4.85967, -4.32054, oneMinusR2);
    return a / (1.0 + b * pow(max(x, 0.0), c)) + d * x + e;
}

float SheenCharlieLambda(float cosTheta, float r)
{
    if (cosTheta < 0.5)
        return exp(SheenCharlieLTerm(cosTheta, r));
    return exp(2.0 * SheenCharlieLTerm(0.5, r) - SheenCharlieLTerm(1.0 - cosTheta, r));
}

float SheenCharlieG(float NoV, float NoL, float r)
{
    return 1.0 / (1.0 + SheenCharlieLambda(NoV, r) + SheenCharlieLambda(NoL, r));
}

float SheenDirectionalAlbedo(BxDF bxdf, float cosTheta)
{
    float r = clamp(bxdf.alphaX, 0.07, 1.0);
    return MsSampleSheenDirectionalAlbedo(saturate(cosTheta), r);
}

float SheenBaseWeight(BSDF bsdf, float3 wo)
{
    if (bsdf.hasSheen == 0u || bsdf.sheen.kind != BXDF_KIND_SHEEN)
        return 1.0;

    float sheenAlbedo = SheenDirectionalAlbedo(bsdf.sheen, AbsCosTheta(wo));
    float sheenStrength = saturate(Max3(bsdf.sheen.scale));
    return saturate(1.0 - sheenStrength * sheenAlbedo);
}

float SheenSelectionWeight(BxDF bxdf, float3 wo)
{
    float sheenAlbedo = SheenDirectionalAlbedo(bxdf, AbsCosTheta(wo));
    float strength = saturate(Max3(bxdf.scale));
    return strength * max(sheenAlbedo, 1e-4);
}

float3 Sheen_f(BxDF bxdf, float3 wo, float3 wi)
{
    if (!SameHemisphere(wo, wi) || AbsCosTheta(wo) <= 0.0 || AbsCosTheta(wi) <= 0.0)
        return 0.0;

    float3 wm = SafeNormalize(wo + wi, float3(0.0, 0.0, 1.0));
    if (wm.z < 0.0)
        wm = -wm;

    float r = clamp(bxdf.alphaX, 0.07, 1.0);
    float NoV = AbsCosTheta(wo);
    float NoL = AbsCosTheta(wi);
    float NoH = saturate(wm.z);

    float D = SheenCharlieD(NoH, r);
    float G = SheenCharlieG(NoV, NoL, r);
    return bxdf.scale * (D * G / max(4.0 * NoV * NoL, 1e-6));
}

float Sheen_PDF(BxDF bxdf, float3 wo, float3 wi)
{
    if (!SameHemisphere(wo, wi) || AbsCosTheta(wo) <= 0.0 || AbsCosTheta(wi) <= 0.0)
        return 0.0;
    return AbsCosTheta(wi) / SHEEN_PI;
}

bool Sheen_Sample(BxDF bxdf, float3 wo, float2 u, out BSDFSample sample)
{
    float cosThetaI = sqrt(max(u.x, 0.0));
    float sinThetaI = sqrt(max(1.0 - cosThetaI * cosThetaI, 0.0));
    float phi = 2.0 * SHEEN_PI * u.y;
    float3 wi = float3(sinThetaI * cos(phi), sinThetaI * sin(phi), cosThetaI);
    if (wo.z < 0.0)
        wi.z = -wi.z;

    sample.wi = wi;
    sample.f = Sheen_f(bxdf, wo, wi);
    sample.pdf = Sheen_PDF(bxdf, wo, wi);
    sample.flags = BXDF_FLAG_REFLECTION | BXDF_FLAG_GLOSSY;
    sample.eta = 1.0;
    return sample.pdf > 1e-6 && Max3(sample.f) > 1e-6;
}

#endif
