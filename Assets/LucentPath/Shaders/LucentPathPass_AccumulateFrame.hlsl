#ifndef LUCENT_PATH_PASS_ACCUMULATE_FRAME_INCLUDED
#define LUCENT_PATH_PASS_ACCUMULATE_FRAME_INCLUDED

static const float SIGNAL_STATISTICS_EPSILON = 1e-5;
static const float SPATIAL_VARIANCE_HISTORY_LENGTH_THRESHOLD = 4.0;
static const int SPATIAL_VARIANCE_RADIUS = 3;
static const uint SIGNAL_GUIDE_SURFACE = 0u;
static const uint SIGNAL_GUIDE_REFLECTION = 1u;
static const uint SIGNAL_GUIDE_TRANSMISSION = 2u;
static const uint SIGNAL_GUIDE_SHADOW = 3u;

int _UseSecondaryCompaction;
float _BootstrapMissThreshold;
StructuredBuffer<uint> _ActiveSecondaryIndices;
StructuredBuffer<uint> _SecondaryDispatchInfo;
StructuredBuffer<uint> _BootstrapDispatchInfo;
RWStructuredBuffer<uint> _BootstrapActiveIndices;
RWStructuredBuffer<uint> _BootstrapDispatchArgs;
#if defined(LUCENT_ACCUMULATE_PRE_PASS)
RWTexture2D<float4> _ReflectionPrePassSignal;
#else
Texture2D<float4> _ReflectionPrePassSignal;
#endif
float _ReflectionPrePassRadius;

float2 GetCurrentUv(uint2 pixel)
{
    return (float2(pixel) + 0.5) / float2(_Width, _Height);
}

bool IsUvInsideHistory(float2 uv)
{
    return all(uv >= 0.0) && all(uv <= 1.0);
}

int2 UvToPixel(float2 uv)
{
    int2 pixel = int2(floor(uv * float2(_Width, _Height)));
    return clamp(pixel, int2(0, 0), int2(_Width - 1, _Height - 1));
}

float ComputeDepthConfidence(float relativeDepthDelta)
{
    return saturate(1.0 - relativeDepthDelta / max(_TemporalDepthRejectThreshold * _HistoryDepthConfidenceScale, 1e-4));
}

float ComputeNormalConfidence(float normalAgreement)
{
    float normalSpan = max(1.0 - _TemporalNormalRejectThreshold, 1e-3);
    return pow(saturate((normalAgreement - _TemporalNormalRejectThreshold) / normalSpan), _HistoryNormalConfidencePower);
}

static const float SAMPLED_SHADOW_CHANGE_SIGMA_SCALE = 3.0;

float ComputeSampledShadowChangeExcess(float shadowSum, float prevShadowSum, float shadowWeight)
{
    float currentMean = shadowSum / shadowWeight;
    float prevMean = saturate(prevShadowSum / shadowWeight);
    float smoothedMean = (prevMean * shadowWeight + .5) / (shadowWeight + 1.0);
    float samplingSigma = sqrt(smoothedMean * (1.0 - smoothedMean) / shadowWeight);
    float threshold = SAMPLED_SHADOW_CHANGE_SIGMA_SCALE * samplingSigma;

    if (threshold >= 1.0)
    {
        return 0.0;
    }

    return saturate((abs(currentMean - prevMean) - threshold) / (1.0 - threshold));
}

float ComputeMotionConfidence(float motionPixels)
{
    if (_MotionRejectionStrength <= 0.0)
        return 1.0;

    return 1.0 - saturate(motionPixels / max(_MotionRejectionStrength, 1e-4));
}

float ComputeYoungHistoryBoost(float historyLength)
{
    float youngFrames = (float)max(_YoungHistoryFrames, 0);
    if (youngFrames <= 0.0)
        return 0.0;

    float historyAge = max(historyLength - 1.0, 0.0);
    return saturate(1.0 - historyAge / youngFrames);
}

int ComputeYoungHistoryRadius(int baseRadius, float youngHistoryBoost)
{
    int youngRadius = clamp(_YoungHistoryRadius, 0, 4);
    float boostedRadius = lerp((float)baseRadius, (float)max(baseRadius, youngRadius), youngHistoryBoost);
    return clamp((int)ceil(boostedRadius), 0, 4);
}

float ComputeYoungHistoryVarianceMultiplier(float recoveryBoost)
{
    return lerp(1.0, max(_YoungHistoryVarianceBoost, 1.0), recoveryBoost);
}

float ComputeRecoveryBoost(float historyLength, float spatialBlend, float historyConfidence)
{
    float youngHistoryBoost = ComputeYoungHistoryBoost(historyLength);
    float lowConfidenceBoost = 1.0 - saturate(historyConfidence);
    return saturate(max(max(youngHistoryBoost, spatialBlend), lowConfidenceBoost));
}

float ComputeRepairUncertaintyVarianceFloor(
    float repairedLuminance,
    float repairResidual,
    float spatialVariance,
    float recoveryBoost)
{
    float luminanceFloor = max(max(repairedLuminance * 0.05, _SpatialBootstrapLuminanceFloor), 1e-4);
    float uncertaintyVariance = max(max(spatialVariance, repairResidual * repairResidual), luminanceFloor * luminanceFloor);
    return uncertaintyVariance * ComputeYoungHistoryVarianceMultiplier(recoveryBoost);
}

void PreserveRepairUncertainty(
    inout float4 moments,
    float repairedLuminance,
    float repairResidual,
    float spatialVariance,
    float recoveryBoost)
{
    if (recoveryBoost <= 1e-4)
        return;

    float varianceFloor = ComputeRepairUncertaintyVarianceFloor(
        repairedLuminance,
        repairResidual,
        spatialVariance,
        recoveryBoost);
    moments.x = repairedLuminance;
    moments.w = max(moments.w, varianceFloor);
    moments.y = moments.x * moments.x + moments.w;
}

float4 BuildYoungHistoryAtrousSignal(float historyLength, float spatialBlend, float historyConfidence, float variance)
{
    float recoveryBoost = ComputeRecoveryBoost(historyLength, spatialBlend, historyConfidence);
    float youngHistoryRadius = lerp(0.0, (float)clamp(_YoungHistoryRadius, 0, 4), recoveryBoost);
    float denoiserVariance = max(variance, 0.0) * ComputeYoungHistoryVarianceMultiplier(recoveryBoost);
    return float4(recoveryBoost, youngHistoryRadius, saturate(spatialBlend), denoiserVariance);
}

float ApplyHistoryConfidenceCurve(uint2 pixel, uint signalKind, float confidence)
{
    confidence = pow(saturate(confidence), max(_HistoryConfidencePower, 0.01));

    float maxHistoryLength = max((float)_MaxHistoryLength, 1.0);
    float ditherAmplitude = saturate(_HistoryConfidenceDitherStrength) * 0.5 / maxHistoryLength;
    if (ditherAmplitude > 0.0)
    {
        uint2 ditherSeed = pixel ^ uint2((uint)_FrameIndex * 1664525u + signalKind * 1013904223u, signalKind * 747796405u + 2891336453u);
        float dither = UintToFloat01(Hash2(ditherSeed)) - 0.5;
        confidence = saturate(confidence + dither * ditherAmplitude);
    }

    return confidence;
}

bool SameSurfaceIdentity(float4 currentGuide1, float4 prevGuide1)
{
    return currentGuide1.z == prevGuide1.z &&
        currentGuide1.w == prevGuide1.w;
}

bool HasValidSecondaryIdentity(float4 guide1)
{
    return guide1.z >= 0.0;
}

float ExpectedPreviousDepth(float currentDepth, float4 currentGuide2)
{
    return max(currentDepth + currentGuide2.w, 1e-4);
}

bool SameGuidePathKind(float currentKind, float prevKind, float expectedKind)
{
    return abs(currentKind - expectedKind) < 0.5 &&
        abs(prevKind - expectedKind) < 0.5;
}

void EvaluateGuideMatch(
    float4 currentGuide0,
    float4 currentGuide2,
    float4 prevGuide0,
    float4 prevGuide2,
    out float relativeDepthDelta,
    out float directionAgreement)
{
    float currentDepth = max(currentGuide0.w, 1e-4);
    float expectedPrevDepth = ExpectedPreviousDepth(currentDepth, currentGuide2);
    float prevDepth = max(prevGuide0.w, 1e-4);
    relativeDepthDelta = abs(expectedPrevDepth - prevDepth) / max(expectedPrevDepth, 1e-4);
    directionAgreement = dot(SafeNormalize(currentGuide0.xyz), SafeNormalize(prevGuide0.xyz));
}

bool ValidateSurfaceHistory(
    float4 currentGuide0,
    float4 currentGuide1,
    float4 currentGuide2,
    float4 prevGuide0,
    float4 prevGuide1,
    float4 prevGuide2,
    out float relativeDepthDelta,
    out float directionAgreement)
{
    bool currentSky = IsSkyGuide(currentGuide0);
    bool prevSky = IsSkyGuide(prevGuide0);
    relativeDepthDelta = 1.0;
    directionAgreement = dot(SafeNormalize(currentGuide0.xyz), SafeNormalize(prevGuide0.xyz));

    bool valid = currentSky == prevSky && SamePathChain(currentGuide2, prevGuide2);

    if (valid && currentSky)
        valid = directionAgreement >= _TemporalNormalRejectThreshold;
    else if (valid)
    {
        EvaluateGuideMatch(currentGuide0, currentGuide2, prevGuide0, prevGuide2, relativeDepthDelta, directionAgreement);
        valid = SameSurfaceIdentity(currentGuide1, prevGuide1) &&
            relativeDepthDelta <= _TemporalDepthRejectThreshold &&
            directionAgreement >= _TemporalNormalRejectThreshold;
    }

    return valid;
}

bool ValidateReflectionHistory(
    float4 currentGuide0,
    float4 currentGuide1,
    float4 currentGuide2,
    float4 prevGuide0,
    float4 prevGuide1,
    float4 prevGuide2,
    out float relativeDepthDelta,
    out float directionAgreement)
{
    relativeDepthDelta = 1.0;
    directionAgreement = 0.0;

    if (!SameGuidePathKind(currentGuide2.x, prevGuide2.x, GUIDE_PATH_KIND_SPECULAR))
        return false;

    bool currentSky = IsSkyGuide(currentGuide0);
    bool prevSky = IsSkyGuide(prevGuide0);
    if (currentSky || prevSky)
    {
        bool validSkyHistory = currentSky == prevSky;
        relativeDepthDelta = validSkyHistory ? 0.0 : 1.0;
        directionAgreement = validSkyHistory ? 1.0 : 0.0;
        return validSkyHistory;
    }

    if (!HasValidSecondaryIdentity(currentGuide1) || !HasValidSecondaryIdentity(prevGuide1))
        return false;
    if (!SameSurfaceIdentity(currentGuide1, prevGuide1))
        return false;

    EvaluateGuideMatch(currentGuide0, currentGuide2, prevGuide0, prevGuide2, relativeDepthDelta, directionAgreement);
    float roughness = saturate(currentGuide2.y);
    float directionThreshold = lerp(0.995, _TemporalNormalRejectThreshold, roughness);
    float depthThreshold = _TemporalDepthRejectThreshold * lerp(0.5, 2.0, roughness);
    return relativeDepthDelta <= max(depthThreshold, 1e-4) &&
        directionAgreement >= max(directionThreshold, _TemporalNormalRejectThreshold);
}

