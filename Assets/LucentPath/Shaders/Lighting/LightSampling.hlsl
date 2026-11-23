#ifndef LUCENT_PATH_LIGHT_SAMPLING_INCLUDED
#define LUCENT_PATH_LIGHT_SAMPLING_INCLUDED

#include "LightTypes.hlsl"
#include "AnalyticLightDistribution.hlsl"
#include "LightCategoryCdf.hlsl"
#include "EnvImportanceSampling.hlsl"
#include "EnvSampler.hlsl"
#include "LightingShadow.hlsl"

LightSample BuildEnvLightSample(float3 direction, float solidAnglePdf, float3 radiance)
{
    LightSample sample = MakeEmptyLightSample();
    sample.wi = SafeNormalize(direction, float3(0.0, 1.0, 0.0));
    sample.radiance = max(radiance, 0.0);
    sample.samplePosition = float3(0.0, 0.0, 0.0);
    sample.lightNormal = -sample.wi;
    sample.distance = 1e30;
    sample.areaPdf = 0.0;
    sample.solidAnglePdf = max(solidAnglePdf, 0.0);
    sample.flags = LIGHT_SAMPLE_FLAG_INFINITE;
    if (Max3(sample.radiance) > 1e-8 && sample.solidAnglePdf > 0.0)
        sample.flags |= LIGHT_SAMPLE_FLAG_VALID;
    return sample;
}

LightSample BuildAnalyticLightSample(GpuLight light, SurfaceData surface, float2 areaUv)
{
    LightSample sample = MakeEmptyLightSample();
    float3 wi;
    float maxDistance;
    float3 radiance;
    float areaPdf;
    bool isDelta;
    if (!GetDirectLightSample(light, surface, areaUv, wi, maxDistance, radiance, areaPdf, isDelta))
        return sample;

    sample.wi = wi;
    sample.radiance = radiance;
    sample.distance = max(maxDistance, EPSILON);
    sample.flags = LIGHT_SAMPLE_FLAG_VALID;
    if (isDelta)
    {
        sample.flags |= LIGHT_SAMPLE_FLAG_DELTA;
        sample.solidAnglePdf = 1.0;
        sample.areaPdf = 0.0;
        sample.samplePosition = surface.position + wi * sample.distance;
        sample.lightNormal = -wi;
    }
    else
    {
        sample.areaPdf = areaPdf;
        sample.solidAnglePdf = areaPdf;
        sample.samplePosition = surface.position + wi * sample.distance;
        sample.lightNormal = SafeNormalize(light.directionType.xyz, -wi);
    }
    return sample;
}

bool SampleLight(SurfaceData surface, float uCategory, float uPick, float2 uSample,
    out LightSelector selector, out LightSample sample)
{
    selector = MakeEmptyLightSelector();
    sample = MakeEmptyLightSample();

    float categoryPdf;
    float uCategoryRemapped = uCategory;
    uint category = SampleLightCategory(uCategoryRemapped, categoryPdf);
    if (category == LIGHT_CATEGORY_NONE || categoryPdf <= 0.0)
        return false;

    if (category == LIGHT_CATEGORY_ENV)
    {
        float3 direction;
        float3 radiance;
        float envPdf;
        if (!SampleEnvironmentImportance(uPick, uSample, direction, radiance, envPdf))
            return false;

        selector.category = LIGHT_CATEGORY_ENV;
        selector.lightIndex = 0u;
        selector.envDirection = direction;
        selector.areaUv = float2(0.0, 0.0);
        sample = BuildEnvLightSample(direction, envPdf * categoryPdf, radiance);
        return LightSampleIsValid(sample);
    }

    float pickPdf;
    float uPickRemapped = uPick;
    uint lightIndex = SampleAnalyticLightIndex(surface.position, uPickRemapped, pickPdf);
    if (pickPdf <= 0.0)
        return false;

    GpuLight light = _Lights[lightIndex];
    LightSample analyticSample = BuildAnalyticLightSample(light, surface, uSample);
    if (!LightSampleIsValid(analyticSample))
        return false;

    selector.category = LIGHT_CATEGORY_ANALYTIC;
    selector.lightIndex = lightIndex;
    selector.envDirection = float3(0.0, 0.0, 0.0);
    selector.areaUv = uSample;

    float combinedPdf = categoryPdf * pickPdf;
    analyticSample.solidAnglePdf *= combinedPdf;
    if ((analyticSample.flags & LIGHT_SAMPLE_FLAG_DELTA) == 0u)
        analyticSample.areaPdf *= combinedPdf;
    sample = analyticSample;
    return true;
}

bool ReconstructLightSample(LightSelector selector, SurfaceData surface, out LightSample sample)
{
    sample = MakeEmptyLightSample();
    if (selector.category == LIGHT_CATEGORY_NONE)
        return false;

    if (selector.category == LIGHT_CATEGORY_ENV)
    {
        if (_EnvSamplingReady == 0)
            return false;

        float3 direction = SafeNormalize(selector.envDirection, float3(0.0, 1.0, 0.0));
        float3 radiance = EvaluateEnvironmentRadiance(direction);
        float envPdf = EvaluateEnvironmentPdf(direction);
        float categoryPdf = GetLightCategoryEnvPdf();
        sample = BuildEnvLightSample(direction, envPdf * categoryPdf, radiance);
        return LightSampleIsValid(sample);
    }

    if (selector.lightIndex >= (uint)_LightCount)
        return false;

    GpuLight light = _Lights[selector.lightIndex];
    LightSample analyticSample = BuildAnalyticLightSample(light, surface, selector.areaUv);
    if (!LightSampleIsValid(analyticSample))
        return false;

    float categoryPdf = GetLightCategoryAnalyticPdf();
    float pickPdf = GetAnalyticLightPickPdf(surface.position, selector.lightIndex);
    float combinedPdf = categoryPdf * pickPdf;
    analyticSample.solidAnglePdf *= combinedPdf;
    if ((analyticSample.flags & LIGHT_SAMPLE_FLAG_DELTA) == 0u)
        analyticSample.areaPdf *= combinedPdf;
    sample = analyticSample;
    return true;
}

float EvaluateLightSelectorPdf(LightSelector selector, SurfaceData surface)
{
    LightSample sample;
    if (!ReconstructLightSample(selector, surface, sample))
        return 0.0;
    return sample.solidAnglePdf;
}

#endif
