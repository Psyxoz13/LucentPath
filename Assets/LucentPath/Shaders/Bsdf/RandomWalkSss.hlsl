#ifndef LUCENT_PATH_RANDOM_WALK_SSS_INCLUDED
#define LUCENT_PATH_RANDOM_WALK_SSS_INCLUDED

#include "BsdfSubsurface.hlsl"

struct RandomWalkResult
{
    bool exited;
    float3 exitPosition;
    float3 exitNormal;
    float3 exitDirection;
    float3 throughput;
    uint stepCount;
    float traveledDistance;
    uint transportMode;
};

#define SSS_TRANSPORT_MODE_SOLID_RANDOM_WALK 0u
#define SSS_TRANSPORT_MODE_THIN_BTDF 1u

#if defined(LUCENT_DXR_ONLY)
#if defined(LUCENT_PATH_SHADOW_DIELECTRIC_TRACERAY)
bool RandomWalkTraceMedium(float3 origin, float3 direction, float maxDistance, uint entryInstanceId, out float hitDistance)
{
    hitDistance = maxDistance;

    RayDesc ray;
    ray.Origin = origin;
    ray.Direction = direction;
    ray.TMin = max(_SurfaceBias, EPSILON);
    ray.TMax = max(maxDistance, EPSILON);

    RayPayload payload;
    InitRayPayloadMiss(payload, ray.TMax, 1u);

    TraceRay(_RayTracingAccelerationStructure, RAY_FLAG_SKIP_PROCEDURAL_PRIMITIVES, 0xFF, 0, 1, 0, ray, payload);

    if (GetRayPayloadHit(payload) == 0u)
        return false;

    uint hitInstanceId = GetRayPayloadInstanceId(payload);
    if (hitInstanceId != entryInstanceId)
        return false;

    hitDistance = GetRayPayloadHitDistance(payload);
    return true;
}
#else
bool RandomWalkTraceMedium(float3 origin, float3 direction, float maxDistance, uint entryInstanceId, out float hitDistance)
{
    hitDistance = maxDistance;

    RayDesc ray;
    ray.Origin = origin;
    ray.Direction = direction;
    ray.TMin = max(_SurfaceBias, EPSILON);
    ray.TMax = max(maxDistance, EPSILON);

    RayQuery<RAY_FLAG_SKIP_PROCEDURAL_PRIMITIVES> rayQuery;
    rayQuery.TraceRayInline(_RayTracingAccelerationStructure, RAY_FLAG_SKIP_PROCEDURAL_PRIMITIVES, 0xFF, ray);
    while (rayQuery.Proceed())
    {
        if (rayQuery.CandidateType() == CANDIDATE_NON_OPAQUE_TRIANGLE)
            rayQuery.CommitNonOpaqueTriangleHit();
    }
    if (rayQuery.CommittedStatus() != COMMITTED_TRIANGLE_HIT)
        return false;

    uint hitInstanceId = rayQuery.CommittedInstanceID();
    if (hitInstanceId != entryInstanceId)
        return false;

    hitDistance = rayQuery.CommittedRayT();
    return true;
}
#endif
#else
bool RandomWalkTraceMedium(float3 origin, float3 direction, float maxDistance, uint entryInstanceId, out float hitDistance)
{
    hitDistance = maxDistance;
    return false;
}
#endif

RandomWalkResult CreateEmptyRandomWalkResult(float3 entryPosition, float3 entryDirection)
{
    RandomWalkResult result;
    result.exited = false;
    result.exitPosition = entryPosition;
    result.exitNormal = float3(0.0, 1.0, 0.0);
    result.exitDirection = entryDirection;
    result.throughput = float3(0.0, 0.0, 0.0);
    result.stepCount = 0u;
    result.traveledDistance = 0.0;
    result.transportMode = SSS_TRANSPORT_MODE_SOLID_RANDOM_WALK;
    return result;
}

