#ifndef LUCENT_PATH_ANALYTIC_LIGHT_DISTRIBUTION_INCLUDED
#define LUCENT_PATH_ANALYTIC_LIGHT_DISTRIBUTION_INCLUDED

#ifndef LUCENT_PATH_ANALYTIC_LIGHT_DISTRIBUTION_DECLARED
#define LUCENT_PATH_ANALYTIC_LIGHT_DISTRIBUTION_DECLARED
int _LightNeeRisCandidates;
StructuredBuffer<uint> _DistantLightIndices;
int _DistantLightCount;
#endif

float GetRangeFade(float distanceSqr, float range)
{
    float rangeSqr = max(range * range, 1e-6);
    float fade = saturate(1.0 - distanceSqr / rangeSqr);
    return fade * fade;
}

bool IsAnalyticAreaLight(GpuLight light)
{
    float lightType = light.directionType.w;
    return lightType == LIGHT_TYPE_RECTANGLE || lightType == LIGHT_TYPE_DISC;
}

float GetAnalyticAreaLightArea(GpuLight light)
{
    float lightType = light.directionType.w;
    if (lightType == LIGHT_TYPE_RECTANGLE)
        return 4.0 * light.areaData.x * light.areaData.y;

    if (lightType == LIGHT_TYPE_DISC)
        return PI * light.areaData.z * light.areaData.z;

    return 0.0;
}

float GetAnalyticLightBoundRadius(GpuLight light)
{
    float lightType = light.directionType.w;

    if (lightType == LIGHT_TYPE_RECTANGLE)
    {
        return length(light.areaData.xy);
    }

    if (lightType == LIGHT_TYPE_DISC)
    {
        return light.areaData.z;
    }

    return max(light.areaData.x, 0.0);
}

float GetAnalyticLightImportance(GpuLight light, float3 position)
{
    float lightType = light.directionType.w;

    if (lightType == LIGHT_TYPE_DIRECTIONAL)
    {
        return 0.0;
    }

    float emittedLuminance = dot(max(light.colorIntensity.rgb, 0.0), float3(.2126, .7152, .0722)) * max(light.colorIntensity.w, 0.0);
    float3 toPosition = position - light.positionRange.xyz;
    float distanceSqr = dot(toPosition, toPosition);
    float distance = sqrt(distanceSqr);
    float boundRadius = GetAnalyticLightBoundRadius(light);
    float nearestDistance = max(distance - boundRadius, 0.0);
    float rangeFade = GetRangeFade(nearestDistance * nearestDistance, light.positionRange.w);

    if (emittedLuminance <= 0.0 || rangeFade <= 0.0)
    {
        return 0.0;
    }

    float3 toPositionDirection = distance > 1e-6 ? toPosition / distance : float3(0.0, 1.0, 0.0);
    float boundAngle = distance > boundRadius ? asin(saturate(boundRadius / distance)) : PI;
    float power;
    float orientationCosine = 1.0;

    if (lightType == LIGHT_TYPE_POINT)
    {
        power = 4.0 * PI * emittedLuminance;
    }
    else if (lightType == LIGHT_TYPE_SPOT)
    {
        float cosFalloffEnd = clamp(light.spotAngles.x, -1.0, 1.0);
        float cosFalloffStart = clamp(light.spotAngles.y, cosFalloffEnd, 1.0);
        power = 2.0 * PI * emittedLuminance * ((1.0 - cosFalloffStart) + (cosFalloffStart - cosFalloffEnd) * .5);

        float emissionAngle = acos(cosFalloffStart);
        float falloffAngle = acos(cosFalloffEnd) - emissionAngle;
        float3 spotDirection = SafeNormalize(light.directionType.xyz, float3(0.0, -1.0, 0.0));
        float receiverAngle = acos(clamp(dot(spotDirection, toPositionDirection), -1.0, 1.0));
        float boundedAngle = max(receiverAngle - emissionAngle - boundAngle, 0.0);

        if (boundedAngle >= falloffAngle)
        {
            return 0.0;
        }

        orientationCosine = cos(boundedAngle);
    }
    else
    {
        power = PI * emittedLuminance * GetAnalyticAreaLightArea(light);

        float3 lightNormal = SafeNormalize(light.directionType.xyz, float3(0.0, -1.0, 0.0));
        float receiverAngle = acos(clamp(dot(lightNormal, toPositionDirection), -1.0, 1.0));
        float boundedAngle = max(receiverAngle - boundAngle, 0.0);

        if (boundedAngle >= .5 * PI)
        {
            return 0.0;
        }

        orientationCosine = cos(boundedAngle);
    }

    return power * orientationCosine * rangeFade / max(max(distanceSqr, boundRadius * boundRadius), 1e-8);
}

float GetAnalyticLightImportanceSum(float3 position)
{
    float importanceSum = 0.0;

    [loop]
    for (int lightIndex = 0; lightIndex < _LightCount; lightIndex++)
    {
        importanceSum += GetAnalyticLightImportance(_Lights[lightIndex], position);
    }

    return importanceSum;
}

float GetAnalyticLightPickPdf(float3 position, uint lightIndex)
{
    if (_LightCount <= 0 || lightIndex >= (uint)_LightCount)
    {
        return 0.0;
    }

    float importanceSum = GetAnalyticLightImportanceSum(position);

    if (importanceSum <= 0.0)
    {
        return 0.0;
    }

    return GetAnalyticLightImportance(_Lights[lightIndex], position) / importanceSum;
}

uint SampleAnalyticLightIndex(float3 position, inout float u, out float pickPdf)
{
    pickPdf = 0.0;

    if (_LightCount <= 0)
    {
        return 0u;
    }

    float importanceSum = GetAnalyticLightImportanceSum(position);

    if (importanceSum <= 0.0)
    {
        return 0u;
    }

    float target = min(saturate(u), 1.0 - 1e-7) * importanceSum;
    float cumulative = 0.0;
    uint selectedIndex = 0u;
    float selectedImportance = 0.0;
    float selectedStart = 0.0;

    [loop]
    for (int lightIndex = 0; lightIndex < _LightCount; lightIndex++)
    {
        float importance = GetAnalyticLightImportance(_Lights[lightIndex], position);

        if (importance <= 0.0)
        {
            continue;
        }

        selectedIndex = (uint)lightIndex;
        selectedImportance = importance;
        selectedStart = cumulative;
        cumulative += importance;

        if (target < cumulative)
        {
            break;
        }
    }

    pickPdf = selectedImportance / importanceSum;
    u = saturate((target - selectedStart) / max(selectedImportance, 1e-20));

    return selectedIndex;
}

#endif
