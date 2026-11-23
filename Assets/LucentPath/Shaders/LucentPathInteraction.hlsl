#ifndef LUCENT_PATH_INTERACTION_INCLUDED
#define LUCENT_PATH_INTERACTION_INCLUDED

struct SurfaceShadingGeometry
{
    float3 n;
    float3 dpdu;
    float3 dpdv;
    float3 dndu;
    float3 dndv;
};

struct SurfaceInteraction
{
    float3 p;
    float3 pError;
    float3 wo;
    float2 uv;
    float3 n;
    float3 dpdu;
    float3 dpdv;
    float3 dndu;
    float3 dndv;
    SurfaceShadingGeometry shading;
    int materialIndex;
    int triIndex;
    int instanceIndex;
};

float3 FaceForwardTo(float3 normal, float3 reference)
{
    return dot(normal, reference) < 0.0 ? -normal : normal;
}

float3 GetSurfaceOffsetNormal(SurfaceInteraction si, float3 direction)
{
    return dot(si.n, direction) >= 0.0 ? si.n : -si.n;
}

float ComputeSurfaceOffsetDistance(SurfaceInteraction si)
{
    return max(_SurfaceBias, Max3(si.pError));
}

float3 OffsetRayOriginFromSurface(SurfaceInteraction si, float3 direction)
{
    return si.p + GetSurfaceOffsetNormal(si, direction) * ComputeSurfaceOffsetDistance(si);
}

Ray SpawnRayFromSurface(SurfaceInteraction si, float3 direction)
{
    Ray ray;
    ray.direction = SafeNormalize(direction, si.shading.n);
    ray.origin = OffsetRayOriginFromSurface(si, ray.direction);
    ray.maxDistance = max(_BounceMaxDistance, EPSILON);
    return ray;
}

#endif
