#ifndef DXR_LUCENT_PATH_PAYLOAD_INCLUDED
#define DXR_LUCENT_PATH_PAYLOAD_INCLUDED

#include "Assets/LucentPath/Shaders/LucentPathSurfaceHit.hlsl"

#define LUCENT_PAYLOAD_TWO_PI 6.28318530717958647692

void LucentPayloadBuildBasis(float3 n, out float3 t, out float3 b)
{
    float s = n.z >= 0.0 ? 1.0 : -1.0;
    float a = -1.0 / (s + n.z);
    float bb = n.x * n.y * a;
    t = float3(1.0 + s * n.x * n.x * a, s * bb, -s * n.x);
    b = float3(bb, s + n.y * n.y * a, -n.y);
}

// Packed hit transport (36 B). Not stored: world position is reconstructed as
// ray.origin + ray.dir * t, and prevPosition as position + motionDelta.
struct RayPayload
{
    uint g0; // hit distance t                            : fp32 (asuint)
    uint g1; // shading normal                            : octahedral 16|16
    uint g2; // geom normal                               : octahedral 16|16
    uint g3; // uv.x                                      : fp32 (asuint)
    uint g4; // uv.y                                      : fp32 (asuint)
    uint g5; // instanceId 0-19 (20b) | materialIndex 20-31 (12b)
    uint g6; // alpha 0-7 (unorm8) | geometryIndex 8-15 (8b) | flags 16-21 (6b) | materialModel 22-24 (3b) | hit 25 | rayType 26 | tangentSign 27 | spare 28-31
    uint g7; // motionDelta.x 0-15 (fp16) | motionDelta.y 16-31 (fp16)
    uint g8; // motionDelta.z 0-15 (fp16) | tangent azimuth 16-31 (unorm16)
};

struct AttributeData
{
    float2 barycentrics : SV_Barycentrics;
};

uint GetRayPayloadHit(RayPayload payload)
{
    return (payload.g6 >> 25u) & 1u;
}

uint GetRayPayloadRayType(RayPayload payload)
{
    return (payload.g6 >> 26u) & 1u;
}

float GetRayPayloadHitDistance(RayPayload payload)
{
    return asfloat(payload.g0);
}

uint GetRayPayloadInstanceId(RayPayload payload)
{
    return payload.g5 & 0xFFFFFu;
}

uint GetRayPayloadGeometryIndex(RayPayload payload)
{
    return (payload.g6 >> 8u) & 0xFFu;
}

void SetRayPayloadGeometryIndex(inout RayPayload payload, uint geometryIndex)
{
    payload.g6 = (payload.g6 & ~(0xFFu << 8u)) | ((geometryIndex & 0xFFu) << 8u);
}

void InitRayPayloadMiss(inout RayPayload payload, float hitDistance, uint rayType)
{
    payload.g0 = asuint(hitDistance);
    payload.g1 = 0u;
    payload.g2 = 0u;
    payload.g3 = 0u;
    payload.g4 = 0u;
    payload.g5 = 0u;
    payload.g6 = (rayType & 1u) << 26u;
    payload.g7 = 0u;
    payload.g8 = 0u;
}

void MarkRayPayloadMiss(inout RayPayload payload)
{
    payload.g6 &= ~(1u << 25u);
    payload.g0 = asuint(0.0);
}

