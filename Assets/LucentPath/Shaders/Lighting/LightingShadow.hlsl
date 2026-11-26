#ifndef LUCENT_PATH_LIGHTING_SHADOW_INCLUDED
#define LUCENT_PATH_LIGHTING_SHADOW_INCLUDED

#include "../Bsdf/BsdfLobes.hlsl"
#include "AnalyticLightDistribution.hlsl"
#include "EnvSampler.hlsl"
#include "EnvImportanceSampling.hlsl"
#include "SurfelGuide.hlsl"

float PowerHeuristic(float a, float b)
{
    float a2 = a * a;
    float b2 = b * b;
    return a2 / max(a2 + b2, 1e-6);
}

float3 EvaluateEnvironment(float3 direction)
{
    return EvaluateEnvironmentRadiance(direction);
}

bool IntersectAnalyticAreaLight(GpuLight light, Ray ray, out float hitT, out float3 radiance, out float lightPdf)
{
    hitT = 0.0;
    radiance = 0.0;
    lightPdf = 0.0;

    float sphereType = light.directionType.w;
    if (sphereType == LIGHT_TYPE_POINT || sphereType == LIGHT_TYPE_SPOT)
    {
        float radius = light.areaData.x;
        if (radius <= 0.0)
            return false;

        float3 oc = light.positionRange.xyz - ray.origin;
        float distSqr = dot(oc, oc);
        float dist = sqrt(distSqr);
        if (dist > light.positionRange.w || dist <= radius)
            return false;

        float b = dot(ray.direction, oc);
        float disc = b * b - (distSqr - radius * radius);
        if (disc < 0.0)
            return false;
        float t = b - sqrt(disc);
        if (t <= EPSILON || t >= ray.maxDistance)
            return false;

        float cosThetaMax = sqrt(max(1.0 - radius * radius / distSqr, 0.0));
        float3 L = light.colorIntensity.rgb * light.colorIntensity.w * GetRangeFade(distSqr, light.positionRange.w) / max(PI * radius * radius, 1e-6);
        if (sphereType == LIGHT_TYPE_SPOT)
        {
            float3 spotDir = SafeNormalize(light.directionType.xyz, float3(0.0, -1.0, 0.0));
            float cosThetaSpot = saturate(dot(-(oc / dist), spotDir));
            float spotFade = saturate((cosThetaSpot - light.spotAngles.x) / max(light.spotAngles.y - light.spotAngles.x, 1e-5));
            L *= spotFade * spotFade;
        }
        if (Max3(L) <= 1e-8)
            return false;

        hitT = t;
        radiance = L;
        lightPdf = 1.0 / max(2.0 * PI * (1.0 - cosThetaMax), 1e-6);
        return true;
    }

    if (!IsAnalyticAreaLight(light))
        return false;

    float area = GetAnalyticAreaLightArea(light);
    if (area <= 0.0)
        return false;

    float3 lightNormal = SafeNormalize(light.directionType.xyz, float3(0.0, -1.0, 0.0));
    float denom = dot(ray.direction, lightNormal);
    float lightFacing = -denom;
    if (lightFacing <= 1e-6)
        return false;

    float t = dot(light.positionRange.xyz - ray.origin, lightNormal) / denom;
    if (t <= EPSILON || t >= ray.maxDistance)
        return false;

    float3 hitPosition = ray.origin + ray.direction * t;
    float3 local = hitPosition - light.positionRange.xyz;
    float localX = dot(local, light.right.xyz);
    float localY = dot(local, light.up.xyz);
    float lightType = light.directionType.w;

    if (lightType == LIGHT_TYPE_RECTANGLE)
    {
        if (abs(localX) > light.areaData.x || abs(localY) > light.areaData.y)
            return false;
    }
    else
    {
        float radius = light.areaData.z;
        if (localX * localX + localY * localY > radius * radius)
            return false;
    }

    float distanceSqr = dot(hitPosition - ray.origin, hitPosition - ray.origin);
    float pdf = distanceSqr / max(lightFacing * area, 1e-6);
    if (pdf <= 0.0)
        return false;

    float3 emittedRadiance = light.colorIntensity.rgb * light.colorIntensity.w * GetRangeFade(distanceSqr, light.positionRange.w);
    if (Max3(emittedRadiance) <= 1e-8)
        return false;

    hitT = t;
    radiance = emittedRadiance;
    lightPdf = pdf;
    return true;
}

bool FindClosestAnalyticAreaLightHit(Ray ray, float maxDistance, out float hitT, out float3 radiance, out float lightPdf)
{
    hitT = maxDistance;
    radiance = 0.0;
    lightPdf = 0.0;

    if (_AnalyticAreaLightCount <= 0)
        return false;

    Ray lightRay = ray;
    lightRay.maxDistance = max(maxDistance, EPSILON);
    bool found = false;
    [loop]
    for (int i = 0; i < _AnalyticAreaLightCount; i++)
    {
        float candidateT;
        float3 candidateRadiance;
        float candidatePdf;
        if (!IntersectAnalyticAreaLight(_Lights[i], lightRay, candidateT, candidateRadiance, candidatePdf))
            continue;

        float lightSelectionPdf = GetAnalyticLightPickPdf(ray.origin, (uint)i);
        if (lightSelectionPdf <= 0.0)
            continue;

        if (!found || candidateT < hitT)
        {
            found = true;
            hitT = candidateT;
            radiance = candidateRadiance;
            lightPdf = lightSelectionPdf * candidatePdf;
        }
    }

    return found && Max3(radiance) > 1e-8;
}

float GetBsdfSampledLightMisWeight(uint pathDepth, bool previousBounceSpecular, float previousBsdfPdf, float lightPdf)
{
    if (pathDepth == 0u || previousBounceSpecular || previousBsdfPdf <= 0.0)
        return 1.0;

    return PowerHeuristic(previousBsdfPdf, lightPdf);
}

bool ShouldAccumulateBsdfSampledAnalyticLight(uint pathDepth, bool previousBounceSpecular, float previousBsdfPdf)
{
    return pathDepth > 0u && (previousBounceSpecular || previousBsdfPdf > 0.0);
}

float3 SampleRectangleLight(GpuLight light, float2 sampleUv)
{
    float2 u = sampleUv * 2.0 - 1.0;
    return light.positionRange.xyz + light.right.xyz * (u.x * light.areaData.x) + light.up.xyz * (u.y * light.areaData.y);
}

float3 SampleDiscLight(GpuLight light, float2 sampleUv)
{
    float radius = sqrt(sampleUv.x) * light.areaData.z;
    float phi = 2.0 * PI * sampleUv.y;
    float2 disk = float2(cos(phi), sin(phi)) * radius;
    return light.positionRange.xyz + light.right.xyz * disk.x + light.up.xyz * disk.y;
}

