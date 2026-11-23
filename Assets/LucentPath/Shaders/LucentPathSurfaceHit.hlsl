#ifndef LUCENT_PATH_SURFACE_HIT_INCLUDED
#define LUCENT_PATH_SURFACE_HIT_INCLUDED

#include "Assets/LucentPath/Shaders/LucentPathCommon.hlsl"
#include "Assets/LucentPath/Shaders/LucentPathHitPack.hlsl"

#define SURFACE_HIT_FLAG_FRONT_FACE 1u
#define SURFACE_HIT_FLAG_ALPHA_CLIP 2u
#define SURFACE_HIT_FLAG_EXPLICIT_CONDUCTOR 4u
#define SURFACE_HIT_FLAG_OPENPBR 8u
#define SURFACE_HIT_FLAG_THIN_WALLED 16u
#define SURFACE_HIT_FLAG_PREV_POSITION_VALID 32u

#define LUCENT_HIT_MATERIAL_STANDARD 0u
#define LUCENT_HIT_MATERIAL_DIFFUSE 1u
#define LUCENT_HIT_MATERIAL_CONDUCTOR 2u
#define LUCENT_HIT_MATERIAL_DIELECTRIC 3u
#define LUCENT_HIT_MATERIAL_THIN_DIELECTRIC 4u
#define LUCENT_HIT_MATERIAL_OPENPBR 5u

struct GpuSurfaceHit
{
    uint   flagsMaterial;
    float  t;
    uint   instanceId;

    float4 positionPacked;
    uint4  packedNormalsRoughAnisoTangent;

    float3 prevPosition;

    float2 uv;
    uint   materialIndex;
};

struct GpuStoredHit
{
    uint   flags;
    float  t;
    uint   materialModel;
    uint   instanceId;

    float4 positionPacked;
    uint4  packedNormalsRoughAnisoTangent;

    float2 uv;
    uint   materialIndex;
    uint   reserved;
};

GpuStoredHit PackStoredHit(GpuSurfaceHit hit)
{
    uint flags;
    uint materialModel;
    _LucentUnpackFlagsMaterial(hit.flagsMaterial, flags, materialModel);

    GpuStoredHit stored;
    stored.flags = flags;
    stored.t = hit.t;
    stored.materialModel = materialModel;
    stored.instanceId = hit.instanceId;
    stored.positionPacked = hit.positionPacked;
    stored.packedNormalsRoughAnisoTangent = hit.packedNormalsRoughAnisoTangent;
    stored.uv = hit.uv;
    stored.materialIndex = hit.materialIndex;
    stored.reserved = 0u;
    return stored;
}

GpuSurfaceHit UnpackStoredHit(GpuStoredHit stored)
{
    GpuSurfaceHit hit;
    hit.flagsMaterial = _LucentPackFlagsMaterial(stored.flags, stored.materialModel);
    hit.t = stored.t;
    hit.instanceId = stored.instanceId;
    hit.positionPacked = stored.positionPacked;
    hit.packedNormalsRoughAnisoTangent = stored.packedNormalsRoughAnisoTangent;
    hit.prevPosition = float3(0.0, 0.0, 0.0);
    hit.uv = stored.uv;
    hit.materialIndex = stored.materialIndex;
    return hit;
}

uint GetSurfaceHitFlags(GpuSurfaceHit hit)
{
    uint flags;
    uint materialModel;
    _LucentUnpackFlagsMaterial(hit.flagsMaterial, flags, materialModel);
    return flags;
}

uint GetSurfaceHitMaterialModel(GpuSurfaceHit hit)
{
    uint flags;
    uint materialModel;
    _LucentUnpackFlagsMaterial(hit.flagsMaterial, flags, materialModel);
    return materialModel;
}

void SetSurfaceHitFlagsMaterial(inout GpuSurfaceHit hit, uint flags, uint materialModel)
{
    hit.flagsMaterial = _LucentPackFlagsMaterial(flags, materialModel);
}

bool GetSurfaceHitPreviousPositionValid(GpuSurfaceHit hit)
{
    return (GetSurfaceHitFlags(hit) & SURFACE_HIT_FLAG_PREV_POSITION_VALID) != 0u;
}

float3 GetSurfaceHitPosition(GpuSurfaceHit hit)
{
    return hit.positionPacked.xyz;
}

float GetSurfaceHitTangentSign(GpuSurfaceHit hit)
{
    float alpha;
    float sign;
    _LucentUnpackAlphaTangentSign(asuint(hit.positionPacked.w), alpha, sign);
    return sign;
}

float3 GetSurfaceHitShadingNormal(GpuSurfaceHit hit)
{
    return _LucentUnpackOct32(hit.packedNormalsRoughAnisoTangent.x);
}

float3 GetSurfaceHitGeomNormal(GpuSurfaceHit hit)
{
    return _LucentUnpackOct32(hit.packedNormalsRoughAnisoTangent.z);
}

float3 GetSurfaceHitTangent(GpuSurfaceHit hit)
{
    return _LucentUnpackOct32(hit.packedNormalsRoughAnisoTangent.w);
}

float GetSurfaceHitMaterialModelFloat(GpuSurfaceHit hit)
{
    return float(GetSurfaceHitMaterialModel(hit));
}

void SetSurfaceHitPositionAndAlphaTangentSign(inout GpuSurfaceHit hit, float3 position, float alpha, float tangentSign)
{
    hit.positionPacked = float4(position, asfloat(_LucentPackAlphaTangentSign(alpha, tangentSign)));
}

void SetSurfaceHitNormalsRoughAnisoTangent(inout GpuSurfaceHit hit, float3 shadingNormal, float roughness, float anisotropy, float3 geomNormal, float3 tangent)
{
    hit.packedNormalsRoughAnisoTangent = uint4(
        _LucentPackOct32(shadingNormal),
        _LucentPackHalf2(roughness, anisotropy),
        _LucentPackOct32(geomNormal),
        _LucentPackOct32(tangent));
}

GpuSurfaceHit CreateEmptySurfaceHit()
{
    GpuSurfaceHit hit;
    hit.flagsMaterial = _LucentPackFlagsMaterial(0u, LUCENT_HIT_MATERIAL_STANDARD);
    hit.t = 0.0;
    hit.instanceId = 0u;
    hit.positionPacked = float4(0.0, 0.0, 0.0, asfloat(_LucentPackAlphaTangentSign(1.0, 1.0)));
    hit.packedNormalsRoughAnisoTangent = uint4(
        _LucentPackOct32(float3(0.0, 1.0, 0.0)),
        _LucentPackHalf2(1.0, 0.0),
        _LucentPackOct32(float3(0.0, 1.0, 0.0)),
        _LucentPackOct32(float3(1.0, 0.0, 0.0)));
    hit.prevPosition = float3(0.0, 0.0, 0.0);
    hit.uv = float2(0.0, 0.0);
    hit.materialIndex = 0u;
    return hit;
}

#endif