bool ValidateTransmissionHistory(
    float4 currentGuide0,
    float4 currentGuide1,
    float4 currentGuide2,
    float4 prevGuide0,
    float4 prevGuide1,
    float4 prevGuide2,
    out float relativeDepthDelta,
    out float directionAgreement)
{
    relativeDepthDelta = 1.0;
    directionAgreement = 0.0;

    if (!SameGuidePathKind(currentGuide2.x, prevGuide2.x, GUIDE_PATH_KIND_TRANSMISSION))
        return false;

    float etaDelta = abs(max(currentGuide2.y, 1.0) - max(prevGuide2.y, 1.0));
    float mediumDelta = abs(currentGuide2.z - prevGuide2.z);
    if (etaDelta > 0.025 || mediumDelta > 0.5)
        return false;

    bool currentSky = IsSkyGuide(currentGuide0);
    bool prevSky = IsSkyGuide(prevGuide0);
    if (currentSky || prevSky)
    {
        bool validSkyHistory = currentSky == prevSky;
        relativeDepthDelta = validSkyHistory ? 0.0 : 1.0;
        directionAgreement = validSkyHistory ? 1.0 : 0.0;
        relativeDepthDelta = validSkyHistory ? 0.0 : 1.0;
        return validSkyHistory;
    }

    if (!HasValidSecondaryIdentity(currentGuide1) || !HasValidSecondaryIdentity(prevGuide1))
        return false;
    if (!SameSurfaceIdentity(currentGuide1, prevGuide1))
        return false;

    EvaluateGuideMatch(currentGuide0, currentGuide2, prevGuide0, prevGuide2, relativeDepthDelta, directionAgreement);
    return relativeDepthDelta <= _TemporalDepthRejectThreshold &&
        directionAgreement >= _TemporalNormalRejectThreshold;
}

bool ValidateHistory(
    uint guideKind,
    float4 currentGuide0,
    float4 currentGuide1,
    float4 currentGuide2,
    float4 prevGuide0,
    float4 prevGuide1,
    float4 prevGuide2,
    out float relativeDepthDelta,
    out float directionAgreement)
{
    if (guideKind == SIGNAL_GUIDE_REFLECTION)
    {
        return ValidateReflectionHistory(
            currentGuide0, currentGuide1, currentGuide2,
            prevGuide0, prevGuide1, prevGuide2,
            relativeDepthDelta, directionAgreement);
    }

    if (guideKind == SIGNAL_GUIDE_TRANSMISSION)
    {
        return ValidateTransmissionHistory(
            currentGuide0, currentGuide1, currentGuide2,
            prevGuide0, prevGuide1, prevGuide2,
            relativeDepthDelta, directionAgreement);
    }

    return ValidateSurfaceHistory(
        currentGuide0, currentGuide1, currentGuide2,
        prevGuide0, prevGuide1, prevGuide2,
        relativeDepthDelta, directionAgreement);
}

struct CurrentFrameSample
{
    float4 source;
    float4 direct;
    float4 indirect;
    float4 surface;
    float4 reflection;
    float4 transmission;
    float hitMask;
};

CurrentFrameSample LoadCurrentFrameSample(int2 pixel, float invSampleCount)
{
    CurrentFrameSample sample;
    float4 frameSurfaceSignalSum = _FrameSurfaceSignalSum.Load(int3(pixel, 0));
    float4 frameReflectionSignalSum = float4(_ReflectionPrePassSignal[pixel].rgb, _FrameReflectionSignalSum.Load(int3(pixel, 0)).a);
    float4 frameTransmissionSignalSum = _FrameTransmissionSignalSum.Load(int3(pixel, 0));
    float3 frameSurface = frameSurfaceSignalSum.rgb * invSampleCount;
    float3 frameReflection = frameReflectionSignalSum.rgb * invSampleCount;
    float3 frameTransmission = frameTransmissionSignalSum.rgb * invSampleCount;
    float frameHitMask = frameSurfaceSignalSum.a * invSampleCount;
    float frameAlpha = frameTransmissionSignalSum.a * invSampleCount;

    sample.source = float4(frameSurface + frameReflection + frameTransmission, frameAlpha);
    sample.direct = float4(frameSurface, frameHitMask);
    sample.indirect = float4(frameReflection + frameTransmission, frameAlpha);
    sample.surface = float4(frameSurface, frameHitMask);
    sample.reflection = float4(frameReflection, frameHitMask);
    sample.transmission = float4(frameTransmission, frameAlpha);
    sample.hitMask = frameHitMask;
    return sample;
}

float4 SelectCurrentSignal(CurrentFrameSample sample, uint signalKind)
{
    if (signalKind == PATH_SIGNAL_REFLECTION)
        return sample.reflection;
    if (signalKind == PATH_SIGNAL_TRANSMISSION)
        return sample.transmission;
    
    return sample.surface;
}

float4 LoadCurrentGuide0(uint guideKind, int2 pixel)
{
    if (guideKind == SIGNAL_GUIDE_SHADOW)
        return _ShadowGuide0.Load(int3(pixel, 0));
    if (guideKind == SIGNAL_GUIDE_REFLECTION)
        return _ReflectionGuide0.Load(int3(pixel, 0));
    if (guideKind == SIGNAL_GUIDE_TRANSMISSION)
        return _TransmissionGuide0.Load(int3(pixel, 0));
    
    return _Guide0.Load(int3(pixel, 0));
}

float4 LoadCurrentGuide1(uint guideKind, int2 pixel)
{
    if (guideKind == SIGNAL_GUIDE_SHADOW)
        return _ShadowGuide1.Load(int3(pixel, 0));
    if (guideKind == SIGNAL_GUIDE_REFLECTION)
        return _ReflectionGuide1.Load(int3(pixel, 0));
    if (guideKind == SIGNAL_GUIDE_TRANSMISSION)
        return _TransmissionGuide1.Load(int3(pixel, 0));
    
    return _Guide1.Load(int3(pixel, 0));
}

float4 LoadCurrentGuide2(uint guideKind, int2 pixel)
{
    if (guideKind == SIGNAL_GUIDE_SHADOW)
        return _ShadowGuide2.Load(int3(pixel, 0));
    if (guideKind == SIGNAL_GUIDE_REFLECTION)
        return _ReflectionGuide2.Load(int3(pixel, 0));
    if (guideKind == SIGNAL_GUIDE_TRANSMISSION)
        return _TransmissionGuide2.Load(int3(pixel, 0));
    
    return _Guide2.Load(int3(pixel, 0));
}

float ComputeSurfaceSpatialBootstrapGuideWeight(
    float4 centerGuide0,
    float4 centerGuide1,
    float4 centerGuide2,
    float4 sampleGuide0,
    float4 sampleGuide1,
    float4 sampleGuide2)
{
    bool centerSky = IsSkyGuide(centerGuide0);
    bool sampleSky = IsSkyGuide(sampleGuide0);
    if (centerSky != sampleSky || !SamePathChain(centerGuide2, sampleGuide2))
        return 0.0;

    float normalAgreement = dot(SafeNormalize(centerGuide0.xyz), SafeNormalize(sampleGuide0.xyz));
    if (normalAgreement < _SpatialBootstrapNormalThreshold)
        return 0.0;

    if (centerSky)
        return pow(saturate(normalAgreement), _SpatialBootstrapNormalPhi);

    if (!SameSurfaceIdentity(centerGuide1, sampleGuide1))
        return 0.0;

    float centerDepth = max(centerGuide0.w, 1e-4);
    float sampleDepth = max(sampleGuide0.w, 1e-4);
    float relativeDepthDelta = abs(centerDepth - sampleDepth) / centerDepth;
    if (relativeDepthDelta > _SpatialBootstrapDepthRejectThreshold)
        return 0.0;

    return exp(-relativeDepthDelta * _SpatialBootstrapDepthPhi) *
        pow(saturate(normalAgreement), _SpatialBootstrapNormalPhi);
}

struct SpatialSignalVarianceStats
{
    float variance;
    float supportWeight;
};

struct SurfaceSpatialBootstrapStats
{
    float4 signal;
    float blendScale;
    float variance;
    float shadowSignal;
};

struct SpatialNeighborhoodStats
{
    SurfaceSpatialBootstrapStats surfaceBootstrap;
    SpatialSignalVarianceStats surfaceVariance;
    SpatialSignalVarianceStats reflectionVariance;
    SpatialSignalVarianceStats transmissionVariance;
};

bool TryGetSpatialVarianceFallbackParameters(float4 moments, out float youngHistoryBoost, out int radius)
{
    youngHistoryBoost = ComputeYoungHistoryBoost(moments.z);
    if (moments.z >= SPATIAL_VARIANCE_HISTORY_LENGTH_THRESHOLD && youngHistoryBoost <= 0.0)
    {
        radius = 0;
        return false;
    }

    radius = ComputeYoungHistoryRadius(SPATIAL_VARIANCE_RADIUS, youngHistoryBoost);
    return radius > 0;
}

void ApplySpatialVarianceFallback(float youngHistoryBoost, float supportWeight, float spatialVariance, inout float4 moments)
{
    if (supportWeight <= 1e-4)
        return;

    spatialVariance *= SPATIAL_VARIANCE_HISTORY_LENGTH_THRESHOLD / max(moments.z, 1.0);
    spatialVariance *= ComputeYoungHistoryVarianceMultiplier(youngHistoryBoost);
    moments.w = spatialVariance;
    moments.y = moments.x * moments.x + moments.w;
}

