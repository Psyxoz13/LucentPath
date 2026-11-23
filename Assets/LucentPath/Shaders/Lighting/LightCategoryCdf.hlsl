#ifndef LUCENT_PATH_LIGHT_CATEGORY_CDF_INCLUDED
#define LUCENT_PATH_LIGHT_CATEGORY_CDF_INCLUDED

#include "LightTypes.hlsl"

#ifndef LUCENT_PATH_LIGHT_CATEGORY_DECLARED
#define LUCENT_PATH_LIGHT_CATEGORY_DECLARED
float _AnalyticLightsTotalPower;
#endif

float GetEnvLightTotalPower()
{
    if (_EnvSamplingReady == 0)
        return 0.0;

    return max(_EnvTotalLuminanceBuffer[0], 0.0);
}

float GetAnalyticLightsTotalPower()
{
    if (_LightCount <= 0)
        return 0.0;

    return max(_AnalyticLightsTotalPower, 0.0);
}

float GetLightCategoryEnvPdf()
{
    float envPower = GetEnvLightTotalPower();
    float analyticPower = GetAnalyticLightsTotalPower();
    float total = envPower + analyticPower;
    if (total <= 1e-12)
        return 0.0;
    return envPower / total;
}

float GetLightCategoryAnalyticPdf()
{
    float envPower = GetEnvLightTotalPower();
    float analyticPower = GetAnalyticLightsTotalPower();
    float total = envPower + analyticPower;
    if (total <= 1e-12)
        return 0.0;
    return analyticPower / total;
}

uint SampleLightCategory(inout float u, out float categoryPdf)
{
    float envPdf = GetLightCategoryEnvPdf();
    float analyticPdf = GetLightCategoryAnalyticPdf();
    float total = envPdf + analyticPdf;
    if (total <= 1e-12)
    {
        categoryPdf = 0.0;
        return LIGHT_CATEGORY_NONE;
    }

    float uClamped = saturate(u);
    if (uClamped < envPdf)
    {
        categoryPdf = envPdf;
        u = (envPdf > 0.0) ? saturate(uClamped / envPdf) : 0.0;
        return LIGHT_CATEGORY_ENV;
    }

    categoryPdf = analyticPdf;
    float remainder = uClamped - envPdf;
    u = (analyticPdf > 0.0) ? saturate(remainder / analyticPdf) : 0.0;
    return LIGHT_CATEGORY_ANALYTIC;
}

float EvaluateLightCategoryPdf(uint category)
{
    if (category == LIGHT_CATEGORY_ENV)
        return GetLightCategoryEnvPdf();
    if (category == LIGHT_CATEGORY_ANALYTIC)
        return GetLightCategoryAnalyticPdf();
    return 0.0;
}

#endif