bool GetDirectLightSample(GpuLight light, SurfaceData surface, float2 lightSample, out float3 wi, out float maxDistance, out float3 radiance, out float pdf, out bool isDelta)
{
    wi = 0.0;
    maxDistance = 0.0;
    radiance = 0.0;
    pdf = 0.0;
    isDelta = false;

    float lightType = light.directionType.w;
    if (lightType == LIGHT_TYPE_DIRECTIONAL)
    {
        wi = SafeNormalize(light.directionType.xyz, float3(0.0, 1.0, 0.0));
        maxDistance = 1e30;
        radiance = light.colorIntensity.rgb * light.colorIntensity.w;
        pdf = 1.0;
        isDelta = true;
        return Max3(radiance) > 1e-8;
    }

    if (lightType == LIGHT_TYPE_POINT)
    {
        float radius = light.areaData.x;
        float3 toLight = light.positionRange.xyz - surface.position;
        float distanceSqr = dot(toLight, toLight);
        if (distanceSqr <= 1e-8)
            return false;

        float distance = sqrt(distanceSqr);
        if (distance > light.positionRange.w)
            return false;

        float3 axis = toLight / distance;
        if (radius <= 0.0 || distance <= radius)
        {
            wi = axis;
            maxDistance = distance;
            radiance = light.colorIntensity.rgb * light.colorIntensity.w * (GetRangeFade(distanceSqr, light.positionRange.w) / max(distanceSqr, 1e-4));
            pdf = 1.0;
            isDelta = true;
            return Max3(radiance) > 1e-8;
        }

        float cosThetaMax = sqrt(max(1.0 - radius * radius / distanceSqr, 0.0));
        float cosTheta = 1.0 - lightSample.x * (1.0 - cosThetaMax);
        float sinTheta = sqrt(max(1.0 - cosTheta * cosTheta, 0.0));
        float phi = 2.0 * PI * lightSample.y;
        float3 tangent;
        float3 bitangent;
        BuildBasis(axis, tangent, bitangent);
        wi = SafeNormalize(sinTheta * cos(phi) * tangent + sinTheta * sin(phi) * bitangent + cosTheta * axis, axis);

        float bRoot = dot(wi, toLight);
        float discRoot = bRoot * bRoot - (distanceSqr - radius * radius);
        maxDistance = max(discRoot > 0.0 ? (bRoot - sqrt(discRoot)) : distance, EPSILON);
        pdf = 1.0 / max(2.0 * PI * (1.0 - cosThetaMax), 1e-6);
        radiance = light.colorIntensity.rgb * light.colorIntensity.w * GetRangeFade(distanceSqr, light.positionRange.w) / max(PI * radius * radius, 1e-6);
        isDelta = false;
        return Max3(radiance) > 1e-8;
    }

    if (lightType == LIGHT_TYPE_SPOT)
    {
        float radius = light.areaData.x;
        float3 toLight = light.positionRange.xyz - surface.position;
        float distanceSqr = dot(toLight, toLight);
        if (distanceSqr <= 1e-8)
            return false;

        float distance = sqrt(distanceSqr);
        if (distance > light.positionRange.w)
            return false;

        float3 axis = toLight / distance;
        float3 spotDirection = SafeNormalize(light.directionType.xyz, float3(0.0, -1.0, 0.0));
        float cosThetaSpot = saturate(dot(-axis, spotDirection));
        float spotFade = saturate((cosThetaSpot - light.spotAngles.x) / max(light.spotAngles.y - light.spotAngles.x, 1e-5));
        spotFade *= spotFade;
        if (spotFade <= 1e-6)
            return false;

        if (radius <= 0.0 || distance <= radius)
        {
            wi = axis;
            maxDistance = distance;
            radiance = light.colorIntensity.rgb * light.colorIntensity.w * spotFade * (GetRangeFade(distanceSqr, light.positionRange.w) / max(distanceSqr, 1e-4));
            pdf = 1.0;
            isDelta = true;
            return Max3(radiance) > 1e-8;
        }

        float cosThetaMax = sqrt(max(1.0 - radius * radius / distanceSqr, 0.0));
        float cosTheta = 1.0 - lightSample.x * (1.0 - cosThetaMax);
        float sinTheta = sqrt(max(1.0 - cosTheta * cosTheta, 0.0));
        float phi = 2.0 * PI * lightSample.y;
        float3 tangent;
        float3 bitangent;
        BuildBasis(axis, tangent, bitangent);
        wi = SafeNormalize(sinTheta * cos(phi) * tangent + sinTheta * sin(phi) * bitangent + cosTheta * axis, axis);

        float bRoot = dot(wi, toLight);
        float discRoot = bRoot * bRoot - (distanceSqr - radius * radius);
        maxDistance = max(discRoot > 0.0 ? (bRoot - sqrt(discRoot)) : distance, EPSILON);
        pdf = 1.0 / max(2.0 * PI * (1.0 - cosThetaMax), 1e-6);
        radiance = light.colorIntensity.rgb * light.colorIntensity.w * spotFade * GetRangeFade(distanceSqr, light.positionRange.w) / max(PI * radius * radius, 1e-6);
        isDelta = false;
        return Max3(radiance) > 1e-8;
    }

    if (lightType == LIGHT_TYPE_RECTANGLE || lightType == LIGHT_TYPE_DISC)
    {
        float3 samplePosition = lightType == LIGHT_TYPE_RECTANGLE ? SampleRectangleLight(light, lightSample) : SampleDiscLight(light, lightSample);
        float3 toLight = samplePosition - surface.position;
        float distanceSqr = dot(toLight, toLight);
        if (distanceSqr <= 1e-8)
            return false;

        float distance = sqrt(distanceSqr);
        if (distance > light.positionRange.w)
            return false;

        wi = toLight / distance;
        maxDistance = distance;
        float3 lightNormal = SafeNormalize(light.directionType.xyz, float3(0.0, -1.0, 0.0));
        float lightFacing = saturate(dot(lightNormal, -wi));
        if (lightFacing <= 1e-6)
            return false;

        float area = max(GetAnalyticAreaLightArea(light), 1e-4);
        radiance = light.colorIntensity.rgb * light.colorIntensity.w * GetRangeFade(distanceSqr, light.positionRange.w);
        pdf = distanceSqr / max(lightFacing * area, 1e-6);
        isDelta = false;
        return Max3(radiance) > 1e-8;
    }

    return false;
}


float2 ProjectSystemShadowHistoryWorldToUv(float3 worldPosition)
{
    float4 clip = mul(_PrevViewProjMatrix, float4(worldPosition, 1.0));
    float invW = rcp(max(abs(clip.w), 1e-6));
    return clip.xy * invW * 0.5 + 0.5;
}

float GetSystemShadowHistoryViewDepth(float3 worldPosition)
{
    return abs(mul(_ViewMatrix, float4(worldPosition, 1.0)).z);
}

