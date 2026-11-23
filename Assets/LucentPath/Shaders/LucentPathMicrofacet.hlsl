#ifndef LUCENT_PATH_MICROFACET_INCLUDED
#define LUCENT_PATH_MICROFACET_INCLUDED

struct TrowbridgeReitzDistribution
{
    float alphaX;
    float alphaY;
};

float CosTheta(float3 w)
{
    return w.z;
}

float AbsCosTheta(float3 w)
{
    return abs(w.z);
}

float Sin2Theta(float3 w)
{
    return max(0.0, 1.0 - w.z * w.z);
}

float Tan2Theta(float3 w)
{
    float cosThetaSq = w.z * w.z;
    if (cosThetaSq <= 1e-20)
        return 1e30;

    return Sin2Theta(w) / cosThetaSq;
}

float Cos2Phi(float3 w)
{
    float sinThetaSq = Sin2Theta(w);
    if (sinThetaSq <= 1e-20)
        return 1.0;

    return saturate((w.x * w.x) / sinThetaSq);
}

float Sin2Phi(float3 w)
{
    float sinThetaSq = Sin2Theta(w);
    if (sinThetaSq <= 1e-20)
        return 0.0;

    return saturate((w.y * w.y) / sinThetaSq);
}

bool SameHemisphere(float3 w, float3 wp)
{
    return w.z * wp.z > 0.0;
}

bool TrowbridgeReitz_EffectivelySmooth(TrowbridgeReitzDistribution distribution)
{
    return max(distribution.alphaX, distribution.alphaY) <= 1e-3;
}

float TrowbridgeReitz_D(TrowbridgeReitzDistribution distribution, float3 wm)
{
    float tan2Theta = Tan2Theta(wm);
    if (tan2Theta >= 1e29)
        return 0.0;

    float cosTheta4 = CosTheta(wm);
    cosTheta4 *= cosTheta4;
    cosTheta4 *= cosTheta4;
    if (cosTheta4 <= 1e-20)
        return 0.0;

    float e = tan2Theta * (Cos2Phi(wm) / max(distribution.alphaX * distribution.alphaX, 1e-12) +
        Sin2Phi(wm) / max(distribution.alphaY * distribution.alphaY, 1e-12));
    float denom = PI * max(distribution.alphaX * distribution.alphaY, 1e-12) * cosTheta4 * (1.0 + e) * (1.0 + e);
    return rcp(max(denom, 1e-20));
}

float TrowbridgeReitz_Lambda(TrowbridgeReitzDistribution distribution, float3 w)
{
    float tan2Theta = Tan2Theta(w);
    if (tan2Theta >= 1e29)
        return 0.0;

    float alpha = sqrt(Cos2Phi(w) * distribution.alphaX * distribution.alphaX + Sin2Phi(w) * distribution.alphaY * distribution.alphaY);
    float alpha2Tan2Theta = alpha * alpha * tan2Theta;
    return 0.5 * (sqrt(1.0 + alpha2Tan2Theta) - 1.0);
}

float TrowbridgeReitz_G1(TrowbridgeReitzDistribution distribution, float3 w)
{
    return rcp(1.0 + TrowbridgeReitz_Lambda(distribution, w));
}

float TrowbridgeReitz_G(TrowbridgeReitzDistribution distribution, float3 wo, float3 wi)
{
    return rcp(1.0 + TrowbridgeReitz_Lambda(distribution, wo) + TrowbridgeReitz_Lambda(distribution, wi));
}

float3 TrowbridgeReitz_Sample_wm(TrowbridgeReitzDistribution distribution, float3 wo, float2 u)
{
    bool flip = wo.z < 0.0;
    float3 w = flip ? -wo : wo;

    float3 stretched = SafeNormalize(float3(distribution.alphaX * w.x, distribution.alphaY * w.y, w.z), float3(0.0, 0.0, 1.0));
    float lensq = stretched.x * stretched.x + stretched.y * stretched.y;
    float3 t1 = lensq > 1e-20 ? float3(-stretched.y, stretched.x, 0.0) * rsqrt(lensq) : float3(1.0, 0.0, 0.0);
    float3 t2 = cross(stretched, t1);

    float2 disk = SampleUniformDiskConcentric(u);
    float s = 0.5 * (1.0 + stretched.z);
    disk.y = lerp(sqrt(saturate(1.0 - disk.x * disk.x)), disk.y, s);

    float z = sqrt(saturate(1.0 - dot(disk, disk)));
    float3 nh = disk.x * t1 + disk.y * t2 + z * stretched;
    float3 wm = SafeNormalize(float3(distribution.alphaX * nh.x, distribution.alphaY * nh.y, max(nh.z, 1e-6)), float3(0.0, 0.0, 1.0));
    return flip ? -wm : wm;
}

float TrowbridgeReitz_PDF(TrowbridgeReitzDistribution distribution, float3 wo, float3 wm)
{
    float absCosWo = AbsCosTheta(wo);
    if (absCosWo <= 0.0)
        return 0.0;

    return TrowbridgeReitz_D(distribution, wm) * TrowbridgeReitz_G1(distribution, wo) * abs(dot(wo, wm)) / absCosWo;
}

#endif
