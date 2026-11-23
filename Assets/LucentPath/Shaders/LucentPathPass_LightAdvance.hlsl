#ifndef LUCENT_PATH_PASS_LIGHT_ADVANCE_INCLUDED
#define LUCENT_PATH_PASS_LIGHT_ADVANCE_INCLUDED

#include "Bsdf/RandomWalkSss.hlsl"

#ifndef LUCENT_PATH_SUBSURFACE_DECLARED
#define LUCENT_PATH_SUBSURFACE_DECLARED
int _SubsurfaceEnabled;
float _SubsurfaceIntensity;
#endif

#if defined(LUCENT_PATH_GUIDING_DEPOSIT)
bool SampleGuidedSurfaceInteraction(
    SurfaceData surface,
    ShadingFrame frame,
    LobeSet set,
    PathSampler pathSample,
    uint bounce,
    float guideBins[GuideFieldBinCount],
    float guideTotal,
    float guideFraction,
    out Ray nextRay,
    out float3 weight,
    out float sampledPdf,
    out uint sampleFlags,
    out bool dielectricTransmission,
    out bool dielectricFrontFace,
    out bool requestPassthrough)
{
    nextRay.origin = 0.0;
    nextRay.direction = 0.0;
    nextRay.maxDistance = max(_BounceMaxDistance, EPSILON);
    weight = 0.0;
    sampledPdf = 0.0;
    sampleFlags = 0u;
    dielectricTransmission = false;
    dielectricFrontFace = false;
    requestPassthrough = false;

    if (SelectableLobeCount(set) == 0u)
    {
        requestPassthrough = true;
        return false;
    }

    float3 woWorld = surface.interaction.wo;
    float bsdfFraction = 1.0 - guideFraction;
    bool guideBranch = GetBounceGuidingSelector(pathSample, bounce) < guideFraction;

    float3 wiWorld;
    float3 f;
    float bsdfPdf;
    float guidePdfForMix;
    uint resolvedFlags;
    if (!guideBranch)
    {
        BSDFSample sample;
        if (!BSDF_Sample_f(surface, frame, set, woWorld, GetBounceBsdfSelector(pathSample, bounce), GetBounceBsdfSample(pathSample, bounce), TRANSPORT_MODE_RADIANCE, BXDF_FLAG_ALL, sample))
        {
            requestPassthrough = true;
            return false;
        }

        float cosThetaB = abs(dot(surface.normal, sample.wi));
        if (cosThetaB <= 0.0 || sample.pdf <= 0.0)
        {
            requestPassthrough = true;
            return false;
        }

        if ((sample.flags & BXDF_FLAG_SPECULAR) != 0u)
        {
            float specularSamplingPdf = bsdfFraction * sample.pdf;
            weight = sample.f * (cosThetaB / max(specularSamplingPdf, 1e-8));
            if (specularSamplingPdf <= 0.0 || Max3(weight) <= 1e-6)
            {
                requestPassthrough = true;
                return false;
            }
            sampledPdf = sample.pdf;
            sampleFlags = sample.flags;
            nextRay = SpawnRayFromSurface(surface.interaction, sample.wi);
            dielectricTransmission = IsThickDielectricSurface(surface) && (sample.flags & BXDF_FLAG_TRANSMISSION) != 0u;
            dielectricFrontFace = dot(woWorld, surface.geomNormal) > 0.0;
            return true;
        }

        wiWorld = sample.wi;
        f = sample.f;
        bsdfPdf = sample.pdf;
        guidePdfForMix = GuideFieldPdfFromBins(guideBins, guideTotal, wiWorld);
        resolvedFlags = sample.flags;
    }
    else
    {
        GuideFieldSampleFromBins(guideBins, guideTotal, GetBounceBsdfSelector(pathSample, bounce), GetBounceBsdfSample(pathSample, bounce), wiWorld, guidePdfForMix);
        if (guidePdfForMix <= 0.0)
            return false;

        BSDF_f_and_PDF(surface, frame, set, woWorld, wiWorld, TRANSPORT_MODE_RADIANCE, BXDF_FLAG_ALL, f, bsdfPdf);
        if (Max3(f) <= 0.0)
            return false;

        bool reflect = dot(woWorld, surface.geomNormal) * dot(wiWorld, surface.geomNormal) > 0.0;
        resolvedFlags = reflect ? (BXDF_FLAG_REFLECTION | BXDF_FLAG_DIFFUSE) : (BXDF_FLAG_TRANSMISSION | BXDF_FLAG_DIFFUSE);
    }

    float cosTheta = abs(dot(surface.normal, wiWorld));
    if (cosTheta <= 0.0)
    {
        requestPassthrough = !guideBranch;
        return false;
    }

    float mixPdf = guideFraction * guidePdfForMix + bsdfFraction * bsdfPdf;
    weight = f * (cosTheta / max(mixPdf, 1e-8));
    if (mixPdf <= 0.0 || Max3(weight) <= 1e-6)
    {
        requestPassthrough = !guideBranch;
        return false;
    }

    sampleFlags = resolvedFlags;
    sampledPdf = bsdfPdf;
    nextRay = SpawnRayFromSurface(surface.interaction, wiWorld);
    dielectricTransmission = IsThickDielectricSurface(surface) && (resolvedFlags & BXDF_FLAG_TRANSMISSION) != 0u;
    dielectricFrontFace = dot(woWorld, surface.geomNormal) > 0.0;
    return true;
}
#endif