#if defined(LUCENT_PATH_SHADOW_DIELECTRIC_TRACERAY)
bool TraceSystemShadowHit(uint2 pixel, Ray shadowRay, bool writeShadowGuide)
{
    RayDesc rayDesc;
    rayDesc.Origin = shadowRay.origin;
    rayDesc.Direction = SafeNormalize(shadowRay.direction, float3(0.0, 1.0, 0.0));
    rayDesc.TMin = max(_SurfaceBias, EPSILON);
    rayDesc.TMax = max(shadowRay.maxDistance, EPSILON);

    RayPayload payload;
    InitRayPayloadMiss(payload, rayDesc.TMax, 1u);

    TraceRay(
        _RayTracingAccelerationStructure,
        RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH | RAY_FLAG_SKIP_PROCEDURAL_PRIMITIVES,
        0xFF,
        0,
        1,
        0,
        rayDesc,
        payload);

    bool hit = GetRayPayloadHit(payload) != 0u;
#if defined(LUCENT_PATH_KERNEL_DIRECT_LIGHTING)
    if (hit && writeShadowGuide)
    {
        float3 hitPosition = rayDesc.Origin + rayDesc.Direction * GetRayPayloadHitDistance(payload);
        float currentDepth = GetSystemShadowHistoryViewDepth(hitPosition);
        float2 currentUv = RemoveCameraJitterFromUv((float2(pixel) + 0.5) / float2(_Width, _Height));
        float2 prevUv = ProjectSystemShadowHistoryWorldToUv(hitPosition);
        uint instanceId = GetRayPayloadInstanceId(payload);

        _ShadowGuide0[pixel] = float4(-rayDesc.Direction, currentDepth);
        _ShadowGuide1[pixel] = float4(currentUv - prevUv, (float)instanceId, 0.0);
        _ShadowGuide2[pixel] = float4(GUIDE_PATH_KIND_SHADOW_BLOCKER, 0.0, 0.0, 0.0);
        _FrameShadowSum[pixel] = _FrameShadowSum[pixel] + float4(1.0, 0.0, 0.0, 0.0);
    }
#endif

    return hit;
}
#else
bool TraceSystemShadowHit(uint2 pixel, Ray shadowRay, bool writeShadowGuide)
{
    RayDesc rayDesc;
    rayDesc.Origin = shadowRay.origin;
    rayDesc.Direction = SafeNormalize(shadowRay.direction, float3(0.0, 1.0, 0.0));
    rayDesc.TMin = max(_SurfaceBias, EPSILON);
    rayDesc.TMax = max(shadowRay.maxDistance, EPSILON);

    RayQuery<RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH | RAY_FLAG_SKIP_PROCEDURAL_PRIMITIVES> rayQuery;
    rayQuery.TraceRayInline(
        _RayTracingAccelerationStructure,
        RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH | RAY_FLAG_SKIP_PROCEDURAL_PRIMITIVES,
        0xFF,
        rayDesc);
    rayQuery.Proceed();
    bool hit = rayQuery.CommittedStatus() == COMMITTED_TRIANGLE_HIT;
#if defined(LUCENT_PATH_KERNEL_DIRECT_LIGHTING)
    if (hit && writeShadowGuide)
    {
        float t = rayQuery.CommittedRayT();
        float3 hitPosition = rayDesc.Origin + rayDesc.Direction * t;
        float currentDepth = GetSystemShadowHistoryViewDepth(hitPosition);
        float2 currentUv = RemoveCameraJitterFromUv((float2(pixel) + 0.5) / float2(_Width, _Height));
        float2 prevUv = ProjectSystemShadowHistoryWorldToUv(hitPosition);
        uint instanceId = rayQuery.CommittedInstanceID();

        _ShadowGuide0[pixel] = float4(-rayDesc.Direction, currentDepth);
        _ShadowGuide1[pixel] = float4(currentUv - prevUv, (float)instanceId, 0.0);
        _ShadowGuide2[pixel] = float4(GUIDE_PATH_KIND_SHADOW_BLOCKER, 0.0, 0.0, 0.0);
        _FrameShadowSum[pixel] = _FrameShadowSum[pixel] + float4(1.0, 0.0, 0.0, 0.0);
    }
#endif

    return hit;
}
#endif

#if defined(LUCENT_PATH_SHADOW_DIELECTRIC_TRACERAY)

#ifndef LUCENT_PATH_SHADOW_MAX_DIELECTRIC_STEPS
#define LUCENT_PATH_SHADOW_MAX_DIELECTRIC_STEPS 8
#endif

#ifndef LUCENT_PATH_SHADOW_FRESNEL_CLAMP
#define LUCENT_PATH_SHADOW_FRESNEL_CLAMP 0.9
#endif

SurfaceData GetSurface(GpuSurfaceHit hit, Ray ray);

float3 TraceShadowTransmission(uint2 pixel, Ray shadowRay, bool allowRefractiveTraversal, bool writeShadowGuide, inout uint seed, out float3 transmittedDirection, out float3 sourceAirOrigin)
{
    float3 direction = SafeNormalize(shadowRay.direction, float3(0.0, 1.0, 0.0));
    transmittedDirection = direction;
    sourceAirOrigin = shadowRay.origin;

    float bias = max(_SurfaceBias, EPSILON);
    float3 origin = shadowRay.origin;
    float remaining = max(shadowRay.maxDistance, EPSILON);

    float3 transmittance = float3(1.0, 1.0, 1.0);
    bool insideDielectric = false;
    float3 dielectricTint = float3(1.0, 1.0, 1.0);
    float dielectricDepth = 1.0;

    [loop]
    for (int dielectricStep = 0; dielectricStep < LUCENT_PATH_SHADOW_MAX_DIELECTRIC_STEPS; dielectricStep++)
    {
        if (remaining <= EPSILON)
            return transmittance;

        RayDesc rayDesc;
        rayDesc.Origin = origin;
        rayDesc.Direction = direction;
        rayDesc.TMin = bias;
        rayDesc.TMax = remaining;

        RayPayload payload;
        InitRayPayloadMiss(payload, remaining, 1u);

        TraceRay(
            _RayTracingAccelerationStructure,
            RAY_FLAG_SKIP_PROCEDURAL_PRIMITIVES,
            0xFF,
            0,
            1,
            0,
            rayDesc,
            payload);

        if (GetRayPayloadHit(payload) == 0u)
        {
            if (insideDielectric)
                transmittance *= ComputeVolumeAttenuationFromTint(dielectricTint, remaining, dielectricDepth);
            return transmittance;
        }

        float segmentDistance = max(GetRayPayloadHitDistance(payload), 0.0);
        if (insideDielectric)
        {
            transmittance *= ComputeVolumeAttenuationFromTint(dielectricTint, segmentDistance, dielectricDepth);
            if (Max3(transmittance) <= 1e-4)
                return float3(0.0, 0.0, 0.0);
        }

        Ray segmentRay;
        segmentRay.origin = origin;
        segmentRay.direction = direction;
        segmentRay.maxDistance = remaining;
        GpuSurfaceHit segmentSurfaceHit = UnpackHitPayload(payload, segmentRay.origin, segmentRay.direction);
        SurfaceData surface = GetSurface(segmentSurfaceHit, segmentRay);

        bool traversable = CanTraverseShadowDielectric(surface);
        bool thinDielectric = IsThinDielectricSurface(surface) && surface.transmissionWeight > 1e-3 && SurfaceSpecularEffectivelySmooth(surface);

        if (!traversable && !thinDielectric)
        {
#if defined(LUCENT_PATH_KERNEL_DIRECT_LIGHTING)
            if (writeShadowGuide)
            {
                float3 hitPosition = surface.position;
                float currentDepth = GetSystemShadowHistoryViewDepth(hitPosition);
                float2 currentUv = RemoveCameraJitterFromUv((float2(pixel) + 0.5) / float2(_Width, _Height));
                float2 prevUv = ProjectSystemShadowHistoryWorldToUv(hitPosition);
                uint instanceId = segmentSurfaceHit.instanceId;

                _ShadowGuide0[pixel] = float4(-direction, currentDepth);
                _ShadowGuide1[pixel] = float4(currentUv - prevUv, (float)instanceId, 0.0);
                _ShadowGuide2[pixel] = float4(GUIDE_PATH_KIND_SHADOW_BLOCKER, 0.0, 0.0, 0.0);
                _FrameShadowSum[pixel] = _FrameShadowSum[pixel] + float4(1.0, 0.0, 0.0, 0.0);
            }
#endif
            return float3(0.0, 0.0, 0.0);
        }

        if (thinDielectric)
        {
            float3 thinT = ComputeThinDielectricTransmission(surface, direction);
            transmittance *= thinT;
            if (Max3(transmittance) <= 1e-4)
                return float3(0.0, 0.0, 0.0);
        }
        else
        {
            float cosIncidence = saturate(abs(dot(direction, surface.normal)));
            float fresnel = FresnelDielectric(cosIncidence, 1.0, ResolveSurfaceSpecularIor(surface));
            fresnel = min(fresnel, LUCENT_PATH_SHADOW_FRESNEL_CLAMP);
            float interfaceTransmit = saturate(1.0 - fresnel) * saturate(surface.transmissionWeight);
            transmittance *= float3(interfaceTransmit, interfaceTransmit, interfaceTransmit);
            if (Max3(transmittance) <= 1e-4)
                return float3(0.0, 0.0, 0.0);

            if (surface.frontFace != 0u)
            {
                insideDielectric = true;
                dielectricTint = surface.transmissionColor;
                dielectricDepth = surface.transmissionDepth > 0.0 ? surface.transmissionDepth : 1.0;
            }
            else
            {
                insideDielectric = false;
            }
        }

        float advance = segmentDistance + bias;
        origin = origin + direction * advance;
        remaining = max(remaining - advance, 0.0);
    }

    return float3(0.0, 0.0, 0.0);
}