void PackHitPayload(inout RayPayload payload, GpuSurfaceHit hit)
{
    uint rayType = GetRayPayloadRayType(payload);

    float3 shadingNormal = GetSurfaceHitShadingNormal(hit);
    float3 geomNormal = GetSurfaceHitGeomNormal(hit);
    float3 tangent = GetSurfaceHitTangent(hit);
    float3 position = GetSurfaceHitPosition(hit);

    float alpha;
    float tangentSign;
    _LucentUnpackAlphaTangentSign(asuint(hit.positionPacked.w), alpha, tangentSign);

    uint flags = GetSurfaceHitFlags(hit);
    uint materialModel = GetSurfaceHitMaterialModel(hit);

    float3 motionDelta = hit.prevPosition - position;

    float3 basisT;
    float3 basisB;
    LucentPayloadBuildBasis(shadingNormal, basisT, basisB);
    float azimuth = atan2(dot(tangent, basisB), dot(tangent, basisT));
    uint azimuth16 = uint(saturate(azimuth / LUCENT_PAYLOAD_TWO_PI + 0.5) * 65535.0 + 0.5);

    uint alpha8 = uint(saturate(alpha) * 255.0 + 0.5);
    uint signBit = tangentSign < 0.0 ? 1u : 0u;

    payload.g0 = asuint(hit.t);
    payload.g1 = _LucentPackOct32(shadingNormal);
    payload.g2 = _LucentPackOct32(geomNormal);
    payload.g3 = asuint(hit.uv.x);
    payload.g4 = asuint(hit.uv.y);
    payload.g5 = (hit.instanceId & 0xFFFFFu) | ((hit.materialIndex & 0xFFFu) << 20u);
    payload.g6 = (alpha8 & 0xFFu)
        | (payload.g6 & (0xFFu << 8u))
        | ((flags & 0x3Fu) << 16u)
        | ((materialModel & 0x7u) << 22u)
        | (1u << 25u)
        | ((rayType & 1u) << 26u)
        | (signBit << 27u);
    payload.g7 = (f32tof16(motionDelta.x) & 0xFFFFu) | ((f32tof16(motionDelta.y) & 0xFFFFu) << 16u);
    payload.g8 = (f32tof16(motionDelta.z) & 0xFFFFu) | ((azimuth16 & 0xFFFFu) << 16u);
}

GpuSurfaceHit UnpackHitPayload(RayPayload payload, float3 rayOrigin, float3 rayDirection)
{
    float t = asfloat(payload.g0);
    float3 shadingNormal = _LucentUnpackOct32(payload.g1);
    float3 geomNormal = _LucentUnpackOct32(payload.g2);
    float2 uv = float2(asfloat(payload.g3), asfloat(payload.g4));
    uint instanceId = payload.g5 & 0xFFFFFu;
    uint materialIndex = (payload.g5 >> 20u) & 0xFFFu;

    float alpha = float(payload.g6 & 0xFFu) / 255.0;
    uint flags = (payload.g6 >> 16u) & 0x3Fu;
    uint materialModel = (payload.g6 >> 22u) & 0x7u;
    float tangentSign = ((payload.g6 >> 27u) & 1u) != 0u ? -1.0 : 1.0;

    float3 motionDelta = float3(
        f16tof32(payload.g7 & 0xFFFFu),
        f16tof32((payload.g7 >> 16u) & 0xFFFFu),
        f16tof32(payload.g8 & 0xFFFFu));
    float azimuth = (float((payload.g8 >> 16u) & 0xFFFFu) / 65535.0 - 0.5) * LUCENT_PAYLOAD_TWO_PI;

    float3 position = rayOrigin + rayDirection * t;
    float3 prevPosition = position + motionDelta;

    float3 basisT;
    float3 basisB;
    LucentPayloadBuildBasis(shadingNormal, basisT, basisB);
    float3 tangent = cos(azimuth) * basisT + sin(azimuth) * basisB;

    GpuSurfaceHit hit;
    hit.t = t;
    hit.instanceId = instanceId;
    hit.materialIndex = materialIndex;
    hit.uv = uv;
    hit.prevPosition = prevPosition;
    SetSurfaceHitFlagsMaterial(hit, flags, materialModel);
    SetSurfaceHitPositionAndAlphaTangentSign(hit, position, alpha, tangentSign);
    SetSurfaceHitNormalsRoughAnisoTangent(hit, shadingNormal, 0.0, 0.0, geomNormal, tangent);
    return hit;
}

#endif
