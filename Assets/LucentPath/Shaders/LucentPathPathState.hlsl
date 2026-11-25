#ifndef LUCENT_PATH_PATH_STATE_INCLUDED
#define LUCENT_PATH_PATH_STATE_INCLUDED

#define PATH_FLAG_ACTIVE 1u
#define PATH_FLAG_INSIDE_DIELECTRIC 2u
#define PATH_FLAG_PREV_SPECULAR_BOUNCE 4u
#define PATH_FLAG_PRIMARY_REFLECTION_SURFACE 32u
#define PATH_FLAG_PRIMARY_REFLECTION_SKY_SPLIT 64u
#define PATH_FLAG_PRIMARY_TRANSMISSION_SURFACE 128u
#define PATH_FLAG_ENV_NEE_PREV_VERTEX 256u
#define PATH_FLAG_SURFEL_NEE_PREV_VERTEX 512u
#define PATH_FLAG_DELTA_TRANSMISSION_CHAIN 1024u
#define PATH_FLAG_STOCHASTIC_SCATTER 2048u
#define PATH_FLAG_SKY_TRANSMISSION_RESOLVED 4096u
#define PATH_SIGNAL_KIND_SHIFT 3u
#define PATH_SIGNAL_KIND_MASK (3u << PATH_SIGNAL_KIND_SHIFT)

#define PATH_MEDIUM_KIND_NONE 0.0
#define PATH_MEDIUM_KIND_SUBSURFACE 1.0

struct PathState
{
    float3 rayOrigin;
    uint flags;
    float3 rayDirection;
    uint depth;
    float3 throughput;
    float maxDistance;
    float3 dielectricTint;
    float prevBsdfPdf;
    float guideRoughness;
    float primaryReflectionRoughness;
    float mediumKind;
    float mediumInstanceId;
    float mediumDistance;
    float mediumTransportMode;
    uint maxBouncesForPath;
    float dielectricDepth;
};

struct PackedPathState
{
    float3 rayOrigin;
    uint flags;
    float3 rayDirection;
    float maxDistance;
    float prevBsdfPdf;
    float mediumInstanceId;
    uint packedCounts;
    uint throughputXY;
    uint throughputZTintX;
    uint tintYZ;
    uint roughGuidePrim;
    uint mediumDistDielDepth;
};

uint LucentPackHalf2(float a, float b)
{
    return (f32tof16(a) & 0xffffu) | (f32tof16(b) << 16);
}

float LucentUnpackHalfLo(uint packed)
{
    return f16tof32(packed & 0xffffu);
}

float LucentUnpackHalfHi(uint packed)
{
    return f16tof32(packed >> 16);
}

PackedPathState PackPathState(PathState state)
{
    PackedPathState packed;
    packed.rayOrigin = state.rayOrigin;
    packed.flags = state.flags;
    packed.rayDirection = state.rayDirection;
    packed.maxDistance = state.maxDistance;
    packed.prevBsdfPdf = state.prevBsdfPdf;
    packed.mediumInstanceId = state.mediumInstanceId;

    uint mediumKindBit = state.mediumKind == PATH_MEDIUM_KIND_SUBSURFACE ? 1u : 0u;
    uint transportBit = state.mediumTransportMode != 0.0 ? 1u : 0u;
    packed.packedCounts = (state.depth & 0xffu) | ((state.maxBouncesForPath & 0xffu) << 8) | (mediumKindBit << 16) | (transportBit << 17);

    packed.throughputXY = LucentPackHalf2(state.throughput.x, state.throughput.y);
    packed.throughputZTintX = LucentPackHalf2(state.throughput.z, state.dielectricTint.x);
    packed.tintYZ = LucentPackHalf2(state.dielectricTint.y, state.dielectricTint.z);
    packed.roughGuidePrim = LucentPackHalf2(state.guideRoughness, state.primaryReflectionRoughness);
    packed.mediumDistDielDepth = LucentPackHalf2(state.mediumDistance, state.dielectricDepth);
    return packed;
}