#else

#ifndef LUCENT_PATH_SHADOW_MAX_DIELECTRIC_STEPS
#define LUCENT_PATH_SHADOW_MAX_DIELECTRIC_STEPS 8
#endif

#ifndef LUCENT_PATH_SHADOW_FRESNEL_CLAMP
#define LUCENT_PATH_SHADOW_FRESNEL_CLAMP 0.9
#endif

StructuredBuffer<uint4> _MaterialCache;
int _MaterialCacheCapacity;

float3 TraceShadowTransmission(uint2 pixel, Ray shadowRay, bool allowRefractiveTraversal, bool writeShadowGuide, inout uint seed, out float3 transmittedDirection, out float3 sourceAirOrigin)
{
    float3 direction = SafeNormalize(shadowRay.direction, float3(0.0, 1.0, 0.0));
    transmittedDirection = direction;
    sourceAirOrigin = shadowRay.origin;

    float bias = max(_SurfaceBias, EPSILON);
    float3 origin = shadowRay.origin;
    float remaining = max(shadowRay.maxDistance, EPSILON);

    float3 transmittance = float3(1.0, 1.0, 1.0);
    bool insideDielectric = false;
    float3 dielectricTint = float3(1.0, 1.0, 1.0);
    float dielectricDepth = 1.0;

    [loop]
    for (int dielectricStep = 0; dielectricStep < LUCENT_PATH_SHADOW_MAX_DIELECTRIC_STEPS; dielectricStep++)
    {
        if (remaining <= EPSILON)
            return transmittance;

        RayDesc rayDesc;
        rayDesc.Origin = origin;
        rayDesc.Direction = direction;
        rayDesc.TMin = bias;
        rayDesc.TMax = remaining;

        RayQuery<RAY_FLAG_SKIP_PROCEDURAL_PRIMITIVES> rayQuery;
        rayQuery.TraceRayInline(_RayTracingAccelerationStructure, RAY_FLAG_SKIP_PROCEDURAL_PRIMITIVES, 0xFF, rayDesc);
        rayQuery.Proceed();

        if (rayQuery.CommittedStatus() != COMMITTED_TRIANGLE_HIT)
        {
            if (insideDielectric)
                transmittance *= ComputeVolumeAttenuationFromTint(dielectricTint, remaining, dielectricDepth);
            return transmittance;
        }

        float committedT = rayQuery.CommittedRayT();
        uint instanceId = rayQuery.CommittedInstanceID();
        uint geometryIndex = rayQuery.CommittedGeometryIndex();
        bool frontFace = rayQuery.CommittedTriangleFrontFace();

        uint cacheIndex = _LucentMaterialCacheSlot(instanceId, geometryIndex, _MaterialCacheCapacity);
        uint4 entry = _MaterialCache[cacheIndex];
        float4 transmissionColorWeight = _LucentUnpackUnorm4x8(entry.x);
        float3 tint = transmissionColorWeight.rgb;
        float tWeight = transmissionColorWeight.w;
        float thinFilmThickness;
        float matTransmissionDepth;
        _LucentUnpackHalf2(entry.z, thinFilmThickness, matTransmissionDepth);

        if (insideDielectric)
        {
            transmittance *= ComputeVolumeAttenuationFromTint(dielectricTint, committedT, dielectricDepth);
            if (Max3(transmittance) <= 1e-4)
                return float3(0.0, 0.0, 0.0);
        }

        if (tWeight <= 1e-3)
        {
#if defined(LUCENT_PATH_KERNEL_DIRECT_LIGHTING)
            if (writeShadowGuide)
            {
                float3 hitPosition = origin + direction * committedT;
                float currentDepth = GetSystemShadowHistoryViewDepth(hitPosition);
                float2 currentUv = RemoveCameraJitterFromUv((float2(pixel) + 0.5) / float2(_Width, _Height));
                float2 prevUv = ProjectSystemShadowHistoryWorldToUv(hitPosition);
                _ShadowGuide0[pixel] = float4(-direction, currentDepth);
                _ShadowGuide1[pixel] = float4(currentUv - prevUv, (float)instanceId, 0.0);
                _ShadowGuide2[pixel] = float4(GUIDE_PATH_KIND_SHADOW_BLOCKER, 0.0, 0.0, 0.0);
                _FrameShadowSum[pixel] = _FrameShadowSum[pixel] + float4(1.0, 0.0, 0.0, 0.0);
            }
#endif
            return float3(0.0, 0.0, 0.0);
        }

        float fresnel = min(0.04, LUCENT_PATH_SHADOW_FRESNEL_CLAMP);
        float interfaceTransmit = saturate(1.0 - fresnel) * saturate(tWeight);
        transmittance *= float3(interfaceTransmit, interfaceTransmit, interfaceTransmit);
        if (Max3(transmittance) <= 1e-4)
            return float3(0.0, 0.0, 0.0);

        if (frontFace)
        {
            insideDielectric = true;
            dielectricTint = tint;
            dielectricDepth = matTransmissionDepth > 0.0 ? matTransmissionDepth : 1.0;
        }
        else
        {
            insideDielectric = false;
        }

        float advance = committedT + bias;
        origin = origin + direction * advance;
        remaining = max(remaining - advance, 0.0);
    }

    return float3(0.0, 0.0, 0.0);
}

#endif



float ShadowTerminatorTerm(float3 geomN, float3 shadingN, float3 w)
{
    float cosGeom = dot(geomN, w);
    if (cosGeom <= 0.0)
        return 0.0;
    float cosShading = dot(shadingN, w);
    if (cosShading <= 0.0)
        return 0.0;
    float g = saturate(cosGeom / max(cosShading, 1e-4));
    return g * g * (3.0 - 2.0 * g);
}