void LightAdvanceBody(uint2 pixel, inout PathState state, Hit hit, GpuSurfaceHit surfaceHit)
{
    if (!IsPathActive(state) || state.depth >= state.maxBouncesForPath)
        return;

    if (hit.didHit == 0u)
        return;

    if (IsSkySplitReflectionContinuation(state))
    {
        SetPathActive(state, false);
        return;
    }

    Ray ray = GetPathRay(state);
    SurfaceData surface = GetSurface(surfaceHit, ray);
    PathSampler pathSample = CreatePathSampler(pixel, (uint) _CurrentSampleIndex);
    float3 wo = -ray.direction;
    LobeSet set = ComputeLobeSet(surface);
    ShadingFrame frame = ComputeShadingFrame(surface);
    OrientShadingFrameForView(frame, wo);
    if (state.depth == 0u)
    {
        SetPrimaryReflectionSurface(state, SurfaceHasReflectionSignal(surface), SurfaceGuideRoughness(surface));
        SetPrimaryTransmissionSurface(state, SurfaceHasTransmissionSignal(surface));
    }


#if !defined(LUCENT_PATH_SHADE_ADVANCE_ONLY)
    uint signalKind = GetPathSignalKind(state);

    if (!IsDeltaSurface(surface) && SelectableLobeCount(set) > 0u)
    {
        if (state.depth == 0u && signalKind == PATH_SIGNAL_SURFACE)
        {
            float3 surfaceContribution;
            float3 reflectionContribution;
            float3 transmissionContribution;
            EstimateDirectLightingSplitWithBSDF(pixel, surface, frame, set, wo, pathSample, state.depth, surfaceContribution, reflectionContribution, transmissionContribution);
            AccumulateFrameSurfaceSignal(pixel, state.throughput * surfaceContribution, state.depth);
            AccumulateFrameReflectionSignal(pixel, state, state.throughput * reflectionContribution);
            AccumulateFrameTransmissionSignal(pixel, state, state.throughput * transmissionContribution);
#if defined(LUCENT_PATH_GUIDING_DEPOSIT)
            if (_RadianceCacheEnabled != 0 && state.depth >= (uint)_RadianceCacheMinDepth)
            {
                SpatialHashKey guideKey = SpatialHashComputeKey(ray.origin, _CameraWorldPos.xyz, _RadianceCacheBaseCellSize, _RadianceCacheLodScale, (uint)_RadianceCacheCapacity);
                float guideWeight = ComputeLuminance(surfaceContribution + reflectionContribution + transmissionContribution);
                GuideFieldDeposit(_RadianceCache, (uint)_RadianceCacheCapacity, guideKey, ray.direction, guideWeight);
            }
#endif
        }
        else
        {
            uint shadowChannel = signalKind == PATH_SIGNAL_REFLECTION ? 1u : (signalKind == PATH_SIGNAL_TRANSMISSION ? 2u : 0u);
            float3 localDirect = EstimateDirectLightingWithBSDF(pixel, surface, frame, set, wo, pathSample, state.depth, shadowChannel);
            AccumulateFrameSignal(pixel, state, signalKind, state.throughput * localDirect);
#if defined(LUCENT_PATH_GUIDING_DEPOSIT)
            if (_RadianceCacheEnabled != 0 && state.depth >= (uint)_RadianceCacheMinDepth)
            {
                SpatialHashKey guideKey = SpatialHashComputeKey(ray.origin, _CameraWorldPos.xyz, _RadianceCacheBaseCellSize, _RadianceCacheLodScale, (uint)_RadianceCacheCapacity);
                GuideFieldDeposit(_RadianceCache, (uint)_RadianceCacheCapacity, guideKey, ray.direction, ComputeLuminance(localDirect));
            }
#endif
        }
    }
#endif

#if !defined(LUCENT_PATH_SHADE_NEE_ONLY)
    bool isPreviousVertexScattering = state.depth > 0u && WasPreviousBounceSpecular(state) == false;
    bool previousEnvNee = HadEnvNeeAtPrevVertex(state);
    bool previousSurfelNee = HadSurfelNeeAtPrevVertex(state);
    SetEnvNeeAtPrevVertex(state, _EnvNeeEnabled != 0 && _EnvSamplingReady != 0 && _SkyObjectLighting != 0 && !IsDeltaSurface(surface) && SelectableLobeCount(set) > 0u);
    SetSurfelNeeAtPrevVertex(state, _EmissiveSurfelNeeEnabled != 0 && !IsDeltaSurface(surface) && SelectableLobeCount(set) > 0u && SurfelGuideCandidateCount() > 0u);

    if (_SubsurfaceEnabled != 0 && IsSubsurfaceSurface(surface) && IsThinWalledTranslucentSurface(surface) == false)
    {
        float subsurfaceProbability = OpenPbrSubsurfaceMixWeight(surface);
        float uSubsurfacePick = GetBounceSssLobeSelector(pathSample, state.depth);
        bool takeSubsurfaceBranch = uSubsurfacePick < subsurfaceProbability;

        if (takeSubsurfaceBranch)
        {
            SubsurfaceMedium medium = BuildSubsurfaceMedium(surface.subsurfaceColor, surface.subsurfaceRadius, surface.subsurfaceScale, surface.subsurfaceAnisotropy);
            float3 inwardNormal = SafeNormalize(-surface.geomNormal, float3(0.0, -1.0, 0.0));
            float3 entryDirection = SampleCosineHemisphere(inwardNormal, GetBounceSssDirectionSample(pathSample, state.depth));
            float3 entryPosition = surface.position + inwardNormal * max(_SurfaceBias, EPSILON);
            uint sssSeed = pathSample.pixelSeed ^ pathSample.sampleIndex ^ (state.depth * 0x68E31DA4u) ^ 0x7F6F2A8Du;
            uint entryInstanceId = surfaceHit.instanceId;

            RandomWalkResult walk;
            bool usedThinBtdf = ExecuteThinSubsurfaceBtdf(
                medium,
                entryPosition,
                entryDirection,
                entryInstanceId,
                max(_BounceMaxDistance, EPSILON),
                GetBounceVolumePhaseSample(pathSample, state.depth),
                walk);
            if (!usedThinBtdf)
            {
                float3 sssEffectiveRadius = max(surface.subsurfaceRadius * surface.subsurfaceScale, float3(1e-6, 1e-6, 1e-6));
                walk = ExecuteDiffusionSss(surface.subsurfaceColor, sssEffectiveRadius, surface.position, surface.geomNormal, entryInstanceId, sssSeed);
            }

            if (!walk.exited || Max3(walk.throughput) <= 1e-6)
            {
                SetPathActive(state, false);
                return;
            }

            float3 sssScale = walk.throughput * _SubsurfaceIntensity;
            state.throughput *= sssScale;
            SetPathSubsurfaceMediumState(state, entryInstanceId, walk.traveledDistance, walk.transportMode);

            float3 exitDirWorld = SafeNormalize(walk.exitDirection, float3(0.0, 1.0, 0.0));
            Ray sssExitRay;
            sssExitRay.origin = walk.exitPosition + exitDirWorld * max(_SurfaceBias, EPSILON);
            sssExitRay.direction = exitDirWorld;
            sssExitRay.maxDistance = max(_BounceMaxDistance, EPSILON);
            SetPathRay(state, sssExitRay);
            state.prevBsdfPdf = max(INV_PI, 1e-6);
            state.guideRoughness = 1.0;
            SetPreviousBounceSpecular(state, false);
            SetPathSignalKind(state, ResolveScatteringSignalKind(GetPathSignalKind(state), BXDF_FLAG_REFLECTION | BXDF_FLAG_DIFFUSE, state.depth));
            state.depth += 1u;
            if (state.depth >= state.maxBouncesForPath)
                SetPathActive(state, false);
            return;
        }

        float nonSubsurfaceCompensation = min(rcp(max(1.0 - subsurfaceProbability, 1e-4)), SSS_MAX_BRANCH_COMPENSATION);
        state.throughput *= nonSubsurfaceCompensation;
    }

    uint allowedLobeFlags = GetAllowedLobeFlags(state, surface);
    Ray nextRay;
    float3 weight;
    float bsdfPdf;
    uint sampleFlags;
    bool dielectricTransmission;
    bool dielectricFrontFace;
    bool sampledInteraction;
#if defined(LUCENT_PATH_GUIDING_DEPOSIT)
    bool guideTrusted = false;
    float guideBins[GuideFieldBinCount];
    float guideTotal;
    if (_RadianceCacheEnabled != 0 && state.depth >= (uint)_RadianceCacheMinDepth && !IsDeltaSurface(surface))
    {
        SpatialHashKey guideSampleKey = SpatialHashComputeKeyJittered(surface.position, _CameraWorldPos.xyz, _RadianceCacheBaseCellSize, _RadianceCacheLodScale, (uint)_RadianceCacheCapacity, SpatialHashJitter(pathSample.pixelSeed, pathSample.sampleIndex, state.depth));
        guideTrusted = GuideFieldFetch(_RadianceCache, (uint)_RadianceCacheCapacity, guideSampleKey, (uint)_RadianceCacheMinSamples, guideBins, guideTotal);
    }
    if (guideTrusted)
    {
        bool guideRequestPassthrough;
        sampledInteraction = SampleGuidedSurfaceInteraction(surface, frame, set, pathSample, state.depth, guideBins, guideTotal, min(saturate(_RadianceCacheCorrectionProbability), 0.95), nextRay, weight, bsdfPdf, sampleFlags, dielectricTransmission, dielectricFrontFace, guideRequestPassthrough);
        if (!sampledInteraction && !guideRequestPassthrough)
        {
            SetPathActive(state, false);
            return;
        }
    }
    else
        sampledInteraction = SampleSurfaceInteractionWithBSDF(surface, frame, set, pathSample, state.depth, allowedLobeFlags, nextRay, weight, bsdfPdf, sampleFlags, dielectricTransmission, dielectricFrontFace);
#else
    sampledInteraction = SampleSurfaceInteractionWithBSDF(surface, frame, set, pathSample, state.depth, allowedLobeFlags, nextRay, weight, bsdfPdf, sampleFlags, dielectricTransmission, dielectricFrontFace);
#endif
    if (sampledInteraction == false && allowedLobeFlags != BXDF_FLAG_ALL)
    {
        SetPathActive(state, false);
        return;
    }

    if (!sampledInteraction)
    {
        ray.origin = surface.position + ray.direction * _SurfaceBias;
        ray.direction = SafeNormalize(ray.direction, surface.normal);
        SetPathRay(state, ray);
        state.prevBsdfPdf = 1.0;
        state.guideRoughness = SurfaceGuideRoughness(surface);
        SetPreviousBounceSpecular(state, true);
        SetPathSignalKind(state, ResolveScatteringSignalKind(GetPathSignalKind(state), BXDF_FLAG_REFLECTION | BXDF_FLAG_SPECULAR, state.depth));
        state.depth += 1u;
#if defined(LUCENT_PATH_HAS_DXR_FINALIZE)
        if (state.depth >= state.maxBouncesForPath)
        {
            FinalizePathAtBounceLimit(pixel, state);
            return;
        }
#endif
        return;
    }

    state.throughput *= weight;
    if (Max3(state.throughput) <= 1e-4)
    {
        SetPathActive(state, false);
        return;
    }

    if (dielectricTransmission)
    {
        SetPathInsideDielectric(state, dielectricFrontFace);
        state.dielectricTint = dielectricFrontFace ? surface.transmissionColor : float3(1.0, 1.0, 1.0);
        state.dielectricDepth = dielectricFrontFace ? surface.transmissionDepth : 1.0;
    }

    if (state.depth >= (uint) _RussianRouletteStart)
    {
        float p = clamp(Max3(state.throughput), 0.05, 0.95);
        if (GetBounceRouletteSample(pathSample, state.depth) > p)
        {
            SetPathActive(state, false);
            return;
        }

        state.throughput /= p;
    }

    bool isNeeTransparentTransmission = isPreviousVertexScattering && IsDeltaSurface(surface) && (sampleFlags & BXDF_FLAG_TRANSMISSION) != 0u && (sampleFlags & BXDF_FLAG_SPECULAR) != 0u;

    if (isNeeTransparentTransmission)
    {
        SetEnvNeeAtPrevVertex(state, previousEnvNee);
        SetSurfelNeeAtPrevVertex(state, previousSurfelNee);
    }
    else
    {
        state.prevBsdfPdf = bsdfPdf;
        state.guideRoughness = SurfaceGuideRoughness(surface);
        SetPreviousBounceSpecular(state, (sampleFlags & BXDF_FLAG_SPECULAR) != 0u);
    }

    uint resolvedSignalKind = ResolveScatteringSignalKind(GetPathSignalKind(state), sampleFlags, state.depth);
    SetPathSignalKind(state, allowedLobeFlags == BXDF_FLAG_REFLECTION ? PATH_SIGNAL_REFLECTION : resolvedSignalKind);
    SetDeltaTransmissionChain(state, state.depth == 0u && allowedLobeFlags == BXDF_FLAG_TRANSMISSION);
    SetPathRay(state, nextRay);
    state.depth += 1u;
#if defined(LUCENT_PATH_HAS_DXR_FINALIZE)
    if (state.depth >= state.maxBouncesForPath)
    {
        FinalizePathAtBounceLimit(pixel, state);
        return;
    }
#endif
#endif
}

#endif