PathState UnpackPathState(PackedPathState packed)
{
    PathState state;
    state.rayOrigin = packed.rayOrigin;
    state.flags = packed.flags;
    state.rayDirection = packed.rayDirection;
    state.maxDistance = packed.maxDistance;
    state.prevBsdfPdf = packed.prevBsdfPdf;
    state.mediumInstanceId = packed.mediumInstanceId;

    state.depth = packed.packedCounts & 0xffu;
    state.maxBouncesForPath = (packed.packedCounts >> 8) & 0xffu;
    state.mediumKind = ((packed.packedCounts >> 16) & 1u) != 0u ? PATH_MEDIUM_KIND_SUBSURFACE : PATH_MEDIUM_KIND_NONE;
    state.mediumTransportMode = (float)((packed.packedCounts >> 17) & 1u);

    state.throughput = float3(LucentUnpackHalfLo(packed.throughputXY), LucentUnpackHalfHi(packed.throughputXY), LucentUnpackHalfLo(packed.throughputZTintX));
    state.dielectricTint = float3(LucentUnpackHalfHi(packed.throughputZTintX), LucentUnpackHalfLo(packed.tintYZ), LucentUnpackHalfHi(packed.tintYZ));
    state.guideRoughness = LucentUnpackHalfLo(packed.roughGuidePrim);
    state.primaryReflectionRoughness = LucentUnpackHalfHi(packed.roughGuidePrim);
    state.mediumDistance = LucentUnpackHalfLo(packed.mediumDistDielDepth);
    state.dielectricDepth = LucentUnpackHalfHi(packed.mediumDistDielDepth);
    return state;
}

#if defined(LUCENT_PATH_NEEDS_PATH_STATE_BUFFERS)
#if defined(LUCENT_PATH_SHADE_NEE_ONLY)
StructuredBuffer<PackedPathState> _PathStates;
#else
RWStructuredBuffer<PackedPathState> _PathStates;
#endif
RWStructuredBuffer<uint> _PathFlags;
#if defined(LUCENT_PATH_DXR_MERGED)
#elif defined(LUCENT_PATH_KERNEL_DIRECT_LIGHTING)
StructuredBuffer<Hit> _PathHits;
#else
RWStructuredBuffer<Hit> _PathHits;
RWStructuredBuffer<float> _PathDistances;
#endif
#endif

#if defined(LUCENT_PATH_NEEDS_PATH_STATE_RESOURCES) && !defined(LUCENT_PATH_FRAME_SIGNAL_RESOURCES_DECLARED)
#if defined(LUCENT_PATH_KERNEL_ACCUMULATE_FRAME)
Texture2D<float4> _FrameSurfaceSignalSum;
Texture2D<float4> _FrameReflectionSignalSum;
Texture2D<float4> _FrameTransmissionSignalSum;
Texture2D<float4> _FrameEmissionSum;
Texture2D<float4> _PrimaryDiffuseAlbedo;
Texture2D<float4> _PrimarySpecularF0;
Texture2D<float4> _PrimaryTransmissionTint;
#else
RWTexture2D<float4> _FrameSurfaceSignalSum;
RWTexture2D<float4> _FrameReflectionSignalSum;
RWTexture2D<float4> _FrameTransmissionSignalSum;
RWTexture2D<float4> _FrameEmissionSum;
#if defined(LUCENT_PATH_SHADE_NEE_ONLY)
Texture2D<float4> _PrimaryDiffuseAlbedo;
Texture2D<float4> _PrimarySpecularF0;
Texture2D<float4> _PrimaryTransmissionTint;
#else
RWTexture2D<float4> _PrimaryDiffuseAlbedo;
RWTexture2D<float4> _PrimarySpecularF0;
RWTexture2D<float4> _PrimaryTransmissionTint;
#endif
#if !defined(LUCENT_PATH_DXR_AVAILABLE)
Texture2D<float> _PrimaryHitDistance;
#endif
RWTexture2D<float> _ReflectionHitDistance;
RWTexture2D<float> _TransmissionHitDistance;
#endif
#define LUCENT_PATH_FRAME_SIGNAL_RESOURCES_DECLARED 1
#endif

int _CurrentSampleIndex;
float _IndirectClampMultiplierSurface;
float _IndirectClampMultiplierReflection;
float _IndirectClampMultiplierTransmission;
float _IndirectClampHistoryScale;
Texture2D<float4> _IndirectClampHistoryLutSurface;
Texture2D<float4> _IndirectClampHistoryLutReflection;
Texture2D<float4> _IndirectClampHistoryLutTransmission;
SamplerState indirect_lut_linear_clamp_sampler;
Texture2D<float4> _IndirectClampHistoryMomentsSurface;
Texture2D<float4> _IndirectClampHistoryMomentsReflection;
Texture2D<float4> _IndirectClampHistoryMomentsTransmission;