SpatialNeighborhoodStats ComputeSpatialNeighborhoodStats(
    int2 pixel,
    float invSampleCount,
    float4 currentSurface,
    float4 currentGuide0,
    float4 currentGuide1,
    float4 currentGuide2,
    float surfaceYoungHistoryBoost,
    float surfaceVarianceCenterLuminance,
    int surfaceBootstrapRadius,
    int surfaceVarianceRadius,
    float4 currentReflectionGuide0,
    float4 currentReflectionGuide1,
    float4 currentReflectionGuide2,
    float reflectionVarianceCenterLuminance,
    int reflectionVarianceRadius,
    float4 currentTransmissionGuide0,
    float4 currentTransmissionGuide1,
    float4 currentTransmissionGuide2,
    float transmissionVarianceCenterLuminance,
    int transmissionVarianceRadius)
{
    SpatialNeighborhoodStats result;
    result.surfaceBootstrap.signal = currentSurface;
    result.surfaceBootstrap.blendScale = 0.0;
    result.surfaceBootstrap.variance = 0.0;
    result.surfaceBootstrap.shadowSignal = saturate(_FrameShadowSum.Load(int3(pixel, 0)).r * invSampleCount);
    result.surfaceVariance.variance = 0.0;
    result.surfaceVariance.supportWeight = 0.0;
    result.reflectionVariance.variance = 0.0;
    result.reflectionVariance.supportWeight = 0.0;
    result.transmissionVariance.variance = 0.0;
    result.transmissionVariance.supportWeight = 0.0;

    surfaceBootstrapRadius = clamp(surfaceBootstrapRadius, 0, 4);
    surfaceVarianceRadius = clamp(surfaceVarianceRadius, 0, 4);
    reflectionVarianceRadius = clamp(reflectionVarianceRadius, 0, 4);
    transmissionVarianceRadius = clamp(transmissionVarianceRadius, 0, 4);

    int maxRadius = max(max(surfaceBootstrapRadius, surfaceVarianceRadius), max(reflectionVarianceRadius, transmissionVarianceRadius));
    if (maxRadius <= 0)
        return result;

    float centerBootstrapLuminance = ComputeLuminance(currentSurface.rgb);
    float bootstrapLuminanceFloor = max(_SpatialBootstrapLuminanceFloor, 1e-4);
    float4 surfaceBootstrapSum = currentSurface;
    float surfaceBootstrapWeightSum = 1.0;
    float surfaceBootstrapLuminanceSum = centerBootstrapLuminance;
    float surfaceBootstrapLuminanceSqSum = centerBootstrapLuminance * centerBootstrapLuminance;
    float shadowBootstrapSum = saturate(_FrameShadowSum.Load(int3(pixel, 0)).r * invSampleCount);

    float surfaceVarianceLuminanceSum = surfaceVarianceCenterLuminance;
    float surfaceVarianceLuminanceSqSum = surfaceVarianceCenterLuminance * surfaceVarianceCenterLuminance;
    float surfaceVarianceWeightSum = 1.0;

    float reflectionVarianceLuminanceSum = reflectionVarianceCenterLuminance;
    float reflectionVarianceLuminanceSqSum = reflectionVarianceCenterLuminance * reflectionVarianceCenterLuminance;
    float reflectionVarianceWeightSum = 1.0;

    float transmissionVarianceLuminanceSum = transmissionVarianceCenterLuminance;
    float transmissionVarianceLuminanceSqSum = transmissionVarianceCenterLuminance * transmissionVarianceCenterLuminance;
    float transmissionVarianceWeightSum = 1.0;

    [loop]
    for (int y = -maxRadius; y <= maxRadius; y++)
    {
        [loop]
        for (int x = -maxRadius; x <= maxRadius; x++)
        {
            if (x == 0 && y == 0)
                continue;

            int2 offset = int2(x, y);
            int2 samplePixel = pixel + offset;
            if (samplePixel.x < 0 || samplePixel.y < 0 || samplePixel.x >= _Width || samplePixel.y >= _Height)
                continue;

            int sampleRadius = max(abs(x), abs(y));

            bool useSurfaceNeighborhood = max(surfaceBootstrapRadius, surfaceVarianceRadius) >= sampleRadius;
            if (useSurfaceNeighborhood)
            {
                float4 sampleGuide0 = _Guide0.Load(int3(samplePixel, 0));
                float4 sampleGuide1 = _Guide1.Load(int3(samplePixel, 0));
                float4 sampleGuide2 = _Guide2.Load(int3(samplePixel, 0));
                float guideWeight = ComputeSurfaceSpatialBootstrapGuideWeight(
                    currentGuide0, currentGuide1, currentGuide2,
                    sampleGuide0, sampleGuide1, sampleGuide2);

                if (guideWeight > 1e-5)
                {
                    float4 frameSurfaceSum = _FrameSurfaceSignalSum.Load(int3(samplePixel, 0));
                    float4 sampleSurface = float4(frameSurfaceSum.rgb * invSampleCount, frameSurfaceSum.a * invSampleCount);
                    float sampleSurfaceLuminance = ComputeLuminance(sampleSurface.rgb);

                    if (surfaceVarianceRadius >= sampleRadius)
                    {
                        surfaceVarianceLuminanceSum += sampleSurfaceLuminance * guideWeight;
                        surfaceVarianceLuminanceSqSum += sampleSurfaceLuminance * sampleSurfaceLuminance * guideWeight;
                        surfaceVarianceWeightSum += guideWeight;
                    }

                    if (surfaceBootstrapRadius >= sampleRadius)
                    {
                        float luminanceScale = max(max(centerBootstrapLuminance, sampleSurfaceLuminance) * 0.25, bootstrapLuminanceFloor);
                        float luminanceSimilarity = exp(-abs(sampleSurfaceLuminance - centerBootstrapLuminance) / luminanceScale * _SpatialBootstrapLuminancePhi);
                        float luminanceWeight = lerp(
                            saturate(_SpatialBootstrapVarianceMinBlend),
                            1.0,
                            luminanceSimilarity);
                        float bootstrapWeight = guideWeight * luminanceWeight;
                        if (bootstrapWeight > 1e-5)
                        {
                            surfaceBootstrapSum += sampleSurface * bootstrapWeight;
                            surfaceBootstrapWeightSum += bootstrapWeight;
                            surfaceBootstrapLuminanceSum += sampleSurfaceLuminance * bootstrapWeight;
                            surfaceBootstrapLuminanceSqSum += sampleSurfaceLuminance * sampleSurfaceLuminance * bootstrapWeight;
                            shadowBootstrapSum += saturate(_FrameShadowSum.Load(int3(samplePixel, 0)).r * invSampleCount) * bootstrapWeight;
                        }
                    }
                }
            }

            if (reflectionVarianceRadius >= sampleRadius)
            {
                float4 sampleGuide0 = _ReflectionGuide0.Load(int3(samplePixel, 0));
                float4 sampleGuide1 = _ReflectionGuide1.Load(int3(samplePixel, 0));
                float4 sampleGuide2 = _ReflectionGuide2.Load(int3(samplePixel, 0));
                float guideWeight = ComputeSurfaceSpatialBootstrapGuideWeight(
                    currentReflectionGuide0, currentReflectionGuide1, currentReflectionGuide2,
                    sampleGuide0, sampleGuide1, sampleGuide2);
                if (guideWeight > 1e-5)
                {
                    float sampleLuminance = ComputeLuminance(_FrameReflectionSignalSum.Load(int3(samplePixel, 0)).rgb * invSampleCount);
                    reflectionVarianceLuminanceSum += sampleLuminance * guideWeight;
                    reflectionVarianceLuminanceSqSum += sampleLuminance * sampleLuminance * guideWeight;
                    reflectionVarianceWeightSum += guideWeight;
                }
            }

            if (transmissionVarianceRadius >= sampleRadius)
            {
                float4 sampleGuide0 = _TransmissionGuide0.Load(int3(samplePixel, 0));
                float4 sampleGuide1 = _TransmissionGuide1.Load(int3(samplePixel, 0));
                float4 sampleGuide2 = _TransmissionGuide2.Load(int3(samplePixel, 0));
                float guideWeight = ComputeSurfaceSpatialBootstrapGuideWeight(
                    currentTransmissionGuide0, currentTransmissionGuide1, currentTransmissionGuide2,
                    sampleGuide0, sampleGuide1, sampleGuide2);
                if (guideWeight > 1e-5)
                {
                    float sampleLuminance = ComputeLuminance(_FrameTransmissionSignalSum.Load(int3(samplePixel, 0)).rgb * invSampleCount);
                    transmissionVarianceLuminanceSum += sampleLuminance * guideWeight;
                    transmissionVarianceLuminanceSqSum += sampleLuminance * sampleLuminance * guideWeight;
                    transmissionVarianceWeightSum += guideWeight;
                }
            }
        }
    }

    result.surfaceVariance.supportWeight = surfaceVarianceWeightSum - 1.0;
    if (surfaceVarianceWeightSum > 1.0001)
    {
        float meanLuminance = surfaceVarianceLuminanceSum / surfaceVarianceWeightSum;
        result.surfaceVariance.variance = max(surfaceVarianceLuminanceSqSum / surfaceVarianceWeightSum - meanLuminance * meanLuminance, 0.0);
    }

    result.reflectionVariance.supportWeight = reflectionVarianceWeightSum - 1.0;
    if (reflectionVarianceWeightSum > 1.0001)
    {
        float meanLuminance = reflectionVarianceLuminanceSum / reflectionVarianceWeightSum;
        result.reflectionVariance.variance = max(reflectionVarianceLuminanceSqSum / reflectionVarianceWeightSum - meanLuminance * meanLuminance, 0.0);
    }

    result.transmissionVariance.supportWeight = transmissionVarianceWeightSum - 1.0;
    if (transmissionVarianceWeightSum > 1.0001)
    {
        float meanLuminance = transmissionVarianceLuminanceSum / transmissionVarianceWeightSum;
        result.transmissionVariance.variance = max(transmissionVarianceLuminanceSqSum / transmissionVarianceWeightSum - meanLuminance * meanLuminance, 0.0);
    }

    if (surfaceBootstrapWeightSum > 1.0001)
    {
        float meanLuminance = surfaceBootstrapLuminanceSum / surfaceBootstrapWeightSum;
        float spatialVariance = max(surfaceBootstrapLuminanceSqSum / surfaceBootstrapWeightSum - meanLuminance * meanLuminance, 0.0);
        float normalizedVariance = spatialVariance / max(meanLuminance * meanLuminance, bootstrapLuminanceFloor * bootstrapLuminanceFloor);
        float varianceBlend = lerp(
            1.0,
            saturate(_SpatialBootstrapVarianceMinBlend),
            saturate(normalizedVariance / max(_SpatialBootstrapVarianceCutoff, 1e-4)));

        result.surfaceBootstrap.blendScale = saturate((surfaceBootstrapWeightSum - 1.0) / max((float)(surfaceBootstrapRadius * surfaceBootstrapRadius * 4), 1.0)) * varianceBlend;
        result.surfaceBootstrap.variance = spatialVariance * ComputeYoungHistoryVarianceMultiplier(surfaceYoungHistoryBoost);
        result.surfaceBootstrap.signal = surfaceBootstrapSum / surfaceBootstrapWeightSum;
        result.surfaceBootstrap.signal.a = currentSurface.a;
        result.surfaceBootstrap.shadowSignal = shadowBootstrapSum / surfaceBootstrapWeightSum;
    }

    return result;
}

float4 LoadPrevGuide0(uint guideKind, int2 pixel)
{
    if (guideKind == SIGNAL_GUIDE_SHADOW)
        return _PrevShadowGuide0.Load(int3(pixel, 0));
    if (guideKind == SIGNAL_GUIDE_REFLECTION)
        return _PrevReflectionGuide0.Load(int3(pixel, 0));
    if (guideKind == SIGNAL_GUIDE_TRANSMISSION)
        return _PrevTransmissionGuide0.Load(int3(pixel, 0));
    
    return _PrevGuide0.Load(int3(pixel, 0));
}

float4 LoadPrevGuide1(uint guideKind, int2 pixel)
{
    if (guideKind == SIGNAL_GUIDE_SHADOW)
        return _PrevShadowGuide1.Load(int3(pixel, 0));
    if (guideKind == SIGNAL_GUIDE_REFLECTION)
        return _PrevReflectionGuide1.Load(int3(pixel, 0));
    if (guideKind == SIGNAL_GUIDE_TRANSMISSION)
        return _PrevTransmissionGuide1.Load(int3(pixel, 0));
    
    return _PrevGuide1.Load(int3(pixel, 0));
}

float4 LoadPrevGuide2(uint guideKind, int2 pixel)
{
    if (guideKind == SIGNAL_GUIDE_SHADOW)
        return _PrevShadowGuide2.Load(int3(pixel, 0));
    if (guideKind == SIGNAL_GUIDE_REFLECTION)
        return _PrevReflectionGuide2.Load(int3(pixel, 0));
    if (guideKind == SIGNAL_GUIDE_TRANSMISSION)
        return _PrevTransmissionGuide2.Load(int3(pixel, 0));
    
    return _PrevGuide2.Load(int3(pixel, 0));
}

