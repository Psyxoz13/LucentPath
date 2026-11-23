#ifndef LUCENT_PATH_PASS_SHARED_INCLUDED
#define LUCENT_PATH_PASS_SHARED_INCLUDED

#include "LucentPathCommon.hlsl"
#include "LucentPathResources.hlsl"
#include "LucentPathRandom.hlsl"
#include "LucentPathSampling.hlsl"
#include "LucentPathCamera.hlsl"
#include "LucentPathSurfaceHit.hlsl"
#include "LucentPathPathState.hlsl"

#if defined(LUCENT_PATH_KERNEL_DIRECT_LIGHTING) || defined(LUCENT_PATH_KERNEL_ADVANCE_PATH)
#include "LucentPathShading.hlsl"
#include "LucentPathSurfaceHitAdapter.hlsl"
#endif

#endif