float IndirectCapSurface(uint2 pixel)
{
    if (_IndirectClampMultiplierSurface <= 0.0)
        return 0.0;
    float trust = saturate(_IndirectClampHistoryMomentsSurface.Load(int3(pixel, 0)).z * _IndirectClampHistoryScale);
    float curve = _IndirectClampHistoryLutSurface.SampleLevel(indirect_lut_linear_clamp_sampler, float2(trust, 0.5), 0).r;
    return _IndirectClampMultiplierSurface * max(curve, 0.0);
}

float IndirectCapReflection(uint2 pixel)
{
    if (_IndirectClampMultiplierReflection <= 0.0)
        return 0.0;
    float trust = saturate(_IndirectClampHistoryMomentsReflection.Load(int3(pixel, 0)).z * _IndirectClampHistoryScale);
    float curve = _IndirectClampHistoryLutReflection.SampleLevel(indirect_lut_linear_clamp_sampler, float2(trust, 0.5), 0).r;
    return _IndirectClampMultiplierReflection * max(curve, 0.0);
}

float IndirectCapTransmission(uint2 pixel)
{
    if (_IndirectClampMultiplierTransmission <= 0.0)
        return 0.0;
    float trust = saturate(_IndirectClampHistoryMomentsTransmission.Load(int3(pixel, 0)).z * _IndirectClampHistoryScale);
    float curve = _IndirectClampHistoryLutTransmission.SampleLevel(indirect_lut_linear_clamp_sampler, float2(trust, 0.5), 0).r;
    return _IndirectClampMultiplierTransmission * max(curve, 0.0);
}

float3 ClampToCap(float3 value, uint depth, float cap)
{
    if (depth == 0u || cap <= 0.0)
        return value;

    float lum = ComputeLuminance(value);
    if (lum > cap && lum > 1e-6)
        return value * (cap / lum);
    return value;
}

uint GetPathIndex(uint2 pixel)
{
    return pixel.y * (uint)_Width + pixel.x;
}

PathState CreateInactivePathState()
{
    PathState state;
    state.rayOrigin = 0.0;
    state.flags = 0u;
    state.rayDirection = float3(0.0, 0.0, 1.0);
    state.depth = 0u;
    state.throughput = 0.0;
    state.maxDistance = EPSILON;
    state.dielectricTint = 1.0;
    state.prevBsdfPdf = 0.0;
    state.guideRoughness = 1.0;
    state.primaryReflectionRoughness = 1.0;
    state.mediumKind = PATH_MEDIUM_KIND_NONE;
    state.mediumInstanceId = 0.0;
    state.mediumDistance = 0.0;
    state.mediumTransportMode = 0.0;
    state.maxBouncesForPath = 1u;
    state.dielectricDepth = 1.0;
    return state;
}

bool IsPathActive(PathState state)
{
    return (state.flags & PATH_FLAG_ACTIVE) != 0u;
}

bool IsInsideDielectricVolume(PathState state)
{
    return (state.flags & PATH_FLAG_INSIDE_DIELECTRIC) != 0u;
}

void SetPathActive(inout PathState state, bool active)
{
    if (active)
        state.flags |= PATH_FLAG_ACTIVE;
    else
        state.flags &= ~PATH_FLAG_ACTIVE;
}

void SetPathInsideDielectric(inout PathState state, bool inside)
{
    if (inside)
        state.flags |= PATH_FLAG_INSIDE_DIELECTRIC;
    else
        state.flags &= ~PATH_FLAG_INSIDE_DIELECTRIC;
}

void SetPathSubsurfaceMediumState(inout PathState state, uint instanceId, float traveledDistance, uint transportMode)
{
    state.mediumKind = PATH_MEDIUM_KIND_SUBSURFACE;
    state.mediumInstanceId = (float)instanceId;
    state.mediumDistance = max(traveledDistance, 0.0);
    state.mediumTransportMode = (float)transportMode;
}

bool WasPreviousBounceSpecular(PathState state)
{
    return (state.flags & PATH_FLAG_PREV_SPECULAR_BOUNCE) != 0u;
}

bool HadEnvNeeAtPrevVertex(PathState state)
{
    return (state.flags & PATH_FLAG_ENV_NEE_PREV_VERTEX) != 0u;
}

void SetEnvNeeAtPrevVertex(inout PathState state, bool active)
{
    if (active)
        state.flags |= PATH_FLAG_ENV_NEE_PREV_VERTEX;
    else
        state.flags &= ~PATH_FLAG_ENV_NEE_PREV_VERTEX;
}

bool HadSurfelNeeAtPrevVertex(PathState state)
{
    return (state.flags & PATH_FLAG_SURFEL_NEE_PREV_VERTEX) != 0u;
}