float4 LoadPrevSignal(uint signalKind, int2 pixel)
{
    if (signalKind == PATH_SIGNAL_REFLECTION)
        return _PrevReflectionSignal.Load(int3(pixel, 0));
    if (signalKind == PATH_SIGNAL_TRANSMISSION)
        return _PrevTransmissionSignal.Load(int3(pixel, 0));

    return _PrevSurfaceSignal.Load(int3(pixel, 0));
}

float4 LoadPrevSignalMoments(uint signalKind, int2 pixel)
{
    if (signalKind == PATH_SIGNAL_REFLECTION)
        return _PrevReflectionMoments.Load(int3(pixel, 0));
    if (signalKind == PATH_SIGNAL_TRANSMISSION)
        return _PrevTransmissionMoments.Load(int3(pixel, 0));

    return _PrevSurfaceMoments.Load(int3(pixel, 0));
}

void StoreSignalMoments(uint signalKind, uint2 pixel, float4 moments)
{
#if defined(LUCENT_ACCUMULATE_SPECULAR)
    if (signalKind == PATH_SIGNAL_REFLECTION)
    {
        _ReflectionMoments[pixel] = moments;
    }
    else if (signalKind == PATH_SIGNAL_TRANSMISSION)
    {
        _TransmissionMoments[pixel] = moments;
    }
#endif
#if defined(LUCENT_ACCUMULATE_SURFACE)
    if (signalKind == PATH_SIGNAL_SURFACE)
    {
        _SurfaceMoments[pixel] = moments;
    }
#endif
}

#if defined(LUCENT_ACCUMULATE_SURFACE)
void StoreCompositeMoments(uint2 pixel, float4 surfaceSignal, float4 surfaceMoments)
{
    float4 reflectionSignal = _ReflectionSignal[pixel];
    float4 transmissionSignal = _TransmissionSignal[pixel];
    float4 reflectionMoments = _ReflectionMoments[pixel];
    float4 transmissionMoments = _TransmissionMoments[pixel];

    float compositeMoment1 = ComputeLuminance(surfaceSignal.rgb + reflectionSignal.rgb + transmissionSignal.rgb);
    float compositeVariance = max(surfaceMoments.w + reflectionMoments.w + transmissionMoments.w, 0.0);
    float compositeMoment2 = compositeMoment1 * compositeMoment1 + compositeVariance;
    float compositeHistoryLength = max(surfaceMoments.z, max(reflectionMoments.z, transmissionMoments.z));
    _Moments[pixel] = float4(compositeMoment1, compositeMoment2, compositeHistoryLength, compositeVariance);
}
#endif


float4 BuildInitialMoments(float4 currentSignal)
{
    float currentLuminance = ComputeLuminance(currentSignal.rgb);
    return float4(currentLuminance, currentLuminance * currentLuminance, 1.0, 0.0);
}

bool BilinearReprojectHistory(
    uint guideKind,
    uint signalKind,
    float2 historyUv,
    float4 currentGuide0,
    float4 currentGuide1,
    float4 currentGuide2,
    out float4 historySignal,
    out float4 historyMoments,
    out float prevHitMask,
    out float relativeDepthDelta,
    out float normalAgreement,
    out int2 resolvedPrevPixel)
{
    historySignal = 0.0;
    historyMoments = 0.0;
    prevHitMask = 0.0;
    relativeDepthDelta = 1.0;
    normalAgreement = 0.0;

    float2 pixelCoord = historyUv * float2(_Width, _Height) - 0.5;
    int2 basePixel = int2(floor(pixelCoord));
    float2 frac = pixelCoord - float2(basePixel);
    resolvedPrevPixel = clamp(basePixel, int2(0, 0), int2(_Width - 1, _Height - 1));

    float bilinearWeights[4];
    bilinearWeights[0] = (1.0 - frac.x) * (1.0 - frac.y);
    bilinearWeights[1] = frac.x * (1.0 - frac.y);
    bilinearWeights[2] = (1.0 - frac.x) * frac.y;
    bilinearWeights[3] = frac.x * frac.y;
    int2 tapOffsets[4] = { int2(0, 0), int2(1, 0), int2(0, 1), int2(1, 1) };

    float4 signalSum = 0.0;
    float4 momentsSum = 0.0;
    float hitMaskSum = 0.0;
    float weightSum = 0.0;
    float bestWeight = -1.0;

    [unroll]
    for (int i = 0; i < 4; i++)
    {
        float weight = bilinearWeights[i];
        if (weight <= 0.0)
            continue;

        int2 tapPixel = clamp(basePixel + tapOffsets[i], int2(0, 0), int2(_Width - 1, _Height - 1));
        float4 tapGuide0 = LoadPrevGuide0(guideKind, tapPixel);
        float4 tapGuide1 = LoadPrevGuide1(guideKind, tapPixel);
        float4 tapGuide2 = LoadPrevGuide2(guideKind, tapPixel);

        float tapRelativeDepthDelta;
        float tapNormalAgreement;
        if (!ValidateHistory(guideKind, currentGuide0, currentGuide1, currentGuide2, tapGuide0, tapGuide1, tapGuide2, tapRelativeDepthDelta, tapNormalAgreement))
            continue;

        signalSum += weight * LoadPrevSignal(signalKind, tapPixel);
        momentsSum += weight * LoadPrevSignalMoments(signalKind, tapPixel);
        hitMaskSum += weight * _PrevHitMask.Load(int3(tapPixel, 0));
        weightSum += weight;

        if (weight > bestWeight)
        {
            bestWeight = weight;
            relativeDepthDelta = tapRelativeDepthDelta;
            normalAgreement = tapNormalAgreement;
            resolvedPrevPixel = tapPixel;
        }
    }

    if (weightSum <= 1e-4)
        return false;

    float invWeightSum = rcp(weightSum);
    historySignal = signalSum * invWeightSum;
    historyMoments = momentsSum * invWeightSum;
    prevHitMask = hitMaskSum * invWeightSum;
    return true;
}

bool ResolveSignalHistory(
    uint guideKind,
    uint signalKind,
    uint2 pixel,
    float4 currentGuide0,
    float4 currentGuide1,
    float4 currentGuide2,
    float4 currentSignal,
    inout float4 accumulatedSignal,
    out float4 previousSignal,
    inout float4 previousMoments,
    out float4 signalMoments,
    out float historyConfidence,
    out float effectivePrevHistoryLength,
    out float historyAlpha,
    out int2 resolvedPrevPixel,
    bool foldCurrentSample)
{
    previousSignal = currentSignal;
    previousMoments = BuildInitialMoments(currentSignal);
    signalMoments = BuildInitialMoments(currentSignal);
    historyConfidence = 0.0;
    effectivePrevHistoryLength = 0.0;
    historyAlpha = 1.0;
    resolvedPrevPixel = int2(pixel);
    accumulatedSignal = currentSignal;

    if (_HasValidHistory == 0)
        return false;

    float2 currentUv = GetCurrentUv(pixel);
    float2 prevUv = GetHistoryUv(currentUv, currentGuide1.xy);
    bool skyHistory = IsSkyGuide(currentGuide0);
    float2 historyUv = skyHistory && !IsUvInsideHistory(prevUv) ? currentUv : prevUv;

    if (!IsUvInsideHistory(prevUv) && !skyHistory)
        return false;

    float relativeDepthDelta = 1.0;
    float normalAgreement = 0.0;
    int2 bestHistoryPixel = int2(-1, -1);
    float4 historySignal;
    float4 historyMoments;
    float prevHitMaskForHistoryPixel;

    if (!BilinearReprojectHistory(
        guideKind, signalKind, historyUv,
        currentGuide0, currentGuide1, currentGuide2,
        historySignal, historyMoments, prevHitMaskForHistoryPixel,
        relativeDepthDelta, normalAgreement, bestHistoryPixel))
    {
        return false;
    }

    float prevHistoryLength = clamp(historyMoments.z, 0.0, (float)_MaxHistoryLength);
    float currentHitMask = saturate(currentSignal.a);

    float motionPixels = skyHistory ? 0.0 : length(currentGuide1.xy * float2(_Width, _Height));
    float depthConfidence = ComputeDepthConfidence(relativeDepthDelta);
    float normalConfidence = ComputeNormalConfidence(normalAgreement);
    float motionConfidence = ComputeMotionConfidence(motionPixels);

    historyConfidence = ApplyHistoryConfidenceCurve(
        pixel, signalKind,
        depthConfidence * normalConfidence * motionConfidence);

    if (signalKind == PATH_SIGNAL_SURFACE)
    {
        float hitMaskDelta = abs(currentHitMask - prevHitMaskForHistoryPixel);
        float hitMaskPenalty = 1.0f - hitMaskDelta;
        hitMaskPenalty = pow(hitMaskPenalty, 2.0f);
        historyConfidence *= hitMaskPenalty;

        float invSpp = rcp(max((float)_SamplesPerPixel, 1.0));
        int shadowRadius = _ShadowAntiLagRadius;
        float surfaceShadowSum = 0.0;
        float prevSurfaceShadowSum = 0.0;
        for (int sy = -shadowRadius; sy <= shadowRadius; ++sy)
        {
            for (int sx = -shadowRadius; sx <= shadowRadius; ++sx)
            {
                int2 neighborOffset = int2(sx, sy);
                surfaceShadowSum += saturate(_FrameShadowSum.Load(int3(int2(pixel) + neighborOffset, 0)).r * invSpp);
                prevSurfaceShadowSum += _PrevShadowHitMask.Load(int3(bestHistoryPixel + neighborOffset, 0));
            }
        }
        float shadowSampleCount = (float)((2 * shadowRadius + 1) * (2 * shadowRadius + 1));
        float currentShadowFraction = surfaceShadowSum / max(shadowSampleCount, 1.0);
        float prevAccumShadow = prevSurfaceShadowSum / max(shadowSampleCount, 1.0);
        float shadowDelta = abs(currentShadowFraction - prevAccumShadow);
        float shadowGate = saturate(prevHistoryLength * _ShadowAntiLagWarmup);
        historyConfidence *= lerp(1.0, pow(saturate(1.0 - shadowDelta), _ShadowAntiLagSensitivity), shadowGate);
    }
    else if (signalKind == PATH_SIGNAL_REFLECTION)
    {
        int shadowRadius = _ShadowAntiLagRadius;
        float shadowSum = 0.0;
        float prevShadowSum = 0.0;
        float shadowWeight = 0.0;
        for (int sy = -shadowRadius; sy <= shadowRadius; ++sy)
        {
            for (int sx = -shadowRadius; sx <= shadowRadius; ++sx)
            {
                int2 neighborOffset = int2(sx, sy);
                float enc = _FrameShadowSum.Load(int3(int2(pixel) + neighborOffset, 0)).g;
                if (ShadowVisibilitySampled(enc))
                {
                    shadowSum += DecodeShadowVisibility(enc);
                    prevShadowSum += _PrevReflectionShadowHitMask.Load(int3(bestHistoryPixel + neighborOffset, 0));
                    shadowWeight += 1.0;
                }
            }
        }
        if (shadowWeight > 0.0)
        {
            float shadowChangeExcess = ComputeSampledShadowChangeExcess(shadowSum, prevShadowSum, shadowWeight);
            float shadowGate = saturate(prevHistoryLength * _ShadowAntiLagWarmup);
            historyConfidence *= lerp(1.0, pow(1.0 - shadowChangeExcess, _ShadowAntiLagSensitivity), shadowGate);
        }
    }
    else if (signalKind == PATH_SIGNAL_TRANSMISSION)
    {
        int shadowRadius = _ShadowAntiLagRadius;
        float shadowSum = 0.0;
        float prevShadowSum = 0.0;
        float shadowWeight = 0.0;
        for (int sy = -shadowRadius; sy <= shadowRadius; ++sy)
        {
            for (int sx = -shadowRadius; sx <= shadowRadius; ++sx)
            {
                int2 neighborOffset = int2(sx, sy);
                float enc = _FrameShadowSum.Load(int3(int2(pixel) + neighborOffset, 0)).b;
                if (ShadowVisibilitySampled(enc))
                {
                    shadowSum += DecodeShadowVisibility(enc);
                    prevShadowSum += _PrevTransmissionShadowHitMask.Load(int3(bestHistoryPixel + neighborOffset, 0));
                    shadowWeight += 1.0;
                }
            }
        }
        if (shadowWeight > 0.0)
        {
            float shadowChangeExcess = ComputeSampledShadowChangeExcess(shadowSum, prevShadowSum, shadowWeight);
            float shadowGate = saturate(prevHistoryLength * _ShadowAntiLagWarmup);
            historyConfidence *= lerp(1.0, pow(1.0 - shadowChangeExcess, _ShadowAntiLagSensitivity), shadowGate);
        }
    }

    previousSignal = historySignal;
    previousMoments = historyMoments;

    effectivePrevHistoryLength = prevHistoryLength * historyConfidence;
    historyAlpha = foldCurrentSample ? rcp(effectivePrevHistoryLength + 1.0) : 0.0;

    accumulatedSignal = lerp(previousSignal, currentSignal, historyAlpha);

    float currentLuminance = ComputeLuminance(currentSignal.rgb);
    float moment1 = lerp(historyMoments.x, currentLuminance, historyAlpha);
    float moment2 = lerp(historyMoments.y, currentLuminance * currentLuminance, historyAlpha);
    float historyLength = foldCurrentSample ? min(effectivePrevHistoryLength + 1.0, (float)_MaxHistoryLength) : prevHistoryLength;
    float variance = max(moment2 - moment1 * moment1, 0.0);
    signalMoments = float4(moment1, moment2, historyLength, variance);

    resolvedPrevPixel = bestHistoryPixel;
    return true;
}