float GetDirectLightingTerminator(SurfaceData surface, float3 wi)
{
    if (dot(surface.geomNormal, wi) >= 0.0)
    {
        return ShadowTerminatorTerm(surface.geomNormal, surface.normal, wi);
    }

    if (IsThinWalledSurface(surface) == false)
    {
        return 0.0;
    }

    return ShadowTerminatorTerm(-surface.geomNormal, -surface.normal, wi);
}

struct DirectLightingContext
{
    float3 wi;
    float3 shadowTransmission;
    float cosTheta;
    float terminator;
    float3 radiance;
    float lightSelectionPdf;
    float samplePdf;
    bool isDelta;
    bool isBsdfReachable;
    bool valid;
};

DirectLightingContext PrepareDirectLightingContextWithBSDF(uint2 pixel, SurfaceData surface, LobeSet set, PathSampler pathSample, uint bounce, uint shadowChannel)
{
    DirectLightingContext ctx;
    ctx.wi = 0.0;
    ctx.shadowTransmission = 0.0;
    ctx.cosTheta = 0.0;
    ctx.terminator = 1.0;
    ctx.radiance = 0.0;
    ctx.lightSelectionPdf = 0.0;
    ctx.samplePdf = 0.0;
    ctx.isDelta = false;
    ctx.isBsdfReachable = false;
    ctx.valid = false;

    if (_LightCount <= 0 || SelectableLobeCount(set) == 0u)
        return ctx;

    float pickSample = GetBounceLightPickSample(pathSample, bounce);
    float lightSelectionPdf;
    uint lightIndex = SampleAnalyticLightIndex(surface.position, pickSample, lightSelectionPdf);
    if (lightSelectionPdf <= 0.0)
        return ctx;

    float2 lightSample = GetBounceLightSample(pathSample, bounce);
    GpuLight light = _Lights[lightIndex];

    float3 wi;
    float maxDistance;
    float3 radiance;
    float samplePdf;
    bool isDelta;
    if (!GetDirectLightSample(light, surface, lightSample, wi, maxDistance, radiance, samplePdf, isDelta))
        return ctx;

    bool allowRefractiveTraversal = isDelta && light.directionType.w == LIGHT_TYPE_DIRECTIONAL;
    bool writeShadowGuide = shadowChannel == 0u && isDelta && bounce == 0u && _LightCount == 1;
    bool writesVisibility = writeShadowGuide || (isDelta && shadowChannel != 0u);
    float terminator = allowRefractiveTraversal ? 1.0 : GetDirectLightingTerminator(surface, wi);
    if (terminator <= 0.0 && !writesVisibility)
        return ctx;

    Ray shadowRay = SpawnRayFromSurface(surface.interaction, wi);
    shadowRay.direction = wi;
    shadowRay.maxDistance = max(maxDistance - _SurfaceBias * 2.0, EPSILON);

    uint shadowSeed = GetBounceLightSeed(pathSample, bounce);
    float3 transmittedWi;
    float3 sourceAirOrigin;
    float3 shadowTransmission = TraceShadowTransmission(pixel, shadowRay, allowRefractiveTraversal, writeShadowGuide, shadowSeed, transmittedWi, sourceAirOrigin);
#if defined(LUCENT_PATH_KERNEL_DIRECT_LIGHTING)
    if (isDelta)
    {
        float shadowVisibility = EncodeShadowVisibility(1.0 - saturate(Max3(shadowTransmission)));
        if (shadowChannel == 1u && !ShadowVisibilitySampled(_FrameShadowSum[pixel].g))
            SetFrameShadowChannel(pixel, 1u, shadowVisibility);
        else if (shadowChannel == 2u && !ShadowVisibilitySampled(_FrameShadowSum[pixel].b))
            SetFrameShadowChannel(pixel, 2u, shadowVisibility);
    }
#endif
    if (Max3(shadowTransmission) <= 1e-6)
        return ctx;

    if (allowRefractiveTraversal)
        wi = transmittedWi;

    float cosTheta = abs(dot(surface.normal, wi));
    if (cosTheta <= 0.0)
        return ctx;

    if (terminator <= 0.0)
        return ctx;

    ctx.wi = wi;
    ctx.shadowTransmission = shadowTransmission;
    ctx.cosTheta = cosTheta;
    ctx.terminator = terminator;
    ctx.radiance = radiance;
    ctx.lightSelectionPdf = lightSelectionPdf;
    ctx.samplePdf = samplePdf;
    ctx.isDelta = isDelta;
    ctx.isBsdfReachable = IsAnalyticAreaLight(light);
    ctx.valid = true;
    return ctx;
}

DirectLightingContext PrepareDirectLightingContext(uint2 pixel, SurfaceData surface, PathSampler pathSample, uint bounce)
{
    return PrepareDirectLightingContextWithBSDF(pixel, surface, ComputeLobeSet(surface), pathSample, bounce, 0u);
}

float3 EvaluateDirectLightingRadiance(DirectLightingContext ctx, float3 wo, float3 f, float bsdfPdf)
{
    float3 base = ctx.radiance * _DirectIntensity * ctx.cosTheta * ctx.shadowTransmission * ctx.terminator;
    if (ctx.isDelta)
        return f * base / ctx.lightSelectionPdf;

    float totalLightPdf = max(ctx.lightSelectionPdf * ctx.samplePdf, 1e-6);
    float misWeight = ctx.isBsdfReachable ? PowerHeuristic(totalLightPdf, bsdfPdf) : 1.0;
    return f * base * misWeight / totalLightPdf;
}