void SetSurfelNeeAtPrevVertex(inout PathState state, bool active)
{
    if (active)
        state.flags |= PATH_FLAG_SURFEL_NEE_PREV_VERTEX;
    else
        state.flags &= ~PATH_FLAG_SURFEL_NEE_PREV_VERTEX;
}

void SetPreviousBounceSpecular(inout PathState state, bool specular)
{
    if (specular)
        state.flags |= PATH_FLAG_PREV_SPECULAR_BOUNCE;
    else
        state.flags &= ~PATH_FLAG_PREV_SPECULAR_BOUNCE;
}

bool IsPrimaryReflectionSurface(PathState state)
{
    return (state.flags & PATH_FLAG_PRIMARY_REFLECTION_SURFACE) != 0u;
}

void SetPrimaryReflectionSurface(inout PathState state, bool reflectionSurface, float roughness)
{
    if (reflectionSurface)
    {
        state.flags |= PATH_FLAG_PRIMARY_REFLECTION_SURFACE;
        state.primaryReflectionRoughness = saturate(roughness);
    }
    else
    {
        state.flags &= ~PATH_FLAG_PRIMARY_REFLECTION_SURFACE;
        state.primaryReflectionRoughness = 1.0;
    }
}

bool HasPrimaryReflectionSkySplit(PathState state)
{
    return (state.flags & PATH_FLAG_PRIMARY_REFLECTION_SKY_SPLIT) != 0u;
}

void SetPrimaryReflectionSkySplit(inout PathState state, bool split)
{
    if (split)
        state.flags |= PATH_FLAG_PRIMARY_REFLECTION_SKY_SPLIT;
    else
        state.flags &= ~PATH_FLAG_PRIMARY_REFLECTION_SKY_SPLIT;
}

bool IsInDeltaTransmissionChain(PathState state)
{
    return (state.flags & PATH_FLAG_DELTA_TRANSMISSION_CHAIN) != 0u;
}

void SetDeltaTransmissionChain(inout PathState state, bool inChain)
{
    if (inChain)
    {
        state.flags |= PATH_FLAG_DELTA_TRANSMISSION_CHAIN;
    }
    else
    {
        state.flags &= ~PATH_FLAG_DELTA_TRANSMISSION_CHAIN;
    }
}

bool IsPrimaryTransmissionSurface(PathState state)
{
    return (state.flags & PATH_FLAG_PRIMARY_TRANSMISSION_SURFACE) != 0u;
}

void SetPrimaryTransmissionSurface(inout PathState state, bool transmissionSurface)
{
    if (transmissionSurface)
        state.flags |= PATH_FLAG_PRIMARY_TRANSMISSION_SURFACE;
    else
        state.flags &= ~PATH_FLAG_PRIMARY_TRANSMISSION_SURFACE;
}

uint GetPathSignalKind(PathState state)
{
    return (state.flags & PATH_SIGNAL_KIND_MASK) >> PATH_SIGNAL_KIND_SHIFT;
}

void SetPathSignalKind(inout PathState state, uint signalKind)
{
    state.flags = (state.flags & ~PATH_SIGNAL_KIND_MASK) | ((signalKind & 3u) << PATH_SIGNAL_KIND_SHIFT);
}

bool IsSkySplitReflectionContinuation(PathState state)
{
    return state.depth > 0u && HasPrimaryReflectionSkySplit(state) && GetPathSignalKind(state) == PATH_SIGNAL_REFLECTION;
}

uint ResolveScatteringSignalKind(uint currentSignalKind, uint sampleFlags, uint depth)
{
    if (depth > 0u || currentSignalKind != PATH_SIGNAL_SURFACE)
    {
        return currentSignalKind;
    }

    if ((sampleFlags & BXDF_FLAG_TRANSMISSION) != 0u && (sampleFlags & BXDF_FLAG_DIFFUSE) == 0u)
        return PATH_SIGNAL_TRANSMISSION;

    if (((sampleFlags & BXDF_FLAG_REFLECTION) != 0u) && ((sampleFlags & (BXDF_FLAG_GLOSSY | BXDF_FLAG_SPECULAR)) != 0u))
        return PATH_SIGNAL_REFLECTION;

    return PATH_SIGNAL_SURFACE;
}

Ray GetPathRay(PathState state)
{
    Ray ray;
    ray.origin = state.rayOrigin;
    ray.direction = state.rayDirection;
    ray.maxDistance = state.maxDistance;
    return ray;
}

void SetPathRay(inout PathState state, Ray ray)
{
    state.rayOrigin = ray.origin;
    state.rayDirection = ray.direction;
    state.maxDistance = ray.maxDistance;
}