void AccumulateFrameBody(uint3 id)
{
    if (id.x >= (uint)_Width || id.y >= (uint)_Height)
        return;

    uint2 pixel = id.xy;
    int2 pixelInt = int2(pixel);
    bool foldCurrentSample = (_EnableCheckerboard == 0) || CheckerboardShadePixel(pixel);
    float invSampleCount = rcp(max((float)_SamplesPerPixel, 1.0));
    CurrentFrameSample currentSample = LoadCurrentFrameSample(pixelInt, invSampleCount);
    
    float4 currentSurface = currentSample.surface;
    float4 currentReflection = currentSample.reflection;
    float4 currentTransmission = currentSample.transmission;
    float currentHitMask = currentSample.hitMask;

    float4 currentGuide0 = _Guide0.Load(int3(pixel, 0));
    float4 currentGuide1 = _Guide1.Load(int3(pixel, 0));
    float4 currentGuide2 = _Guide2.Load(int3(pixel, 0));

    if (IsSkyGuide(currentGuide0))
    {
        float skyHistoryLength = (float)_MaxHistoryLength;
        float skyLuminance = ComputeLuminance(currentSurface.rgb);
        float4 skyConvergedYoungHistory = BuildYoungHistoryAtrousSignal(skyHistoryLength, 0.0, 1.0, 0.0);

        StoreSignalMoments(PATH_SIGNAL_SURFACE, pixel, float4(skyLuminance, skyLuminance * skyLuminance, skyHistoryLength, 0.0));
        StoreSignalMoments(PATH_SIGNAL_REFLECTION, pixel, float4(0.0, 0.0, skyHistoryLength, 0.0));
        StoreSignalMoments(PATH_SIGNAL_TRANSMISSION, pixel, float4(0.0, 0.0, skyHistoryLength, 0.0));

#if defined(LUCENT_ACCUMULATE_SURFACE)
        _SurfaceSignal[pixel] = currentSurface;
        _SurfaceYoungHistorySignal[pixel] = skyConvergedYoungHistory;
        _ShadowHitMask[pixel] = saturate(_FrameShadowSum.Load(int3(pixel, 0)).r * invSampleCount);
        _Moments[pixel] = float4(skyLuminance, skyLuminance * skyLuminance, skyHistoryLength, 0.0);
#endif
#if defined(LUCENT_ACCUMULATE_SPECULAR)
        _ReflectionSignal[pixel] = currentReflection;
        _TransmissionSignal[pixel] = currentTransmission;
        _ReflectionYoungHistorySignal[pixel] = skyConvergedYoungHistory;
        _TransmissionYoungHistorySignal[pixel] = skyConvergedYoungHistory;
        _ReflectionShadowHitMask[pixel] = 0.0;
        _TransmissionShadowHitMask[pixel] = 0.0;
#endif
        return;
    }

    float4 currentReflectionGuide0 = float4(0.0, 0.0, 0.0, 0.0);
    float4 currentReflectionGuide1 = float4(0.0, 0.0, 0.0, 0.0);
    float4 currentReflectionGuide2 = float4(0.0, 0.0, 0.0, 0.0);
    float4 currentTransmissionGuide0 = float4(0.0, 0.0, 0.0, 0.0);
    float4 currentTransmissionGuide1 = float4(0.0, 0.0, 0.0, 0.0);
    float4 currentTransmissionGuide2 = float4(0.0, 0.0, 0.0, 0.0);
    if (_UseSecondaryCompaction == 0)
    {
        currentReflectionGuide0 = _ReflectionGuide0.Load(int3(pixel, 0));
        currentReflectionGuide1 = _ReflectionGuide1.Load(int3(pixel, 0));
        currentReflectionGuide2 = _ReflectionGuide2.Load(int3(pixel, 0));
        currentTransmissionGuide0 = _TransmissionGuide0.Load(int3(pixel, 0));
        currentTransmissionGuide1 = _TransmissionGuide1.Load(int3(pixel, 0));
        currentTransmissionGuide2 = _TransmissionGuide2.Load(int3(pixel, 0));
    }

    float4 accumulatedSurface = currentSurface;
    float4 accumulatedReflection = currentReflection;
    float4 accumulatedTransmission = currentTransmission;
    float accumulatedHitMask = currentHitMask;

    float4 previousSurface = currentSurface;
    float4 previousReflection = currentReflection;
    float4 previousTransmission = currentTransmission;
    float4 surfaceMoments = BuildInitialMoments(currentSurface);
    float4 reflectionMoments = BuildInitialMoments(currentReflection);
    float4 transmissionMoments = BuildInitialMoments(currentTransmission);
    float surfaceHistoryConfidence = 0.0;
    float reflectionHistoryConfidence = 0.0;
    float transmissionHistoryConfidence = 0.0;
    float surfaceEffectivePrevHistoryLength = 0.0;
    float reflectionEffectivePrevHistoryLength = 0.0;
    float transmissionEffectivePrevHistoryLength = 0.0;
    float surfaceHistoryAlpha = 1.0;
    float reflectionHistoryAlpha = 1.0;
    float transmissionHistoryAlpha = 1.0;
    int2 surfacePrevPixel = pixelInt;
    int2 reflectionPrevPixel = pixelInt;
    int2 transmissionPrevPixel = pixelInt;

    bool surfaceHistoryAccepted = ResolveSignalHistory(
        SIGNAL_GUIDE_SURFACE,
        PATH_SIGNAL_SURFACE,
        pixel,
        currentGuide0,
        currentGuide1,
        currentGuide2,
        currentSurface,
        accumulatedSurface,
        previousSurface,
        surfaceMoments,
        surfaceMoments,
        surfaceHistoryConfidence,
        surfaceEffectivePrevHistoryLength,
        surfaceHistoryAlpha,
        surfacePrevPixel,
        foldCurrentSample);

    bool reflectionHistoryAccepted = false;
    if (_UseSecondaryCompaction == 0 && (!foldCurrentSample || HasValidSecondaryIdentity(currentReflectionGuide1) || Max3(currentReflection.rgb) > 0.0))
    {
        reflectionHistoryAccepted = ResolveSignalHistory(
            SIGNAL_GUIDE_REFLECTION,
            PATH_SIGNAL_REFLECTION,
            pixel,
            currentReflectionGuide0,
            currentReflectionGuide1,
            currentReflectionGuide2,
            currentReflection,
            accumulatedReflection,
            previousReflection,
            reflectionMoments,
            reflectionMoments,
            reflectionHistoryConfidence,
            reflectionEffectivePrevHistoryLength,
            reflectionHistoryAlpha,
            reflectionPrevPixel,
            foldCurrentSample);
    }

    bool transmissionHistoryAccepted = false;
    if (_UseSecondaryCompaction == 0 && (!foldCurrentSample || HasValidSecondaryIdentity(currentTransmissionGuide1) || Max3(currentTransmission.rgb) > 0.0))
    {
        transmissionHistoryAccepted = ResolveSignalHistory(
            SIGNAL_GUIDE_TRANSMISSION,
            PATH_SIGNAL_TRANSMISSION,
            pixel,
            currentTransmissionGuide0,
            currentTransmissionGuide1,
            currentTransmissionGuide2,
            currentTransmission,
            accumulatedTransmission,
            previousTransmission,
            transmissionMoments,
            transmissionMoments,
            transmissionHistoryConfidence,
            transmissionEffectivePrevHistoryLength,
            transmissionHistoryAlpha,
            transmissionPrevPixel,
            foldCurrentSample);
    }

    if (surfaceHistoryAccepted)
    {
        float prevAccumulatedHitMask = _PrevHitMask.Load(int3(surfacePrevPixel, 0));
        accumulatedHitMask = lerp(prevAccumulatedHitMask, currentHitMask, surfaceHistoryAlpha);
    }

    float currentShadowFractionFrame = saturate(_FrameShadowSum.Load(int3(pixel, 0)).r * invSampleCount);
    float accumulatedShadowMask = currentShadowFractionFrame;
    if (surfaceHistoryAccepted)
    {
        float prevAccumShadowMask = _PrevShadowHitMask.Load(int3(surfacePrevPixel, 0));
        float shadowAccumAlpha = foldCurrentSample ? max(rcp(max((float)_MaxHistoryLength, 1.0)), 0.1) : 0.0;
        accumulatedShadowMask = lerp(prevAccumShadowMask, currentShadowFractionFrame, shadowAccumAlpha);
    }
#if defined(LUCENT_ACCUMULATE_SURFACE)
    _ShadowHitMask[pixel] = accumulatedShadowMask;
#endif

    float surfaceYoungHistoryBoost = 0.0;
    int surfaceVarianceRadius = 0;
    bool useSurfaceVarianceFallback = TryGetSpatialVarianceFallbackParameters(surfaceMoments, surfaceYoungHistoryBoost, surfaceVarianceRadius);

    float reflectionYoungHistoryBoost = 0.0;
    int reflectionVarianceRadius = 0;
    bool useReflectionVarianceFallback = TryGetSpatialVarianceFallbackParameters(reflectionMoments, reflectionYoungHistoryBoost, reflectionVarianceRadius);

    float transmissionYoungHistoryBoost = 0.0;
    int transmissionVarianceRadius = 0;
    bool useTransmissionVarianceFallback = TryGetSpatialVarianceFallbackParameters(transmissionMoments, transmissionYoungHistoryBoost, transmissionVarianceRadius);

    int surfaceBootstrapRadius = ComputeYoungHistoryRadius(clamp(_SpatialBootstrapRadius, 0, 3), surfaceYoungHistoryBoost);
    float surfaceSpatialBlend = 0.0;

    if (_UseSecondaryCompaction == 0)
    {
        SpatialNeighborhoodStats spatialNeighborhood = ComputeSpatialNeighborhoodStats(
            pixelInt,
            invSampleCount,
            currentSurface,
            currentGuide0,
            currentGuide1,
            currentGuide2,
            surfaceYoungHistoryBoost,
            surfaceMoments.x,
            surfaceBootstrapRadius,
            useSurfaceVarianceFallback ? surfaceVarianceRadius : 0,
            currentReflectionGuide0,
            currentReflectionGuide1,
            currentReflectionGuide2,
            reflectionMoments.x,
            useReflectionVarianceFallback ? reflectionVarianceRadius : 0,
            currentTransmissionGuide0,
            currentTransmissionGuide1,
            currentTransmissionGuide2,
            transmissionMoments.x,
            useTransmissionVarianceFallback ? transmissionVarianceRadius : 0);

        ApplySpatialVarianceFallback(
            surfaceYoungHistoryBoost,
            spatialNeighborhood.surfaceVariance.supportWeight,
            spatialNeighborhood.surfaceVariance.variance,
            surfaceMoments);

        ApplySpatialVarianceFallback(
            reflectionYoungHistoryBoost,
            spatialNeighborhood.reflectionVariance.supportWeight,
            spatialNeighborhood.reflectionVariance.variance,
            reflectionMoments);

        ApplySpatialVarianceFallback(
            transmissionYoungHistoryBoost,
            spatialNeighborhood.transmissionVariance.supportWeight,
            spatialNeighborhood.transmissionVariance.variance,
            transmissionMoments);

        PreserveRepairUncertainty(
            reflectionMoments,
            ComputeLuminance(accumulatedReflection.rgb),
            0.0,
            reflectionMoments.w,
            ComputeRecoveryBoost(reflectionMoments.z, 0.0, reflectionHistoryConfidence));

        PreserveRepairUncertainty(
            transmissionMoments,
            ComputeLuminance(accumulatedTransmission.rgb),
            0.0,
            transmissionMoments.w,
            ComputeRecoveryBoost(transmissionMoments.z, 0.0, transmissionHistoryConfidence));

        float surfaceSpatialBlendScale = spatialNeighborhood.surfaceBootstrap.blendScale;
        float surfaceSpatialVariance = spatialNeighborhood.surfaceBootstrap.variance;
        float4 spatialSurface = spatialNeighborhood.surfaceBootstrap.signal;

        float temporalMissAmount = surfaceHistoryAccepted ? saturate(1.0 - surfaceHistoryConfidence) : 1.0;
        float surfaceBootstrapBlend = lerp(saturate(_HistoryMissSpatialBlend), saturate(_YoungHistoryBlend), surfaceYoungHistoryBoost);
        surfaceSpatialBlend = surfaceBootstrapBlend * temporalMissAmount * surfaceSpatialBlendScale;
        if (surfaceSpatialBlend > 0.0)
        {
            float preBootstrapLuminance = ComputeLuminance(accumulatedSurface.rgb);
            accumulatedSurface = lerp(accumulatedSurface, spatialSurface, surfaceSpatialBlend);
            float accumulatedLuminance = ComputeLuminance(accumulatedSurface.rgb);
            float surfaceRecoveryBoost = ComputeRecoveryBoost(surfaceMoments.z, surfaceSpatialBlend, surfaceHistoryConfidence);
            float repairResidual = abs(accumulatedLuminance - preBootstrapLuminance);
            float variance = max(
                surfaceMoments.w,
                ComputeRepairUncertaintyVarianceFloor(
                    accumulatedLuminance,
                    repairResidual,
                    surfaceSpatialVariance * max(surfaceSpatialBlend, 1e-4),
                    surfaceRecoveryBoost));
            surfaceMoments = float4(
                accumulatedLuminance,
                accumulatedLuminance * accumulatedLuminance + variance,
                surfaceMoments.z,
                variance);
        }
        else
        {
            PreserveRepairUncertainty(
                surfaceMoments,
                ComputeLuminance(accumulatedSurface.rgb),
                0.0,
                surfaceMoments.w,
                ComputeRecoveryBoost(surfaceMoments.z, 0.0, surfaceHistoryConfidence));
        }

        if (surfaceSpatialBlend > 0.0)
        {
            accumulatedShadowMask = lerp(accumulatedShadowMask, spatialNeighborhood.surfaceBootstrap.shadowSignal, surfaceSpatialBlend);
#if defined(LUCENT_ACCUMULATE_SURFACE)
            _ShadowHitMask[pixel] = accumulatedShadowMask;
#endif
        }
    }
    else
    {
        float temporalMissAmount = surfaceHistoryAccepted ? saturate(1.0 - surfaceHistoryConfidence) : 1.0;
        float surfaceBootstrapBlend = lerp(saturate(_HistoryMissSpatialBlend), saturate(_YoungHistoryBlend), surfaceYoungHistoryBoost);
        bool surfaceBootstrapMiss = surfaceBootstrapRadius > 0 &&
            (!surfaceHistoryAccepted ||
             surfaceYoungHistoryBoost > 0.0 ||
             surfaceBootstrapBlend * temporalMissAmount > _BootstrapMissThreshold);
#if defined(LUCENT_ACCUMULATE_SURFACE)
        uint bootstrapSlot = LucentWaveAppendSlot(_BootstrapDispatchArgs, 7u, surfaceBootstrapMiss);

        if (surfaceBootstrapMiss)
        {
            _BootstrapActiveIndices[bootstrapSlot] = pixel.y * (uint)_Width + pixel.x;
        }
#endif

        PreserveRepairUncertainty(
            surfaceMoments,
            ComputeLuminance(accumulatedSurface.rgb),
            0.0,
            surfaceMoments.w,
            ComputeRecoveryBoost(surfaceMoments.z, 0.0, surfaceHistoryConfidence));
    }

    StoreSignalMoments(PATH_SIGNAL_SURFACE, pixel, surfaceMoments);
    StoreSignalMoments(PATH_SIGNAL_REFLECTION, pixel, reflectionMoments);
    StoreSignalMoments(PATH_SIGNAL_TRANSMISSION, pixel, transmissionMoments);

#if defined(LUCENT_ACCUMULATE_SPECULAR)
    float secondaryShadowAlpha = foldCurrentSample ? max(rcp(max((float)_MaxHistoryLength, 1.0)), 0.1) : 0.0;
    _ReflectionShadowHitMask[pixel] = BlendShadowVisibility(_FrameShadowSum.Load(int3(pixel, 0)).g, reflectionHistoryAccepted ? _PrevReflectionShadowHitMask.Load(int3(reflectionPrevPixel, 0)) : 0.0, reflectionHistoryAccepted, secondaryShadowAlpha);
    _TransmissionShadowHitMask[pixel] = BlendShadowVisibility(_FrameShadowSum.Load(int3(pixel, 0)).b, transmissionHistoryAccepted ? _PrevTransmissionShadowHitMask.Load(int3(transmissionPrevPixel, 0)) : 0.0, transmissionHistoryAccepted, secondaryShadowAlpha);
    _ReflectionSignal[pixel] = accumulatedReflection;
    _TransmissionSignal[pixel] = accumulatedTransmission;
    _ReflectionYoungHistorySignal[pixel] = BuildYoungHistoryAtrousSignal(reflectionMoments.z, 0.0, reflectionHistoryConfidence, reflectionMoments.w);
    _TransmissionYoungHistorySignal[pixel] = BuildYoungHistoryAtrousSignal(transmissionMoments.z, 0.0, transmissionHistoryConfidence, transmissionMoments.w);
#endif
#if defined(LUCENT_ACCUMULATE_SURFACE)
    _SurfaceSignal[pixel] = accumulatedSurface;
    _SurfaceYoungHistorySignal[pixel] = BuildYoungHistoryAtrousSignal(surfaceMoments.z, surfaceSpatialBlend, surfaceHistoryConfidence, surfaceMoments.w);
    StoreCompositeMoments(pixel, accumulatedSurface, surfaceMoments);
#endif
}

