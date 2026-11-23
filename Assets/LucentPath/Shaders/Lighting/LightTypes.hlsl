#ifndef LUCENT_PATH_LIGHT_TYPES_INCLUDED
#define LUCENT_PATH_LIGHT_TYPES_INCLUDED

#define LIGHT_CATEGORY_NONE     0u
#define LIGHT_CATEGORY_ANALYTIC 1u
#define LIGHT_CATEGORY_ENV      2u

struct LightSelector
{
    uint category;
    uint lightIndex;
    float3 envDirection;
    float2 areaUv;
};

struct LightSample
{
    float3 wi;
    float3 radiance;
    float3 samplePosition;
    float3 lightNormal;
    float distance;
    float areaPdf;
    float solidAnglePdf;
    uint flags;
};

#define LIGHT_SAMPLE_FLAG_DELTA       0x1u
#define LIGHT_SAMPLE_FLAG_INFINITE    0x2u
#define LIGHT_SAMPLE_FLAG_VALID       0x4u

LightSelector MakeEmptyLightSelector()
{
    LightSelector selector;
    selector.category = LIGHT_CATEGORY_NONE;
    selector.lightIndex = 0u;
    selector.envDirection = float3(0.0, 1.0, 0.0);
    selector.areaUv = float2(0.0, 0.0);
    return selector;
}

LightSample MakeEmptyLightSample()
{
    LightSample sample;
    sample.wi = float3(0.0, 0.0, 0.0);
    sample.radiance = float3(0.0, 0.0, 0.0);
    sample.samplePosition = float3(0.0, 0.0, 0.0);
    sample.lightNormal = float3(0.0, 1.0, 0.0);
    sample.distance = 0.0;
    sample.areaPdf = 0.0;
    sample.solidAnglePdf = 0.0;
    sample.flags = 0u;
    return sample;
}

bool LightSampleIsValid(LightSample sample)
{
    return (sample.flags & LIGHT_SAMPLE_FLAG_VALID) != 0u;
}

bool LightSampleIsDelta(LightSample sample)
{
    return (sample.flags & LIGHT_SAMPLE_FLAG_DELTA) != 0u;
}

bool LightSampleIsInfinite(LightSample sample)
{
    return (sample.flags & LIGHT_SAMPLE_FLAG_INFINITE) != 0u;
}

#endif