Hit CreateEmptyHit()
{
    Hit hit;
    hit.didHit = 0u;
    hit.t = 0.0;
    hit.triIndex = -1;
    hit.instanceIndex = -1;
    hit.bary = 0.0;
    return hit;
}

#if defined(LUCENT_PATH_NEEDS_PATH_STATE_RESOURCES)
float3 ApplyDiffuseDemodulation(uint2 pixel, float3 value)
{
    float3 albedo = max(_PrimaryDiffuseAlbedo[pixel].rgb, float3(0.01, 0.01, 0.01));
    return value / albedo;
}

float3 ApplySpecularDemodulation(uint2 pixel, float3 value)
{
    float3 f0 = max(_PrimarySpecularF0[pixel].rgb, float3(0.04, 0.04, 0.04));
    return value / f0;
}

float3 ApplyTransmissionDemodulation(uint2 pixel, float3 value)
{
    float3 tint = max(_PrimaryTransmissionTint[pixel].rgb, float3(0.01, 0.01, 0.01));
    return value / tint;
}

void AddFrameSurfaceSignalRaw(uint2 pixel, float3 value)
{
    float4 current = _FrameSurfaceSignalSum[pixel];
    current.rgb += ApplyDiffuseDemodulation(pixel, value);
    _FrameSurfaceSignalSum[pixel] = current;
}

void AccumulateFrameSurfaceSignal(uint2 pixel, float3 value, uint depth)
{
    AddFrameSurfaceSignalRaw(pixel, ClampToCap(value, depth, IndirectCapSurface(pixel)));
}

void AccumulateFrameEmission(uint2 pixel, PathState state, float3 value);

#if defined(LUCENT_PATH_DXR_MERGED)
void AccumulateFrameStableReflection(uint2 pixel, float3 value)
{
    float4 stableLighting = _FrameEmissionSum[pixel];
    stableLighting.rgb += ApplySpecularDemodulation(pixel, value) * _PrimarySpecularF0[pixel].rgb;
    _FrameEmissionSum[pixel] = stableLighting;
}

void MarkFramePrimaryEmission(uint2 pixel)
{
    float4 stableLighting = _FrameEmissionSum[pixel];
    stableLighting.a = 1.0;
    _FrameEmissionSum[pixel] = stableLighting;
}
#endif

void AccumulateFrameReflectionSignal(uint2 pixel, PathState state, float3 value)
{
    value = ClampToCap(value, state.depth, IndirectCapReflection(pixel));
    if (IsPrimaryReflectionSurface(state))
    {
        float4 currentReflection = _FrameReflectionSignalSum[pixel];
        currentReflection.rgb += ApplySpecularDemodulation(pixel, value);
        _FrameReflectionSignalSum[pixel] = currentReflection;
        return;
    }

    AddFrameSurfaceSignalRaw(pixel, value);
}

void AccumulateFrameTransmissionSignal(uint2 pixel, PathState state, float3 value)
{
    value = ClampToCap(value, state.depth, IndirectCapTransmission(pixel));
    float4 current = _FrameTransmissionSignalSum[pixel];
    current.rgb += ApplyTransmissionDemodulation(pixel, value);
    _FrameTransmissionSignalSum[pixel] = current;
}

void AccumulateFrameSignal(uint2 pixel, PathState state, uint signalKind, float3 value)
{
    if (signalKind == PATH_SIGNAL_REFLECTION)
        AccumulateFrameReflectionSignal(pixel, state, value);
    else if (signalKind == PATH_SIGNAL_TRANSMISSION)
        AccumulateFrameTransmissionSignal(pixel, state, value);
    else
        AccumulateFrameSurfaceSignal(pixel, value, state.depth);
}

void AccumulateFrameEmission(uint2 pixel, PathState state, float3 value)
{
    if (state.depth == 0u)
    {
        AddFrameSurfaceSignalRaw(pixel, value);
        return;
    }
    uint signalKind = GetPathSignalKind(state);
    AccumulateFrameSignal(pixel, state, signalKind, value);
}

void AccumulateFrameHitMask(uint2 pixel, float value)
{
    float4 current = _FrameSurfaceSignalSum[pixel];
    current.a += value;
    _FrameSurfaceSignalSum[pixel] = current;
}

void AccumulateFrameAlpha(uint2 pixel, float value)
{
    float4 current = _FrameTransmissionSignalSum[pixel];
    current.a += value;
    _FrameTransmissionSignalSum[pixel] = current;
}
#endif

#endif