[numthreads(8, 8, 1)]
void CSAccumulateFrame(uint3 id : SV_DispatchThreadID)
{
    AccumulateFrameBody(id);
}

[numthreads(8, 8, 1)]
void CSAccumulateFrameSpecular(uint3 id : SV_DispatchThreadID)
{
    AccumulateFrameBody(id);
}

#if defined(LUCENT_ACCUMULATE_SPECULAR)

void AccumulateSecondaryCompacted(uint2 pixel)
{
    int2 pixelInt = int2(pixel);
    float invSampleCount = rcp(max((float)_SamplesPerPixel, 1.0));
    CurrentFrameSample currentSample = LoadCurrentFrameSample(pixelInt, invSampleCount);

    float4 currentReflection = currentSample.reflection;
    float4 currentTransmission = currentSample.transmission;

    float4 currentReflectionGuide0 = _ReflectionGuide0.Load(int3(pixel, 0));
    float4 currentReflectionGuide1 = _ReflectionGuide1.Load(int3(pixel, 0));
    float4 currentReflectionGuide2 = _ReflectionGuide2.Load(int3(pixel, 0));
    float4 currentTransmissionGuide0 = _TransmissionGuide0.Load(int3(pixel, 0));
    float4 currentTransmissionGuide1 = _TransmissionGuide1.Load(int3(pixel, 0));
    float4 currentTransmissionGuide2 = _TransmissionGuide2.Load(int3(pixel, 0));

    float4 accumulatedReflection = currentReflection;
    float4 accumulatedTransmission = currentTransmission;
    float4 previousReflection = currentReflection;
    float4 previousTransmission = currentTransmission;
    float4 reflectionMoments = BuildInitialMoments(currentReflection);
    float4 transmissionMoments = BuildInitialMoments(currentTransmission);
    float reflectionHistoryConfidence = 0.0;
    float transmissionHistoryConfidence = 0.0;
    float reflectionEffectivePrevHistoryLength = 0.0;
    float transmissionEffectivePrevHistoryLength = 0.0;
    float reflectionHistoryAlpha = 1.0;
    float transmissionHistoryAlpha = 1.0;
    int2 reflectionPrevPixel = pixelInt;
    int2 transmissionPrevPixel = pixelInt;

    bool reflectionHistoryAccepted = ResolveSignalHistory(
        SIGNAL_GUIDE_REFLECTION,
        PATH_SIGNAL_REFLECTION,
        pixel,
        currentReflectionGuide0,
        currentReflectionGuide1,
        currentReflectionGuide2,
        currentReflection,
        accumulatedReflection,
        previousReflection,
        reflectionMoments,
        reflectionMoments,
        reflectionHistoryConfidence,
        reflectionEffectivePrevHistoryLength,
        reflectionHistoryAlpha,
        reflectionPrevPixel,
        true);

    bool transmissionHistoryAccepted = ResolveSignalHistory(
        SIGNAL_GUIDE_TRANSMISSION,
        PATH_SIGNAL_TRANSMISSION,
        pixel,
        currentTransmissionGuide0,
        currentTransmissionGuide1,
        currentTransmissionGuide2,
        currentTransmission,
        accumulatedTransmission,
        previousTransmission,
        transmissionMoments,
        transmissionMoments,
        transmissionHistoryConfidence,
        transmissionEffectivePrevHistoryLength,
        transmissionHistoryAlpha,
        transmissionPrevPixel,
        true);

    float reflectionYoungHistoryBoost = 0.0;
    int reflectionVarianceRadius = 0;
    bool useReflectionVarianceFallback = TryGetSpatialVarianceFallbackParameters(reflectionMoments, reflectionYoungHistoryBoost, reflectionVarianceRadius);

    float transmissionYoungHistoryBoost = 0.0;
    int transmissionVarianceRadius = 0;
    bool useTransmissionVarianceFallback = TryGetSpatialVarianceFallbackParameters(transmissionMoments, transmissionYoungHistoryBoost, transmissionVarianceRadius);

    float4 zeroGuide = float4(0.0, 0.0, 0.0, 0.0);
    SpatialNeighborhoodStats spatialNeighborhood = ComputeSpatialNeighborhoodStats(
        pixelInt,
        invSampleCount,
        zeroGuide,
        zeroGuide,
        zeroGuide,
        zeroGuide,
        0.0,
        0.0,
        0,
        0,
        currentReflectionGuide0,
        currentReflectionGuide1,
        currentReflectionGuide2,
        reflectionMoments.x,
        useReflectionVarianceFallback ? reflectionVarianceRadius : 0,
        currentTransmissionGuide0,
        currentTransmissionGuide1,
        currentTransmissionGuide2,
        transmissionMoments.x,
        useTransmissionVarianceFallback ? transmissionVarianceRadius : 0);

    ApplySpatialVarianceFallback(
        reflectionYoungHistoryBoost,
        spatialNeighborhood.reflectionVariance.supportWeight,
        spatialNeighborhood.reflectionVariance.variance,
        reflectionMoments);

    ApplySpatialVarianceFallback(
        transmissionYoungHistoryBoost,
        spatialNeighborhood.transmissionVariance.supportWeight,
        spatialNeighborhood.transmissionVariance.variance,
        transmissionMoments);

    PreserveRepairUncertainty(
        reflectionMoments,
        ComputeLuminance(accumulatedReflection.rgb),
        0.0,
        reflectionMoments.w,
        ComputeRecoveryBoost(reflectionMoments.z, 0.0, reflectionHistoryConfidence));

    PreserveRepairUncertainty(
        transmissionMoments,
        ComputeLuminance(accumulatedTransmission.rgb),
        0.0,
        transmissionMoments.w,
        ComputeRecoveryBoost(transmissionMoments.z, 0.0, transmissionHistoryConfidence));

    StoreSignalMoments(PATH_SIGNAL_REFLECTION, pixel, reflectionMoments);
    StoreSignalMoments(PATH_SIGNAL_TRANSMISSION, pixel, transmissionMoments);
    float secondaryShadowAlpha = max(rcp(max((float)_MaxHistoryLength, 1.0)), 0.1);
    _ReflectionShadowHitMask[pixel] = BlendShadowVisibility(_FrameShadowSum.Load(int3(pixel, 0)).g, reflectionHistoryAccepted ? _PrevReflectionShadowHitMask.Load(int3(reflectionPrevPixel, 0)) : 0.0, reflectionHistoryAccepted, secondaryShadowAlpha);
    _TransmissionShadowHitMask[pixel] = BlendShadowVisibility(_FrameShadowSum.Load(int3(pixel, 0)).b, transmissionHistoryAccepted ? _PrevTransmissionShadowHitMask.Load(int3(transmissionPrevPixel, 0)) : 0.0, transmissionHistoryAccepted, secondaryShadowAlpha);
    _ReflectionSignal[pixel] = accumulatedReflection;
    _TransmissionSignal[pixel] = accumulatedTransmission;
    _ReflectionYoungHistorySignal[pixel] = BuildYoungHistoryAtrousSignal(reflectionMoments.z, 0.0, reflectionHistoryConfidence, reflectionMoments.w);
    _TransmissionYoungHistorySignal[pixel] = BuildYoungHistoryAtrousSignal(transmissionMoments.z, 0.0, transmissionHistoryConfidence, transmissionMoments.w);
}
#endif