void EstimateAnalyticDirectLightingRis(
    uint2 pixel,
    SurfaceData surface,
    ShadingFrame frame,
    LobeSet set,
    float3 wo,
    PathSampler pathSample,
    uint bounce,
    uint shadowChannel,
    out float3 surfaceContribution,
    out float3 reflectionContribution,
    out float3 transmissionContribution)
{
    surfaceContribution = 0.0;
    reflectionContribution = 0.0;
    transmissionContribution = 0.0;

    if (_LightCount <= 0 || SelectableLobeCount(set) == 0u)
        return;

    uint candidateCount = (uint)max(_LightNeeRisCandidates, 1);

    float wSum = 0.0;
    float3 selWi = 0.0;
    float selMaxDistance = 0.0;
    float3 selSurfaceF = 0.0;
    float3 selReflectionF = 0.0;
    float3 selTransmissionF = 0.0;
    float3 selRadiance = 0.0;
    float selCos = 0.0;
    float selTerminator = 0.0;
    float selMis = 0.0;
    float selTarget = 0.0;
    bool selIsDelta = false;
    bool selIsDirectional = false;
    bool hasSelection = false;

    [loop]
    for (uint i = 0u; i < candidateCount; ++i)
    {
        float pickSample;
        float2 lightSample;
        if (i == 0u)
        {
            pickSample = GetBounceLightPickSample(pathSample, bounce);
            lightSample = GetBounceLightSample(pathSample, bounce);
        }
        else
        {
            uint candHash = Hash(pathSample.pixelSeed ^ pathSample.sampleIndex ^ (bounce * 0xA7F1E9C3u) ^ (i * 0x9E3779B9u) ^ 0x6C8E9CF5u);
            pickSample = UintToFloat01(candHash);
            lightSample = float2(UintToFloat01(Hash(candHash)), UintToFloat01(Hash(candHash ^ 0x68E31DA4u)));
        }

        float lightSelectionPdf;
        uint lightIndex = SampleAnalyticLightIndex(surface.position, pickSample, lightSelectionPdf);
        if (lightSelectionPdf <= 0.0)
            continue;

        GpuLight light = _Lights[lightIndex];
        float3 wi;
        float maxDistance;
        float3 radiance;
        float samplePdf;
        bool isDelta;
        if (!GetDirectLightSample(light, surface, lightSample, wi, maxDistance, radiance, samplePdf, isDelta))
            continue;

        float cosTheta = abs(dot(surface.normal, wi));
        if (cosTheta <= 0.0 || Max3(radiance) <= 1e-8)
            continue;

        float3 sF;
        float3 rF;
        float3 tF;
        BSDF_f_AllSignals(surface, frame, set, wo, wi, TRANSPORT_MODE_RADIANCE, sF, rF, tF);
        float3 fTotal = sF + rF + tF;
        if (Max3(fTotal) <= 1e-8)
            continue;

        bool isDirectional = light.directionType.w == LIGHT_TYPE_DIRECTIONAL;
        float terminator = (isDelta && isDirectional) ? 1.0 : GetDirectLightingTerminator(surface, wi);
        if (terminator <= 0.0)
            continue;

        float q = lightSelectionPdf * max(samplePdf, isDelta ? 1.0 : 1e-6);
        if (q <= 0.0)
            continue;

        float mis = isDelta || IsAnalyticAreaLight(light) == false ? 1.0 : PowerHeuristic(q, BSDF_PDF(surface, frame, set, wo, wi, BXDF_FLAG_ALL));
        float targetScalar = dot(fTotal * radiance, float3(0.2126, 0.7152, 0.0722)) * cosTheta * terminator * mis;
        if (targetScalar <= 0.0)
            continue;

        float w = targetScalar / q;
        wSum += w;

        float selector = UintToFloat01(Hash(pathSample.pixelSeed ^ pathSample.sampleIndex ^ (bounce * 0x85EBCA6Bu) ^ (i * 0x27D4EB2Fu) ^ 0xB5297A4Du));
        if (selector * wSum <= w)
        {
            hasSelection = true;
            selWi = wi;
            selMaxDistance = maxDistance;
            selSurfaceF = sF;
            selReflectionF = rF;
            selTransmissionF = tF;
            selRadiance = radiance;
            selCos = cosTheta;
            selTerminator = terminator;
            selMis = mis;
            selTarget = targetScalar;
            selIsDelta = isDelta;
            selIsDirectional = isDirectional;
        }
    }

    if (!hasSelection || wSum <= 0.0 || selTarget <= 0.0)
        return;

    Ray shadowRay = SpawnRayFromSurface(surface.interaction, selWi);
    shadowRay.direction = selWi;
    shadowRay.maxDistance = max(selMaxDistance - _SurfaceBias * 2.0, EPSILON);

    bool allowRefractiveTraversal = selIsDelta && selIsDirectional;
    bool writeShadowGuide = shadowChannel == 0u && selIsDelta && bounce == 0u && _LightCount == 1;
    uint shadowSeed = GetBounceLightSeed(pathSample, bounce);
    float3 transmittedWi;
    float3 sourceAirOrigin;
    float3 shadowTransmission = TraceShadowTransmission(pixel, shadowRay, allowRefractiveTraversal, writeShadowGuide, shadowSeed, transmittedWi, sourceAirOrigin);
#if defined(LUCENT_PATH_KERNEL_DIRECT_LIGHTING)
    if (selIsDelta)
    {
        float shadowVisibility = EncodeShadowVisibility(1.0 - saturate(Max3(shadowTransmission)));
        if (shadowChannel == 1u && !ShadowVisibilitySampled(_FrameShadowSum[pixel].g))
            SetFrameShadowChannel(pixel, 1u, shadowVisibility);
        else if (shadowChannel == 2u && !ShadowVisibilitySampled(_FrameShadowSum[pixel].b))
            SetFrameShadowChannel(pixel, 2u, shadowVisibility);
    }
#endif
    if (Max3(shadowTransmission) <= 1e-6)
        return;

    float risWeight = wSum / (float)candidateCount;
    float3 commonScale = selRadiance * _DirectIntensity * selCos * selTerminator * selMis * shadowTransmission * (risWeight / selTarget);
    surfaceContribution = selSurfaceF * commonScale;
    reflectionContribution = selReflectionF * commonScale;
    transmissionContribution = selTransmissionF * commonScale;
}

#if !defined(LUCENT_PATH_SHADOW_DIELECTRIC_TRACERAY)
bool TraceSurfelClosestHit(float3 origin, float3 dir, out uint instanceId, out float hitT)
{
    instanceId = 0u;
    hitT = 0.0;
    RayDesc rayDesc;
    rayDesc.Origin = origin;
    rayDesc.Direction = SafeNormalize(dir, float3(0.0, 1.0, 0.0));
    rayDesc.TMin = max(_SurfaceBias, EPSILON);
    rayDesc.TMax = max(_BounceMaxDistance, EPSILON);

    RayQuery<RAY_FLAG_SKIP_PROCEDURAL_PRIMITIVES> rayQuery;
    rayQuery.TraceRayInline(_RayTracingAccelerationStructure, RAY_FLAG_SKIP_PROCEDURAL_PRIMITIVES, 0xFF, rayDesc);
    rayQuery.Proceed();
    if (rayQuery.CommittedStatus() != COMMITTED_TRIANGLE_HIT)
        return false;
    instanceId = rayQuery.CommittedInstanceID();
    hitT = rayQuery.CommittedRayT();
    return true;
}
#endif

void EstimateSurfelDirectLighting(
    uint2 pixel,
    SurfaceData surface,
    ShadingFrame frame,
    LobeSet set,
    float3 wo,
    PathSampler pathSample,
    uint bounce,
    out float3 surfaceContribution,
    out float3 reflectionContribution,
    out float3 transmissionContribution)
{
    surfaceContribution = 0.0;
    reflectionContribution = 0.0;
    transmissionContribution = 0.0;

#if !defined(LUCENT_PATH_SHADOW_DIELECTRIC_TRACERAY)
    if (_EmissiveSurfelNeeEnabled == 0 || SelectableLobeCount(set) == 0u)
        return;

    uint candHash = Hash(pathSample.pixelSeed ^ pathSample.sampleIndex ^ (bounce * 0xA7F1E9C3u) ^ 0x9E3779B9u);
    float uSelect = UintToFloat01(candHash);
    float2 uCone = float2(UintToFloat01(Hash(candHash)), UintToFloat01(Hash(candHash ^ 0x68E31DA4u)));

    float3 dir;
    float guidePdf;
    if (!SampleSurfelGuideDirection(surface.position, uSelect, uCone, dir, guidePdf))
        return;

    float cosTheta = abs(dot(surface.normal, dir));
    float terminator = GetDirectLightingTerminator(surface, dir);
    if (cosTheta <= 0.0 || terminator <= 0.0)
        return;

    float3 sF;
    float3 rF;
    float3 tF;
    BSDF_f_AllSignals(surface, frame, set, wo, dir, TRANSPORT_MODE_RADIANCE, sF, rF, tF);
    if (Max3(sF + rF + tF) <= 1e-8)
        return;

    Ray surfelRay = SpawnRayFromSurface(surface.interaction, dir);
    uint hitInstance;
    float hitT;
    if (!TraceSurfelClosestHit(surfelRay.origin, dir, hitInstance, hitT))
        return;

    float3 radiance = SurfelGuideEmissionForInstance(surface.position, hitInstance);
    if (Max3(radiance) <= 1e-8)
        return;

    float bsdfPdf = BSDF_PDF(surface, frame, set, wo, dir, BXDF_FLAG_ALL);
    float mis = PowerHeuristic(guidePdf, bsdfPdf);
    float3 base = radiance * cosTheta * terminator * mis / max(guidePdf, 1e-6);
    surfaceContribution = sF * base;
    reflectionContribution = rF * base;
    transmissionContribution = tF * base;
#endif
}

