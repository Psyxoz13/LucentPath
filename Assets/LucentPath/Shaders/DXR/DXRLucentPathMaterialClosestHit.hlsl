#ifndef DXR_LUCENT_PATH_MATERIAL_CLOSEST_HIT_INCLUDED
#define DXR_LUCENT_PATH_MATERIAL_CLOSEST_HIT_INCLUDED

#include "UnityRaytracingMeshUtils.cginc"
#include "Assets/LucentPath/Shaders/LucentPathCommon.hlsl"
#include "Assets/LucentPath/Shaders/DXR/DXRLucentPathPayload.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

struct LucentPathRayTracingVertex
{
    float3 positionOS;
    float3 normalOS;
    float4 tangentOS;
    float2 uv0;
};

float3 LucentPathRayTracingSafeNormalize(float3 value, float3 fallback)
{
    float lengthSq = dot(value, value);
    float3 normalized = value * rsqrt(max(lengthSq, 1e-20));
    return lerp(fallback, normalized, step(1e-20, lengthSq));
}

void FetchLucentPathRayTracingVertex(uint vertexIndex, out LucentPathRayTracingVertex vertex)
{
    vertex.positionOS = UnityRayTracingFetchVertexAttribute3(vertexIndex, kVertexAttributePosition);
    vertex.normalOS = UnityRayTracingFetchVertexAttribute3(vertexIndex, kVertexAttributeNormal);
    vertex.tangentOS = UnityRayTracingFetchVertexAttribute4(vertexIndex, kVertexAttributeTangent);
    vertex.uv0 = UnityRayTracingFetchVertexAttribute2(vertexIndex, kVertexAttributeTexCoord0);
}

float2 GetLucentPathRayTracingUv(AttributeData attributes)
{
    uint3 indices = UnityRayTracingFetchTriangleIndices(PrimitiveIndex());
    float3 barycentrics = float3(1.0 - attributes.barycentrics.x - attributes.barycentrics.y, attributes.barycentrics.x, attributes.barycentrics.y);
    float2 uv0 = UnityRayTracingFetchVertexAttribute2(indices.x, kVertexAttributeTexCoord0);
    float2 uv1 = UnityRayTracingFetchVertexAttribute2(indices.y, kVertexAttributeTexCoord0);
    float2 uv2 = UnityRayTracingFetchVertexAttribute2(indices.z, kVertexAttributeTexCoord0);
    return uv0 * barycentrics.x + uv1 * barycentrics.y + uv2 * barycentrics.z;
}

float GetLucentPathRayTracingBaseAlpha(AttributeData attributes)
{
    float2 uv = GetLucentPathRayTracingUv(attributes) * _BaseMap_ST.xy + _BaseMap_ST.zw;
    return (SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, uv, 0.0) * _BaseColor).a;
}

float3 GetLucentPathRayTracingWorldNormal(float3 normalOS, float3 worldRayDirection)
{
    return LucentPathRayTracingSafeNormalize(mul((float3x3)ObjectToWorld3x4(), normalOS), -worldRayDirection);
}

float3 GetLucentPathRayTracingWorldPosition(float3 positionOS)
{
    return mul(ObjectToWorld3x4(), float4(positionOS, 1.0)).xyz;
}

float3 GetLucentPathRayTracingWorldDirection(float3 directionOS, float3 fallback)
{
    return LucentPathRayTracingSafeNormalize(mul((float3x3)ObjectToWorld3x4(), directionOS), fallback);
}

float3 UnpackLucentPathRayTracingNormal(float4 packedNormal, float scale)
{
    return UnpackNormalScale(packedNormal, max(scale, 0.0));
}

float MaxLucentPathRayTracing3(float3 value)
{
    return max(value.x, max(value.y, value.z));
}

float3 GetLucentPathRayTracingObjectPosition(
    float3 position0OS,
    float3 position1OS,
    float3 position2OS,
    float3 barycentrics)
{
    return position0OS * barycentrics.x + position1OS * barycentrics.y + position2OS * barycentrics.z;
}

bool HasLucentPathPreviousObjectMotion()
{
#if defined(UNITY_PREV_MATRIX_M)
    return unity_MotionVectorsParams.y != 0.0 && unity_MotionVectorsParams.w != 0.0;
#else
    return false;
#endif
}

GpuSurfaceHit EvaluateLucentPathRayTracingSurface(AttributeData attributes, float3 rayDirection, float hitDistance, uint rayType)
{
    uint3 indices = UnityRayTracingFetchTriangleIndices(PrimitiveIndex());
    LucentPathRayTracingVertex v0, v1, v2;
    FetchLucentPathRayTracingVertex(indices.x, v0);
    FetchLucentPathRayTracingVertex(indices.y, v1);
    FetchLucentPathRayTracingVertex(indices.z, v2);

    float3 barycentrics = float3(1.0 - attributes.barycentrics.x - attributes.barycentrics.y, attributes.barycentrics.x, attributes.barycentrics.y);
    float3 objectHitPosition = GetLucentPathRayTracingObjectPosition(v0.positionOS, v1.positionOS, v2.positionOS, barycentrics);
    float3 normalOS = v0.normalOS * barycentrics.x + v1.normalOS * barycentrics.y + v2.normalOS * barycentrics.z;
    float4 tangentOS = v0.tangentOS * barycentrics.x + v1.tangentOS * barycentrics.y + v2.tangentOS * barycentrics.z;
    float2 uvRaw = v0.uv0 * barycentrics.x + v1.uv0 * barycentrics.y + v2.uv0 * barycentrics.z;

    float3 p0 = GetLucentPathRayTracingWorldPosition(v0.positionOS);
    float3 p1 = GetLucentPathRayTracingWorldPosition(v1.positionOS);
    float3 p2 = GetLucentPathRayTracingWorldPosition(v2.positionOS);
    float3 geomNormal = LucentPathRayTracingSafeNormalize(cross(p1 - p0, p2 - p0), GetLucentPathRayTracingWorldNormal(normalOS, rayDirection));
    bool frontFace = dot(geomNormal, rayDirection) < 0.0;
    if (!frontFace)
        geomNormal = -geomNormal;

    float3 shadingNormalBase = GetLucentPathRayTracingWorldNormal(normalOS, geomNormal);
    if (dot(shadingNormalBase, geomNormal) < 0.0)
        shadingNormalBase = -shadingNormalBase;

    float3 tangent = GetLucentPathRayTracingWorldDirection(tangentOS.xyz, abs(geomNormal.y) < 0.999 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0));
    tangent = LucentPathRayTracingSafeNormalize(tangent - shadingNormalBase * dot(shadingNormalBase, tangent), abs(shadingNormalBase.y) < 0.999 ? float3(1.0, 0.0, 0.0) : float3(0.0, 0.0, 1.0));
    float3 bitangent = LucentPathRayTracingSafeNormalize(cross(shadingNormalBase, tangent) * tangentOS.w, cross(shadingNormalBase, tangent));

    float3 worldHitPosition = WorldRayOrigin() + rayDirection * hitDistance;

