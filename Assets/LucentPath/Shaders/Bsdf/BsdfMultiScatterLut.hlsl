#ifndef LUCENT_PATH_BSDF_MULTISCATTER_LUT_INCLUDED
#define LUCENT_PATH_BSDF_MULTISCATTER_LUT_INCLUDED

#ifndef LUCENT_PATH_MS_LUT_DECLARED
#define LUCENT_PATH_MS_LUT_DECLARED
Texture2D<float> _MsLutDirectional;
SamplerState sampler_MsLutDirectional;
StructuredBuffer<float> _MsLutAverage;
Texture2D<float> _MsLutSheenDirectional;
SamplerState sampler_MsLutSheenDirectional;
int _MsLutResolution;
int _MsLutReady;
int _MsLutSheenReady;
#endif

float MsSampleDirectionalAlbedo(float cosTheta, float roughness)
{
    if (_MsLutReady == 0 || _MsLutResolution <= 0)
        return 1.0;
    float u = saturate(cosTheta);
    float v = saturate(roughness);
    return saturate(_MsLutDirectional.SampleLevel(sampler_MsLutDirectional, float2(u, v), 0));
}

float MsSampleAverageAlbedo(float roughness)
{
    if (_MsLutReady == 0 || _MsLutResolution <= 0)
        return 1.0;
    float v = saturate(roughness) * (float)(_MsLutResolution - 1);
    int lo = (int)floor(v);
    int hi = min(lo + 1, _MsLutResolution - 1);
    float t = v - (float)lo;
    float e0 = _MsLutAverage[lo];
    float e1 = _MsLutAverage[hi];
    return saturate(lerp(e0, e1, t));
}

#define LUCENT_MS_MIN_ROUGHNESS 0.045

float MsNonAbsorbingCompensationWithAverage(float cosThetaO, float cosThetaI, float roughness, float E_avg)
{
    float E_o = MsSampleDirectionalAlbedo(cosThetaO, roughness);
    float E_i = MsSampleDirectionalAlbedo(cosThetaI, roughness);
    float oneMinusEAvg = max(1.0 - E_avg, 1e-3);
    return (1.0 - E_o) * (1.0 - E_i) / (3.14159265 * oneMinusEAvg);
}

float MsNonAbsorbingCompensation(float cosThetaO, float cosThetaI, float roughness)
{
    if (_MsLutReady == 0 || roughness < LUCENT_MS_MIN_ROUGHNESS)
        return 0.0;

    float E_avg = MsSampleAverageAlbedo(roughness);
    return MsNonAbsorbingCompensationWithAverage(cosThetaO, cosThetaI, roughness, E_avg);
}

float3 MsMultiScatterCompensation(float3 fSingle, float3 F0, float cosThetaO, float cosThetaI, float roughness)
{
    if (_MsLutReady == 0 || roughness < LUCENT_MS_MIN_ROUGHNESS)
        return fSingle;

    float E_avg = MsSampleAverageAlbedo(roughness);
    float msAttenuation = MsNonAbsorbingCompensationWithAverage(cosThetaO, cosThetaI, roughness, E_avg);
    float3 F_avg = F0 + (1.0 - F0) * (1.0 / 21.0);
    float3 F_ms = F_avg * F_avg * E_avg / (1.0 - F_avg * (1.0 - E_avg));

    return fSingle + msAttenuation * F_ms;
}

float MsSampleSheenDirectionalAlbedo(float cosTheta, float roughness)
{
    if (_MsLutSheenReady == 0 || _MsLutResolution <= 0)
        return 0.0;
    float u = saturate(cosTheta);
    float v = saturate(roughness);
    return saturate(_MsLutSheenDirectional.SampleLevel(sampler_MsLutSheenDirectional, float2(u, v), 0));
}

#endif