void EstimateDistantDirectLighting(
    uint2 pixel,
    SurfaceData surface,
    ShadingFrame frame,
    LobeSet set,
    float3 wo,
    PathSampler pathSample,
    uint bounce,
    uint shadowChannel,
    out float3 surfaceContribution,
    out float3 reflectionContribution,
    out float3 transmissionContribution)
{
    surfaceContribution = 0.0;
    reflectionContribution = 0.0;
    transmissionContribution = 0.0;

    if (_DistantLightCount <= 0 || SelectableLobeCount(set) == 0u)
    {
        return;
    }

    uint distantLightCount = (uint)_DistantLightCount;
    uint distantLightSlot = 0u;

    if (distantLightCount > 1u)
    {
        uint pickHash = Hash(pathSample.pixelSeed ^ pathSample.sampleIndex ^ (bounce * 0x9E3779B1u) ^ 0x3C6EF372u);
        distantLightSlot = min((uint)(UintToFloat01(pickHash) * (float)distantLightCount), distantLightCount - 1u);
    }

    GpuLight light = _Lights[_DistantLightIndices[distantLightSlot]];
    float3 wi;
    float maxDistance;
    float3 radiance;
    float samplePdf;
    bool isDelta;

    if (GetDirectLightSample(light, surface, float2(0.0, 0.0), wi, maxDistance, radiance, samplePdf, isDelta) == false)
    {
        return;
    }

    Ray shadowRay = SpawnRayFromSurface(surface.interaction, wi);
    shadowRay.direction = wi;
    shadowRay.maxDistance = max(maxDistance - _SurfaceBias * 2.0, EPSILON);

    bool isWritingShadowGuide = shadowChannel == 0u && bounce == 0u && _LightCount == 1;
    uint shadowSeed = GetBounceLightSeed(pathSample, bounce) ^ 0x2545F491u;
    float3 transmittedWi;
    float3 sourceAirOrigin;
    float3 shadowTransmission = TraceShadowTransmission(pixel, shadowRay, true, isWritingShadowGuide, shadowSeed, transmittedWi, sourceAirOrigin);
#if defined(LUCENT_PATH_KERNEL_DIRECT_LIGHTING)
    float shadowVisibility = EncodeShadowVisibility(1.0 - saturate(Max3(shadowTransmission)));

    if (shadowChannel == 1u && ShadowVisibilitySampled(_FrameShadowSum[pixel].g) == false)
    {
        SetFrameShadowChannel(pixel, 1u, shadowVisibility);
    }
    else if (shadowChannel == 2u && ShadowVisibilitySampled(_FrameShadowSum[pixel].b) == false)
    {
        SetFrameShadowChannel(pixel, 2u, shadowVisibility);
    }
#endif

    if (Max3(shadowTransmission) <= 1e-6)
    {
        return;
    }

    float cosTheta = abs(dot(surface.normal, transmittedWi));

    if (cosTheta <= 0.0)
    {
        return;
    }

    float3 surfaceF;
    float3 reflectionF;
    float3 transmissionF;
    BSDF_f_AllSignals(surface, frame, set, wo, transmittedWi, TRANSPORT_MODE_RADIANCE, surfaceF, reflectionF, transmissionF);
    float3 commonScale = radiance * _DirectIntensity * cosTheta * shadowTransmission * (float)distantLightCount;
    surfaceContribution = surfaceF * commonScale;
    reflectionContribution = reflectionF * commonScale;
    transmissionContribution = transmissionF * commonScale;
}

float3 EstimateDirectLightingWithBSDF(uint2 pixel, SurfaceData surface, ShadingFrame frame, LobeSet set, float3 wo, PathSampler pathSample, uint bounce, uint shadowChannel)
{
    float3 result = 0.0;

#if !defined(LUCENT_NEE_SINGLE_ANALYTIC)
    if (_LightNeeRisCandidates > 1)
    {
        float3 risSurface;
        float3 risReflection;
        float3 risTransmission;
        EstimateAnalyticDirectLightingRis(pixel, surface, frame, set, wo, pathSample, bounce, shadowChannel, risSurface, risReflection, risTransmission);
        result += risSurface + risReflection + risTransmission;
    }
    else
#endif
    {
        DirectLightingContext ctx = PrepareDirectLightingContextWithBSDF(pixel, surface, set, pathSample, bounce, shadowChannel);
        if (ctx.valid)
        {
            float3 f;
            float bsdfPdf;
            if (ctx.isDelta)
            {
                f = BSDF_f(surface, frame, set, wo, ctx.wi, TRANSPORT_MODE_RADIANCE, BXDF_FLAG_ALL);
                bsdfPdf = 0.0;
            }
            else
            {
                BSDF_f_and_PDF(surface, frame, set, wo, ctx.wi, TRANSPORT_MODE_RADIANCE, BXDF_FLAG_ALL, f, bsdfPdf);
            }
            if (Max3(f) > 1e-8)
                result += EvaluateDirectLightingRadiance(ctx, wo, f, bsdfPdf);
        }
    }

    float3 distantSurface;
    float3 distantReflection;
    float3 distantTransmission;
    EstimateDistantDirectLighting(pixel, surface, frame, set, wo, pathSample, bounce, shadowChannel, distantSurface, distantReflection, distantTransmission);
    result += distantSurface + distantReflection + distantTransmission;

    bool envActive = _EnvNeeEnabled != 0 && SelectableLobeCount(set) > 0u && _EnvSamplingReady != 0 && _SkyObjectLighting != 0;
    if (envActive)
    {
        float envU = GetBounceRisAuxSample(pathSample, bounce);
        uint envJitterHash = Hash(pathSample.pixelSeed ^ pathSample.sampleIndex ^ (bounce * 0xA7F1E9C3u) ^ 0x5AD9BC1Du);
        float2 envJitter = float2(UintToFloat01(envJitterHash), UintToFloat01(Hash(envJitterHash)));
        float3 envDir;
        float3 envRadiance;
        float envPdf;
        if (SampleEnvironmentImportance(envU, envJitter, envDir, envRadiance, envPdf))
        {
            float cosTheta = abs(dot(surface.normal, envDir));
            float terminator = GetDirectLightingTerminator(surface, envDir);
            if (cosTheta > 0.0 && terminator > 0.0)
            {
                float3 envF = BSDF_f(surface, frame, set, wo, envDir, TRANSPORT_MODE_RADIANCE, BXDF_FLAG_ALL);
                if (Max3(envF) > 1e-8 && Max3(envRadiance) > 1e-8)
                {
                    Ray envShadow = SpawnRayFromSurface(surface.interaction, envDir);
                    envShadow.maxDistance = _BounceMaxDistance;
                    uint envShadowSeed = GetBounceLightSeed(pathSample, bounce);
                    float3 transmittedWi;
                    float3 sourceAirOrigin;
                    float3 envTransmission = TraceShadowTransmission(pixel, envShadow, false, false, envShadowSeed, transmittedWi, sourceAirOrigin);
                    if (Max3(envTransmission) > 1e-6)
                    {
                        float envBsdfPdf = BSDF_PDF(surface, frame, set, wo, envDir, BXDF_FLAG_ALL);
                        float envMis = PowerHeuristic(envPdf, envBsdfPdf);
                        result += envF * envRadiance * cosTheta * envTransmission * terminator * envMis / max(envPdf, 1e-6);
                    }
                }
            }
        }
    }

    float3 surfelSurface;
    float3 surfelReflection;
    float3 surfelTransmission;
    EstimateSurfelDirectLighting(pixel, surface, frame, set, wo, pathSample, bounce, surfelSurface, surfelReflection, surfelTransmission);
    result += surfelSurface + surfelReflection + surfelTransmission;

    return result;
}