bool ExecuteThinSubsurfaceBtdf(
    SubsurfaceMedium medium,
    float3 entryPosition,
    float3 entryDirection,
    uint entryInstanceId,
    float maxProbeDistance,
    float2 exitDirectionSample,
    out RandomWalkResult result)
{
    float3 direction = SafeNormalize(entryDirection, float3(0.0, -1.0, 0.0));
    result = CreateEmptyRandomWalkResult(entryPosition, direction);

#if !defined(LUCENT_DXR_ONLY)
    return false;
#else

    float thinLimit = max(ThinSubsurfaceDistanceLimit(medium), EPSILON);
    float probeDistance = max(maxProbeDistance, thinLimit);
    float hitDistance;
    bool hit = RandomWalkTraceMedium(entryPosition, direction, probeDistance, entryInstanceId, hitDistance);
    if (hit && hitDistance > thinLimit)
        return false;

    float thickness = hit ? hitDistance : 0.0;
    float3 transmittance = EvaluateThinSubsurfaceTransmission(medium, thickness);
    if (Max3(transmittance) <= 1e-6)
        return false;

    float exitOffsetDistance = hit ? hitDistance : EPSILON;
    float3 exitNormal = SafeNormalize(direction, float3(0.0, 1.0, 0.0));

    result.exited = true;
    result.exitPosition = entryPosition + direction * max(exitOffsetDistance, EPSILON);
    result.exitNormal = exitNormal;
    result.exitDirection = SampleCosineHemisphere(exitNormal, exitDirectionSample);
    result.throughput = transmittance;
    result.stepCount = 0u;
    result.traveledDistance = thickness;
    result.transportMode = SSS_TRANSPORT_MODE_THIN_BTDF;
    return true;
#endif
}

RandomWalkResult ExecuteDiffusionSss(
    float3 color,
    float3 effectiveRadius,
    float3 entryPosition,
    float3 entryGeomNormal,
    uint entryInstanceId,
    inout uint rngState)
{
    RandomWalkResult result = CreateEmptyRandomWalkResult(entryPosition, entryGeomNormal);

    float3 d = float3(
        BurleyScatterDistance(color.x, effectiveRadius.x),
        BurleyScatterDistance(color.y, effectiveRadius.y),
        BurleyScatterDistance(color.z, effectiveRadius.z));
    d = max(d, float3(1e-6, 1e-6, 1e-6));

    uint channelHash = Hash(rngState ^ 0x9E3779B1u);
    rngState = channelHash;
    uint channel = channelHash % 3u;
    float dChannel = d[channel];

    uint radiusHash = Hash(rngState ^ 0x68E31DA4u);
    rngState = radiusHash;
    float radius = BurleySampleRadius(UintToFloat01(radiusHash), dChannel);

    float maxRadius = 35.0 * dChannel;
    if (radius <= 1e-5 || radius >= maxRadius)
    {
        result.throughput = float3(0.0, 0.0, 0.0);
        return result;
    }

    uint phiHash = Hash(rngState ^ 0xB5297A4Du);
    rngState = phiHash;
    float phi = 2.0 * SSS_PI * UintToFloat01(phiHash);

    float3 axisN = SafeNormalize(entryGeomNormal, float3(0.0, 1.0, 0.0));
    float3 axisT;
    float3 axisB;
    BuildBasis(axisN, axisT, axisB);

    float3 diskOffset = (cos(phi) * axisT + sin(phi) * axisB) * radius;
    float probeHalf = sqrt(max(maxRadius * maxRadius - radius * radius, 1e-8));
    float3 probeOrigin = entryPosition + diskOffset + axisN * probeHalf;
    float3 probeDir = -axisN;
    float probeLength = 2.0 * probeHalf;

    float hitDistance;
    bool hit = RandomWalkTraceMedium(probeOrigin, probeDir, probeLength, entryInstanceId, hitDistance);
    if (!hit)
    {
        result.throughput = float3(0.0, 0.0, 0.0);
        return result;
    }

    float3 radialPdf = float3(
        BurleyRadialPdf(radius, d.x),
        BurleyRadialPdf(radius, d.y),
        BurleyRadialPdf(radius, d.z));
    float pdfSum = radialPdf.x + radialPdf.y + radialPdf.z;
    if (pdfSum <= 1e-8)
    {
        result.throughput = float3(0.0, 0.0, 0.0);
        return result;
    }

    float3 throughput = 3.0 * saturate(color) * radialPdf / pdfSum;

    uint dirHash0 = Hash(rngState ^ 0x238E1F29u);
    uint dirHash1 = Hash(dirHash0 ^ 0x9E3779B1u);
    rngState = dirHash1;
    float2 exitDirSample = float2(UintToFloat01(dirHash0), UintToFloat01(dirHash1));

    result.exited = true;
    result.exitPosition = probeOrigin + probeDir * hitDistance;
    result.exitNormal = axisN;
    result.exitDirection = SampleCosineHemisphere(axisN, exitDirSample);
    result.throughput = throughput;
    result.stepCount = 1u;
    result.traveledDistance = radius;
    result.transportMode = SSS_TRANSPORT_MODE_SOLID_RANDOM_WALK;
    return result;
}

#endif