#if defined(LUCENT_PATH_SURFACE_FROM_SCRATCH)
    float3 normal = shadingNormalBase;
#else
    float2 uvBase = uvRaw * _BaseMap_ST.xy + _BaseMap_ST.zw;
    float4 normalSample = SAMPLE_TEXTURE2D_LOD(_BumpMap, sampler_BumpMap, uvBase, 0.0);
    float3 normalTS = UnpackLucentPathRayTracingNormal(normalSample, _BumpScale);
    float3 normal = LucentPathRayTracingSafeNormalize(tangent * normalTS.x + bitangent * normalTS.y + shadingNormalBase * normalTS.z, shadingNormalBase);
    if (dot(normal, geomNormal) < 0.0)
        normal = -normal;
#endif

#if defined(LUCENT_PATH_OPENPBR_SHADER)
    uint materialModelInt = LUCENT_HIT_MATERIAL_OPENPBR;
    bool hasExplicitConductor = false;
    bool thinWalled = _OpenPbrGeometryThinWalled > 0.5;
#elif defined(LUCENT_PATH_SURFACE_FROM_SCRATCH)
    uint materialModelInt = LUCENT_HIT_MATERIAL_STANDARD;
    bool hasExplicitConductor = false;
    bool thinWalled = false;
#else
    uint materialModelInt = (uint)max(_MaterialModel + 0.5, 0.0);
    bool hasExplicitConductor = MaxLucentPathRayTracing3(abs(_K.rgb)) > 1e-5;
    bool thinWalled = false;
#endif

    uint flags = frontFace ? SURFACE_HIT_FLAG_FRONT_FACE : 0u;
    flags |= _AlphaClip > 0.5 ? SURFACE_HIT_FLAG_ALPHA_CLIP : 0u;
    flags |= hasExplicitConductor ? SURFACE_HIT_FLAG_EXPLICIT_CONDUCTOR : 0u;
    flags |= materialModelInt == LUCENT_HIT_MATERIAL_OPENPBR ? SURFACE_HIT_FLAG_OPENPBR : 0u;
    flags |= thinWalled ? SURFACE_HIT_FLAG_THIN_WALLED : 0u;

    GpuSurfaceHit surfaceHit = CreateEmptySurfaceHit();
    surfaceHit.t = hitDistance;
    surfaceHit.instanceId = InstanceID();
    surfaceHit.uv = uvRaw;
    surfaceHit.materialIndex = (uint)max(_LucentMaterialIndex, 0);

    if (HasLucentPathPreviousObjectMotion())
    {
        float3 prevWorldHitPosition = mul(UNITY_PREV_MATRIX_M, float4(objectHitPosition, 1.0)).xyz;
        surfaceHit.prevPosition = prevWorldHitPosition;
        flags |= SURFACE_HIT_FLAG_PREV_POSITION_VALID;
    }
    else
    {
        surfaceHit.prevPosition = worldHitPosition;
    }

    SetSurfaceHitFlagsMaterial(surfaceHit, flags, materialModelInt);
    SetSurfaceHitPositionAndAlphaTangentSign(surfaceHit, worldHitPosition, 1.0, tangentOS.w);
    SetSurfaceHitNormalsRoughAnisoTangent(surfaceHit, normal, 0.0, 0.0, geomNormal, tangent);

    return surfaceHit;
}

[shader("closesthit")]
void ClosestHitMain(inout RayPayload payload : SV_RayPayload, AttributeData attributes : SV_IntersectionAttributes)
{
    float3 rayDirection = LucentPathRayTracingSafeNormalize(WorldRayDirection(), float3(0.0, 0.0, 1.0));
    float hitDistance = RayTCurrent();
    GpuSurfaceHit surfaceHit = EvaluateLucentPathRayTracingSurface(attributes, rayDirection, hitDistance, GetRayPayloadRayType(payload));
    PackHitPayload(payload, surfaceHit);
    SetRayPayloadGeometryIndex(payload, GeometryIndex());
}

[shader("anyhit")]
void AnyHitMain(inout RayPayload payload : SV_RayPayload, AttributeData attributes : SV_IntersectionAttributes)
{
    if (_AlphaClip > 0.5 && GetLucentPathRayTracingBaseAlpha(attributes) < _Cutoff)
        IgnoreHit();
}

#endif