float3 EstimateDirectLighting(uint2 pixel, SurfaceData surface, float3 wo, PathSampler pathSample, uint bounce)
{
    ShadingFrame frame = ComputeShadingFrame(surface);
    OrientShadingFrameForView(frame, wo);
    return EstimateDirectLightingWithBSDF(pixel, surface, frame, ComputeLobeSet(surface), wo, pathSample, bounce, 0u);
}

void EstimateDirectLightingSplitWithBSDF(
    uint2 pixel,
    SurfaceData surface,
    ShadingFrame frame,
    LobeSet set,
    float3 wo,
    PathSampler pathSample,
    uint bounce,
    out float3 surfaceContribution,
    out float3 reflectionContribution,
    out float3 transmissionContribution)
{
    surfaceContribution = 0.0;
    reflectionContribution = 0.0;
    transmissionContribution = 0.0;

#if !defined(LUCENT_NEE_SINGLE_ANALYTIC)
    if (_LightNeeRisCandidates > 1)
    {
        float3 risSurface;
        float3 risReflection;
        float3 risTransmission;
        EstimateAnalyticDirectLightingRis(pixel, surface, frame, set, wo, pathSample, bounce, 0u, risSurface, risReflection, risTransmission);
        surfaceContribution += risSurface;
        reflectionContribution += risReflection;
        transmissionContribution += risTransmission;
    }
    else
#endif
    {
        DirectLightingContext ctx = PrepareDirectLightingContextWithBSDF(pixel, surface, set, pathSample, bounce, 0u);
        if (ctx.valid)
        {
            float3 surfaceF;
            float3 reflectionF;
            float3 transmissionF;
            BSDF_f_AllSignals(surface, frame, set, wo, ctx.wi, TRANSPORT_MODE_RADIANCE, surfaceF, reflectionF, transmissionF);

            float bsdfPdf = ctx.isDelta ? 0.0 : BSDF_PDF(surface, frame, set, wo, ctx.wi, BXDF_FLAG_ALL);
            surfaceContribution += EvaluateDirectLightingRadiance(ctx, wo, surfaceF, bsdfPdf);
            reflectionContribution += EvaluateDirectLightingRadiance(ctx, wo, reflectionF, bsdfPdf);
            transmissionContribution += EvaluateDirectLightingRadiance(ctx, wo, transmissionF, bsdfPdf);
        }
    }

    float3 distantSurface;
    float3 distantReflection;
    float3 distantTransmission;
    EstimateDistantDirectLighting(pixel, surface, frame, set, wo, pathSample, bounce, 0u, distantSurface, distantReflection, distantTransmission);
    surfaceContribution += distantSurface;
    reflectionContribution += distantReflection;
    transmissionContribution += distantTransmission;

    bool envActive = _EnvNeeEnabled != 0 && SelectableLobeCount(set) > 0u && _EnvSamplingReady != 0 && _SkyObjectLighting != 0;
    if (envActive)
    {
        float envU = GetBounceRisAuxSample(pathSample, bounce);
        uint envJitterHash = Hash(pathSample.pixelSeed ^ pathSample.sampleIndex ^ (bounce * 0xA7F1E9C3u) ^ 0x5AD9BC1Du);
        float2 envJitter = float2(UintToFloat01(envJitterHash), UintToFloat01(Hash(envJitterHash)));
        float3 envDir;
        float3 envRadiance;
        float envPdf;
        if (SampleEnvironmentImportance(envU, envJitter, envDir, envRadiance, envPdf))
        {
            float cosTheta = abs(dot(surface.normal, envDir));
            float terminator = GetDirectLightingTerminator(surface, envDir);
            if (cosTheta > 0.0 && terminator > 0.0 && Max3(envRadiance) > 1e-8)
            {
                float3 envSurfaceF;
                float3 envReflectionF;
                float3 envTransmissionF;
                BSDF_f_AllSignals(surface, frame, set, wo, envDir, TRANSPORT_MODE_RADIANCE, envSurfaceF, envReflectionF, envTransmissionF);
                float3 envFTotal = envSurfaceF + envReflectionF + envTransmissionF;
                if (Max3(envFTotal) > 1e-8)
                {
                    Ray envShadow = SpawnRayFromSurface(surface.interaction, envDir);
                    envShadow.maxDistance = _BounceMaxDistance;
                    uint envShadowSeed = GetBounceLightSeed(pathSample, bounce);
                    float3 transmittedWi;
                    float3 sourceAirOrigin;
                    float3 envTransmission = TraceShadowTransmission(pixel, envShadow, false, false, envShadowSeed, transmittedWi, sourceAirOrigin);
                    if (Max3(envTransmission) > 1e-6)
                    {
                        float envBsdfPdf = BSDF_PDF(surface, frame, set, wo, envDir, BXDF_FLAG_ALL);
                        float envMis = PowerHeuristic(envPdf, envBsdfPdf);
                        float3 envBase = envRadiance * cosTheta * envTransmission * terminator * envMis / max(envPdf, 1e-6);
                        surfaceContribution += envSurfaceF * envBase;
                        reflectionContribution += envReflectionF * envBase;
                        transmissionContribution += envTransmissionF * envBase;
                    }
                }
            }
        }
    }

    float3 surfelSurface;
    float3 surfelReflection;
    float3 surfelTransmission;
    EstimateSurfelDirectLighting(pixel, surface, frame, set, wo, pathSample, bounce, surfelSurface, surfelReflection, surfelTransmission);
    surfaceContribution += surfelSurface;
    reflectionContribution += surfelReflection;
    transmissionContribution += surfelTransmission;
}

void EstimateDirectLightingSplit(
    uint2 pixel,
    SurfaceData surface,
    float3 wo,
    PathSampler pathSample,
    uint bounce,
    out float3 surfaceContribution,
    out float3 reflectionContribution,
    out float3 transmissionContribution)
{
    ShadingFrame frame = ComputeShadingFrame(surface);
    OrientShadingFrameForView(frame, wo);
    EstimateDirectLightingSplitWithBSDF(pixel, surface, frame, ComputeLobeSet(surface), wo, pathSample, bounce, surfaceContribution, reflectionContribution, transmissionContribution);
}

#endif