#if defined(LUCENT_ACCUMULATE_PRE_PASS)
static const float REFLECTION_PRE_PASS_LOBE_VOLUME = .3;
static const float REFLECTION_PRE_PASS_NORMAL_LOBE_VOLUME = .75;
static const float REFLECTION_PRE_PASS_LOBE_ANGLE_FRACTION = .15;
static const float REFLECTION_PRE_PASS_ACCUMULATION_SPEED = 1.0 / 11.0;
static const float REFLECTION_PRE_PASS_FRACTION_SCALE = 2.0;
static const float REFLECTION_PRE_PASS_PLANE_DISTANCE_SENSITIVITY = .02;
static const float REFLECTION_PRE_PASS_ROUGHNESS_FRACTION = .15;
static const float REFLECTION_PRE_PASS_ROUGHNESS_SENSITIVITY = .01;
static const float REFLECTION_PRE_PASS_MIN_RADIUS = 1.0;
static const float REFLECTION_PRE_PASS_EXPONENTIAL_WEIGHT_SCALE = 3.0;
static const float3 REFLECTION_PRE_PASS_KERNEL[8] =
{
    float3(-1.0, 0.0, 1.0),
    float3(0.0, 1.0, 1.0),
    float3(1.0, 0.0, 1.0),
    float3(0.0, -1.0, 1.0),
    float3(-.35355339, .35355339, .5),
    float3(.35355339, .35355339, .5),
    float3(.35355339, -.35355339, .5),
    float3(-.35355339, -.35355339, .5)
};

float GetSpecularBlurRadiusCurve(float roughness)
{
    return (1.0 - exp2(-200.0 * roughness * roughness)) * sqrt(saturate(roughness));
}

float GetExponentialFalloffWeight(float x)
{
    float scaled = -REFLECTION_PRE_PASS_EXPONENTIAL_WEIGHT_SCALE * abs(x);

    return rcp(scaled * scaled - scaled + 1.0);
}

float3 GetGuidePrimaryPosition(uint2 pixel, out float3 rayDirection)
{
    Ray guideRay = GenerateGuideCameraRay(pixel);
    rayDirection = guideRay.direction;

    return guideRay.origin + guideRay.direction * max(_PrimaryHitDistance.Load(int3(pixel, 0)), 0.0);
}

float GetGuidePixelAngle(uint2 pixel)
{
    uint2 neighborPixel = uint2(pixel.x, pixel.y + 1u < (uint)_Height ? pixel.y + 1u : pixel.y - 1u);
    float3 centerDirection = GenerateGuideCameraRay(pixel).direction;
    float3 neighborDirection = GenerateGuideCameraRay(neighborPixel).direction;

    return max(length(centerDirection - neighborDirection), 1e-6);
}

bool IsReflectionPrePassPixel(float4 reflectionGuide0, float4 reflectionGuide2)
{
    return IsSkyGuide(reflectionGuide0) == false && abs(reflectionGuide2.x - GUIDE_PATH_KIND_SPECULAR) < .5;
}

[numthreads(8, 8, 1)]
void CSReflectionPrePass(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= (uint)_Width || id.y >= (uint)_Height)
    {
        return;
    }

    uint2 pixel = id.xy;
    float4 centerSignal = _FrameReflectionSignalSum.Load(int3(pixel, 0));
    float4 centerReflectionGuide0 = _ReflectionGuide0.Load(int3(pixel, 0));
    float4 centerReflectionGuide2 = _ReflectionGuide2.Load(int3(pixel, 0));
    float4 centerGuide0 = _Guide0.Load(int3(pixel, 0));

    if (_ReflectionPrePassRadius <= 0.0 || IsSkyGuide(centerGuide0) || IsReflectionPrePassPixel(centerReflectionGuide0, centerReflectionGuide2) == false)
    {
        _ReflectionPrePassSignal[pixel] = centerSignal;
        return;
    }

    float roughness = saturate(_PrimaryRoughness.Load(int3(pixel, 0)));
    float blurRadiusCurve = GetSpecularBlurRadiusCurve(roughness);
    float3 centerNormal = SafeNormalize(centerGuide0.xyz, float3(0.0, 1.0, 0.0));
    float3 rayDirection;
    float3 centerPosition = GetGuidePrimaryPosition(pixel, rayDirection);
    float primaryDistance = max(_PrimaryHitDistance.Load(int3(pixel, 0)), 1e-4);
    float hitDistance = max(_ReflectionHitDistance.Load(int3(pixel, 0)), 0.0);
    float pixelAngle = GetGuidePixelAngle(pixel);
    float frustumSize = (float)min(_Width, _Height) * pixelAngle * primaryDistance;
    float3 reflectedDirection = reflect(rayDirection, centerNormal);
    float normalDotReflected = abs(dot(centerNormal, reflectedDirection));

    float hitDistanceFactor = saturate(hitDistance / max(frustumSize, 1e-6));
    float blurRadius = sqrt(hitDistanceFactor) * _ReflectionPrePassRadius * blurRadiusCurve;
    blurRadius = max(blurRadius, REFLECTION_PRE_PASS_MIN_RADIUS * blurRadiusCurve);

    float worldLobeRadius = hitDistance * normalDotReflected * GetSpecularLobeTanHalfAngle(roughness, REFLECTION_PRE_PASS_LOBE_VOLUME);
    float lobeRadius = worldLobeRadius / max(pixelAngle * (primaryDistance + hitDistance), 1e-6);
    blurRadius = min(blurRadius, lobeRadius);

    if (blurRadius < 1.0)
    {
        _ReflectionPrePassSignal[pixel] = centerSignal;
        return;
    }

    float normalLobeVolume = REFLECTION_PRE_PASS_NORMAL_LOBE_VOLUME * lerp(REFLECTION_PRE_PASS_LOBE_ANGLE_FRACTION, 1.0, REFLECTION_PRE_PASS_ACCUMULATION_SPEED);
    float normalAngleLimit = max(atan(GetSpecularLobeTanHalfAngle(roughness, normalLobeVolume)), SPECULAR_LOBE_DIRECTION_FLOOR);
    float normalWeightScale = rcp(normalAngleLimit) / REFLECTION_PRE_PASS_FRACTION_SCALE;
    float planeDistanceScale = rcp(REFLECTION_PRE_PASS_PLANE_DISTANCE_SENSITIVITY * max(frustumSize, 1e-6));
    float roughnessWeightScale = rcp(lerp(REFLECTION_PRE_PASS_ROUGHNESS_SENSITIVITY, 1.0, saturate(roughness * REFLECTION_PRE_PASS_ROUGHNESS_FRACTION * REFLECTION_PRE_PASS_FRACTION_SCALE)));
    float contactRelaxation = smoothstep(.5, 1.0, roughness);

    float rotationAngle = GetWeyl1D(.5, (uint)_FrameIndex) * 6.28318531;
    float2 rotation = float2(cos(rotationAngle), sin(rotationAngle));

    float4 sum = centerSignal;
    float weightSum = 1.0;

    [unroll]
    for (int tapIndex = 0; tapIndex < 8; tapIndex++)
    {
        float3 tap = REFLECTION_PRE_PASS_KERNEL[tapIndex];
        float2 rotatedOffset = float2(tap.x * rotation.x - tap.y * rotation.y, tap.x * rotation.y + tap.y * rotation.x) * blurRadius;
        int2 samplePixelInt = int2(floor(float2(pixel) + .5 + rotatedOffset));

        if (samplePixelInt.x < 0 || samplePixelInt.y < 0 || samplePixelInt.x >= _Width || samplePixelInt.y >= _Height)
        {
            continue;
        }

        uint2 samplePixel = uint2(samplePixelInt);
        float4 sampleGuide0 = _Guide0.Load(int3(samplePixel, 0));
        float4 sampleReflectionGuide0 = _ReflectionGuide0.Load(int3(samplePixel, 0));
        float4 sampleReflectionGuide2 = _ReflectionGuide2.Load(int3(samplePixel, 0));

        if (IsSkyGuide(sampleGuide0) || IsReflectionPrePassPixel(sampleReflectionGuide0, sampleReflectionGuide2) == false)
        {
            continue;
        }

        float3 sampleRayDirection;
        float3 samplePosition = GetGuidePrimaryPosition(samplePixel, sampleRayDirection);
        float3 sampleNormal = SafeNormalize(sampleGuide0.xyz, centerNormal);
        float sampleRoughness = saturate(_PrimaryRoughness.Load(int3(samplePixel, 0)));
        float sampleHitDistance = max(_ReflectionHitDistance.Load(int3(samplePixel, 0)), 0.0);

        float normalAngle = acos(clamp(dot(centerNormal, sampleNormal), -1.0, 1.0));
        float planeDistance = dot(centerNormal, samplePosition - centerPosition);
        float relativeHitDistance = abs(sampleHitDistance - hitDistance) / max(max(sampleHitDistance, hitDistance), 1e-4);
        float sampleDistance = length(samplePosition - centerPosition) + 1e-6;
        float contactWeight = lerp(saturate(sampleHitDistance / (sampleDistance + hitDistance)), 1.0, contactRelaxation);

        float weight = exp(-.66 * tap.z * tap.z);
        weight *= GetLinearFalloffWeight(normalAngle * normalWeightScale);
        weight *= GetLinearFalloffWeight((sampleRoughness - roughness) * roughnessWeightScale);
        weight *= GetLinearFalloffWeight(planeDistance * planeDistanceScale);
        weight *= contactWeight;
        weight *= GetExponentialFalloffWeight(relativeHitDistance / REFLECTION_PRE_PASS_ACCUMULATION_SPEED);

        if (weight <= 0.0)
        {
            continue;
        }

        sum += _FrameReflectionSignalSum.Load(int3(samplePixel, 0)) * weight;
        weightSum += weight;
    }

    float4 filtered = sum / weightSum;
    filtered.a = centerSignal.a;
    _ReflectionPrePassSignal[pixel] = filtered;
}

