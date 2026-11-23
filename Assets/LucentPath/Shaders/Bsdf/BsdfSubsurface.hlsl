#ifndef LUCENT_PATH_BSDF_SUBSURFACE_INCLUDED
#define LUCENT_PATH_BSDF_SUBSURFACE_INCLUDED

#define SSS_PI 3.14159265358979323846

struct SubsurfaceMedium
{
    float3 albedo;
    float3 sigmaT;
    float3 sigmaS;
    float anisotropy;
};

#define SSS_THIN_OBJECT_MFP_MULTIPLIER 4.0
#define SSS_MAX_BRANCH_COMPENSATION 4.0

float ChiangAlbedoFromColor(float color)
{
    float c = saturate(color);
    float t = 1.0 - exp(-5.09406 * c + 2.61188 * c * c - 4.31805 * c * c * c);
    return saturate(t);
}

float3 ChiangAlbedoFromColor(float3 color)
{
    return float3(
        ChiangAlbedoFromColor(color.x),
        ChiangAlbedoFromColor(color.y),
        ChiangAlbedoFromColor(color.z));
}

float ChiangSigmaTFromRadius(float color, float radius)
{
    float colorTerm = saturate(color) - 0.33;
    float colorTerm4 = colorTerm * colorTerm * colorTerm * colorTerm;
    float scatteringMfp = 3.5 + 100.0 * colorTerm4;
    return 1.0 / max(radius * scatteringMfp, 1e-6);
}

float3 ChiangSigmaTFromRadius(float3 color, float3 radius)
{
    return float3(
        ChiangSigmaTFromRadius(color.x, radius.x),
        ChiangSigmaTFromRadius(color.y, radius.y),
        ChiangSigmaTFromRadius(color.z, radius.z));
}

SubsurfaceMedium BuildSubsurfaceMedium(float3 color, float3 radius, float scale, float anisotropy)
{
    SubsurfaceMedium m;
    float3 effectiveRadius = max(radius * max(scale, 1e-6), float3(1e-6, 1e-6, 1e-6));
    m.albedo = ChiangAlbedoFromColor(saturate(color));
    m.sigmaT = ChiangSigmaTFromRadius(saturate(color), effectiveRadius);
    m.sigmaS = m.albedo * m.sigmaT;
    m.anisotropy = clamp(anisotropy, -0.99, 0.99);
    return m;
}

float HenyeyGreensteinPhase(float cosTheta, float g)
{
    float denom = 1.0 + g * g - 2.0 * g * cosTheta;
    return (1.0 - g * g) / max(4.0 * SSS_PI * denom * sqrt(max(denom, 1e-6)), 1e-6);
}

float3 SampleHenyeyGreensteinDirection(float3 wo, float g, float2 u, out float pdf)
{
    float cosTheta;
    if (abs(g) > 1e-3)
    {
        float term = (1.0 - g * g) / max(1.0 - g + 2.0 * g * u.x, 1e-6);
        cosTheta = (1.0 + g * g - term * term) / max(2.0 * g, 1e-6);
        cosTheta = clamp(cosTheta, -1.0, 1.0);
    }
    else
    {
        cosTheta = 1.0 - 2.0 * u.x;
    }

    float sinTheta = sqrt(max(1.0 - cosTheta * cosTheta, 0.0));
    float phi = 2.0 * SSS_PI * u.y;

    float3 tangent;
    float3 bitangent;
    BuildBasis(wo, tangent, bitangent);

    float3 wi = tangent * (sinTheta * cos(phi)) + bitangent * (sinTheta * sin(phi)) + wo * cosTheta;
    pdf = HenyeyGreensteinPhase(cosTheta, g);
    return SafeNormalize(wi, wo);
}

float SampleExponentialDistance(float u, float sigmaT)
{
    return -log(max(1.0 - u, 1e-6)) / max(sigmaT, 1e-6);
}

float ExponentialPdf(float distance, float sigmaT)
{
    return sigmaT * exp(-sigmaT * max(distance, 0.0));
}

float3 BeerLambertTransmittance(float3 sigmaT, float distance)
{
    return exp(-sigmaT * max(distance, 0.0));
}

float SubsurfaceLuminance(float3 c)
{
    return dot(c, float3(0.2126, 0.7152, 0.0722));
}

float3 SubsurfaceAbsorptionSigma(SubsurfaceMedium medium)
{
    return max(medium.sigmaT - medium.sigmaS, float3(0.0, 0.0, 0.0));
}

float SubsurfaceMeanFreePath(SubsurfaceMedium medium)
{
    return rcp(max(SubsurfaceLuminance(medium.sigmaT), 1e-6));
}

float ThinSubsurfaceDistanceLimit(SubsurfaceMedium medium)
{
    return SubsurfaceMeanFreePath(medium) * SSS_THIN_OBJECT_MFP_MULTIPLIER;
}

float3 EvaluateThinSubsurfaceTransmission(SubsurfaceMedium medium, float thickness)
{
    float3 sigmaA = SubsurfaceAbsorptionSigma(medium);
    float3 sigmaSPrime = medium.sigmaS * max(1.0 - medium.anisotropy, 1e-3);
    float3 sigmaTPrime = max(sigmaA + sigmaSPrime, float3(1e-6, 1e-6, 1e-6));
    float3 sigmaTr = sqrt(max(3.0 * sigmaA * sigmaTPrime, float3(0.0, 0.0, 0.0)));
    float3 absorption = BeerLambertTransmittance(sigmaA, thickness);
    float3 diffusion = BeerLambertTransmittance(sigmaTr, thickness);
    return saturate(medium.albedo * absorption * diffusion);
}

float BurleyScalingDmfp(float color)
{
    float t = saturate(color) - 0.33;
    float t2 = t * t;
    return 3.5 + 100.0 * t2 * t2;
}

float BurleyScatterDistance(float color, float effectiveRadius)
{
    return max(effectiveRadius, 1e-6) / BurleyScalingDmfp(color);
}

float BurleyRadialPdf(float radius, float d)
{
    float invD = 1.0 / max(d, 1e-6);
    return (exp(-radius * invD) + exp(-radius * invD * (1.0 / 3.0))) * (invD * 0.25);
}

float BurleySampleRadius(float xi, float d)
{
    float u = max(1.0 - xi, 1e-4);
    float g = 1.0 + 4.0 * u * (2.0 * u + sqrt(1.0 + 4.0 * u * u));
    float gCubeRoot = pow(g, 1.0 / 3.0);
    return 3.0 * max(d, 1e-6) * log((1.0 + 1.0 / gCubeRoot + gCubeRoot) / (4.0 * u));
}

#endif
