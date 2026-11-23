#ifndef LUCENT_STANDARD_HIT_INCLUDED
#define LUCENT_STANDARD_HIT_INCLUDED

#include "Assets/LucentPath/Shaders/Sdk/LucentMaterialSdk.hlsl"
#include "Assets/LucentPath/Shaders/LucentPathSurfaceHit.hlsl"

void LucentPackSurface(inout GpuSurfaceHit hit, LucentSurface s)
{
    SetSurfaceHitPositionAndAlphaTangentSign(hit, s.positionWS, saturate(s.alpha), s.tangentWS.w);
    SetSurfaceHitNormalsRoughAnisoTangent(hit, s.normalWS, s.specularRoughness, s.specularAnisotropy, s.geomNormalWS, s.tangentWS.xyz);
    hit.uv = s.uv;
    SetSurfaceHitFlagsMaterial(hit, GetSurfaceHitFlags(hit), s.materialModel);
}

#endif