#endif

#if defined(LUCENT_ACCUMULATE_SPECULAR)
[numthreads(LUCENT_TEMPORAL_COMPACTED_GROUP_SIZE, 1, 1)]
void CSAccumulateSecondary(uint3 id : SV_DispatchThreadID)
{
    uint index = id.x + id.y * (_SecondaryDispatchInfo[0] * LUCENT_TEMPORAL_COMPACTED_GROUP_SIZE);
    if (index >= _SecondaryDispatchInfo[3])
        return;

    uint pixelIndex = _ActiveSecondaryIndices[index];
    uint2 pixel = uint2(pixelIndex % (uint)_Width, pixelIndex / (uint)_Width);
    AccumulateSecondaryCompacted(pixel);
}
#endif

#if defined(LUCENT_ACCUMULATE_SURFACE)

void BootstrapSurfaceCompacted(uint2 pixel)
{
    int2 pixelInt = int2(pixel);
    float invSampleCount = rcp(max((float)_SamplesPerPixel, 1.0));
    CurrentFrameSample currentSample = LoadCurrentFrameSample(pixelInt, invSampleCount);
    float4 currentSurface = currentSample.surface;

    float4 currentGuide0 = _Guide0.Load(int3(pixel, 0));
    float4 currentGuide1 = _Guide1.Load(int3(pixel, 0));
    float4 currentGuide2 = _Guide2.Load(int3(pixel, 0));

    float4 accumulatedSurface = currentSurface;
    float4 previousSurface = currentSurface;
    float4 surfaceMoments = BuildInitialMoments(currentSurface);
    float surfaceHistoryConfidence = 0.0;
    float surfaceEffectivePrevHistoryLength = 0.0;
    float surfaceHistoryAlpha = 1.0;
    int2 surfacePrevPixel = pixelInt;

    bool surfaceHistoryAccepted = ResolveSignalHistory(
        SIGNAL_GUIDE_SURFACE,
        PATH_SIGNAL_SURFACE,
        pixel,
        currentGuide0,
        currentGuide1,
        currentGuide2,
        currentSurface,
        accumulatedSurface,
        previousSurface,
        surfaceMoments,
        surfaceMoments,
        surfaceHistoryConfidence,
        surfaceEffectivePrevHistoryLength,
        surfaceHistoryAlpha,
        surfacePrevPixel,
        true);

    float surfaceYoungHistoryBoost = 0.0;
    int surfaceVarianceRadius = 0;
    bool useSurfaceVarianceFallback = TryGetSpatialVarianceFallbackParameters(surfaceMoments, surfaceYoungHistoryBoost, surfaceVarianceRadius);

    int surfaceBootstrapRadius = ComputeYoungHistoryRadius(clamp(_SpatialBootstrapRadius, 0, 3), surfaceYoungHistoryBoost);

    float4 zeroGuide = float4(0.0, 0.0, 0.0, 0.0);
    SpatialNeighborhoodStats spatialNeighborhood = ComputeSpatialNeighborhoodStats(
        pixelInt,
        invSampleCount,
        currentSurface,
        currentGuide0,
        currentGuide1,
        currentGuide2,
        surfaceYoungHistoryBoost,
        surfaceMoments.x,
        surfaceBootstrapRadius,
        useSurfaceVarianceFallback ? surfaceVarianceRadius : 0,
        zeroGuide,
        zeroGuide,
        zeroGuide,
        0.0,
        0,
        zeroGuide,
        zeroGuide,
        zeroGuide,
        0.0,
        0);

    ApplySpatialVarianceFallback(
        surfaceYoungHistoryBoost,
        spatialNeighborhood.surfaceVariance.supportWeight,
        spatialNeighborhood.surfaceVariance.variance,
        surfaceMoments);

    float surfaceSpatialBlendScale = spatialNeighborhood.surfaceBootstrap.blendScale;
    float surfaceSpatialVariance = spatialNeighborhood.surfaceBootstrap.variance;
    float4 spatialSurface = spatialNeighborhood.surfaceBootstrap.signal;

    float temporalMissAmount = surfaceHistoryAccepted ? saturate(1.0 - surfaceHistoryConfidence) : 1.0;
    float surfaceBootstrapBlend = lerp(saturate(_HistoryMissSpatialBlend), saturate(_YoungHistoryBlend), surfaceYoungHistoryBoost);
    float surfaceSpatialBlend = surfaceBootstrapBlend * temporalMissAmount * surfaceSpatialBlendScale;
    if (surfaceSpatialBlend > 0.0)
    {
        float preBootstrapLuminance = ComputeLuminance(accumulatedSurface.rgb);
        accumulatedSurface = lerp(accumulatedSurface, spatialSurface, surfaceSpatialBlend);
        float accumulatedLuminance = ComputeLuminance(accumulatedSurface.rgb);
        float surfaceRecoveryBoost = ComputeRecoveryBoost(surfaceMoments.z, surfaceSpatialBlend, surfaceHistoryConfidence);
        float repairResidual = abs(accumulatedLuminance - preBootstrapLuminance);
        float variance = max(
            surfaceMoments.w,
            ComputeRepairUncertaintyVarianceFloor(
                accumulatedLuminance,
                repairResidual,
                surfaceSpatialVariance * max(surfaceSpatialBlend, 1e-4),
                surfaceRecoveryBoost));
        surfaceMoments = float4(
            accumulatedLuminance,
            accumulatedLuminance * accumulatedLuminance + variance,
            surfaceMoments.z,
            variance);
    }
    else
    {
        PreserveRepairUncertainty(
            surfaceMoments,
            ComputeLuminance(accumulatedSurface.rgb),
            0.0,
            surfaceMoments.w,
            ComputeRecoveryBoost(surfaceMoments.z, 0.0, surfaceHistoryConfidence));
    }

    float currentShadowFractionFrame = saturate(_FrameShadowSum.Load(int3(pixel, 0)).r * invSampleCount);
    float accumulatedShadowMask = currentShadowFractionFrame;
    if (surfaceHistoryAccepted)
    {
        float prevAccumShadowMask = _PrevShadowHitMask.Load(int3(surfacePrevPixel, 0));
        float shadowAccumAlpha = max(rcp(max((float)_MaxHistoryLength, 1.0)), 0.1);
        accumulatedShadowMask = lerp(prevAccumShadowMask, currentShadowFractionFrame, shadowAccumAlpha);
    }
    if (surfaceSpatialBlend > 0.0)
        accumulatedShadowMask = lerp(accumulatedShadowMask, spatialNeighborhood.surfaceBootstrap.shadowSignal, surfaceSpatialBlend);
    _ShadowHitMask[pixel] = accumulatedShadowMask;

    StoreSignalMoments(PATH_SIGNAL_SURFACE, pixel, surfaceMoments);
    _SurfaceSignal[pixel] = accumulatedSurface;
    _SurfaceYoungHistorySignal[pixel] = BuildYoungHistoryAtrousSignal(surfaceMoments.z, surfaceSpatialBlend, surfaceHistoryConfidence, surfaceMoments.w);
    StoreCompositeMoments(pixel, accumulatedSurface, surfaceMoments);
}
#endif

[numthreads(1, 1, 1)]
void CSClearBootstrapArgs(uint3 id : SV_DispatchThreadID)
{
    _BootstrapDispatchArgs[7] = 0u;
}

[numthreads(1, 1, 1)]
void CSFinalizeBootstrapArgs(uint3 id : SV_DispatchThreadID)
{
    WriteTemporalDispatchArgs(_BootstrapDispatchArgs, 4u, _BootstrapDispatchArgs[7]);
}

#if defined(LUCENT_ACCUMULATE_SURFACE)
[numthreads(LUCENT_TEMPORAL_COMPACTED_GROUP_SIZE, 1, 1)]
void CSBootstrapSurface(uint3 id : SV_DispatchThreadID)
{
    uint index = id.x + id.y * (_BootstrapDispatchInfo[4] * LUCENT_TEMPORAL_COMPACTED_GROUP_SIZE);
    if (index >= _BootstrapDispatchInfo[7])
        return;

    uint pixelIndex = _BootstrapActiveIndices[index];
    uint2 pixel = uint2(pixelIndex % (uint)_Width, pixelIndex / (uint)_Width);
    BootstrapSurfaceCompacted(pixel);
}
#endif

#endif
