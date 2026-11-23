#ifndef LUCENT_PATH_PASS_INTERSECT_RESOLVE_COMMON_INCLUDED
#define LUCENT_PATH_PASS_INTERSECT_RESOLVE_COMMON_INCLUDED

#define LUCENT_PATH_SECONDARY_GUIDE_FLAG_SKY 2147483648u

void StoreSecondaryTerminalGuideEvent(uint2 pixel, uint signalKind, Ray ray, float terminalDistance, uint stableId, uint flags, PathState state);

void AccumulateTerminalRadiance(
    uint2 pixel,
    Ray ray,
    uint signalKind,
    float3 radiance,
    float terminalDistance,
    uint stableId,
    uint flags,
    PathState state,
    bool allowSecondarySignal)
{
    if (state.depth == 0u)
    {
        AccumulateFrameEmission(pixel, state, radiance);
        AccumulateFrameAlpha(pixel, 1.0);
        AccumulateFrameHitMask(pixel, 0.0);
        return;
    }

    if (allowSecondarySignal)
        AccumulateFrameSignal(pixel, state, signalKind, radiance);

    StoreSecondaryTerminalGuideEvent(pixel, signalKind, ray, terminalDistance, stableId, flags, state);
}

bool ResolveBsdfSampledAnalyticLightTerminal(uint2 pixel, Ray ray, Hit hit, inout PathState state)
{
    float maxLightHitDistance = hit.didHit != 0u ? hit.t : ray.maxDistance;
    float areaLightT;
    float3 areaLightRadiance;
    float areaLightPdf;
    if (!ShouldAccumulateBsdfSampledAnalyticLight(state.depth, WasPreviousBounceSpecular(state), state.prevBsdfPdf) ||
        !FindClosestAnalyticAreaLightHit(ray, maxLightHitDistance, areaLightT, areaLightRadiance, areaLightPdf))
        return false;

    if (IsInsideDielectricVolume(state))
    {
        state.throughput *= ComputeVolumeAttenuationFromTint(state.dielectricTint, areaLightT, state.dielectricDepth);
        if (Max3(state.throughput) <= 1e-4)
        {
            SetPathActive(state, false);
            return true;
        }
    }

    uint signalKind = GetPathSignalKind(state);
    float misWeight = GetBsdfSampledLightMisWeight(state.depth, WasPreviousBounceSpecular(state), state.prevBsdfPdf, areaLightPdf);
    float3 radiance = state.throughput * areaLightRadiance * _DirectIntensity * misWeight;
    AccumulateTerminalRadiance(pixel, ray, signalKind, radiance, areaLightT, 1u, 0u, state, true);

#if defined(LUCENT_PATH_GUIDING_DEPOSIT)
    if (_RadianceCacheEnabled != 0 && state.depth >= (uint)_RadianceCacheMinDepth)
    {
        SpatialHashKey guideLightKey = SpatialHashComputeKey(ray.origin, _CameraWorldPos.xyz, _RadianceCacheBaseCellSize, _RadianceCacheLodScale, (uint)_RadianceCacheCapacity);
        GuideFieldDeposit(_RadianceCache, (uint)_RadianceCacheCapacity, guideLightKey, ray.direction, ComputeLuminance(areaLightRadiance * _DirectIntensity));
    }
#endif

    SetPathActive(state, false);
    return true;
}

bool ResolveMissTerminal(uint2 pixel, Ray ray, Hit hit, inout PathState state)
{
    if (hit.didHit != 0u)
        return false;

    uint signalKind = GetPathSignalKind(state);
    bool skipSplitPrimaryReflectionSky = signalKind == PATH_SIGNAL_REFLECTION && HasPrimaryReflectionSkySplit(state);
    float environmentLod = (state.depth == 0u || WasPreviousBounceSpecular(state)) ? 0.0 : saturate(state.guideRoughness) * (float)_EnvironmentMaxMip;
    float3 environmentRadiance = state.throughput * EvaluateEnvironmentRadianceLod(ray.direction, environmentLod);

    if (_SkyObjectLighting == 0 && state.depth > 0u && !WasPreviousBounceSpecular(state))
        environmentRadiance = float3(0.0, 0.0, 0.0);

    if (state.depth > 0u && state.prevBsdfPdf > 1e-6 && !WasPreviousBounceSpecular(state) && _EnvSamplingReady != 0 && HadEnvNeeAtPrevVertex(state))
    {
        float envPdf = EvaluateEnvironmentPdf(ray.direction);
        if (envPdf > 0.0)
        {
            float mis = PowerHeuristic(state.prevBsdfPdf, envPdf);
            environmentRadiance *= mis;
        }
    }

    AccumulateTerminalRadiance(
        pixel,
        ray,
        signalKind,
        environmentRadiance,
        max(ray.maxDistance, _BounceMaxDistance),
        0u,
        LUCENT_PATH_SECONDARY_GUIDE_FLAG_SKY,
        state,
        !skipSplitPrimaryReflectionSky);

    SetPathActive(state, false);
    return true;
}

bool ApplyHitVolumeAttenuation(Hit hit, inout PathState state)
{
    if (!IsInsideDielectricVolume(state))
        return true;

    state.throughput *= ComputeVolumeAttenuationFromTint(state.dielectricTint, hit.t, state.dielectricDepth);
    if (Max3(state.throughput) > 1e-4)
        return true;

    SetPathActive(state, false);
    return false;
}

uint GetSecondaryGuidePathKind(uint signalKind)
{
    if (signalKind == PATH_SIGNAL_REFLECTION)
        return (uint)GUIDE_PATH_KIND_SPECULAR;
    if (signalKind == PATH_SIGNAL_TRANSMISSION)
        return (uint)GUIDE_PATH_KIND_TRANSMISSION;
    return (uint)GUIDE_PATH_KIND_PRIMARY;
}

float ResolveSecondarySegmentDistance(Ray ray, float3 worldPosition)
{
    return max(dot(worldPosition - ray.origin, SafeNormalize(ray.direction, float3(0.0, 0.0, 1.0))), 1e-4);
}

float3 ResolvePrimaryAnchoredSecondaryPosition(uint2 pixel, float secondaryHitDistance)
{
    Ray primaryRay = GenerateGuideCameraRay(pixel);
    float primaryHitDistance = max(_PrimaryHitDistance[pixel], 0.0);
    float3 primaryHitPosition = primaryRay.origin + primaryRay.direction * primaryHitDistance;
    return primaryHitPosition + primaryRay.direction * max(secondaryHitDistance, 1e-4);
}

bool UsesPrimaryAnchoredSecondaryAddress(uint signalKind)
{
    return signalKind == PATH_SIGNAL_REFLECTION;
}

uint ResolveSecondaryPreviousWorldPosition(SurfaceData surface, out float3 previousWorldPosition)
{
    previousWorldPosition = surface.position;
    return 0u;
}

bool BuildPrimaryTransmissionGuideRay(Ray ray, SurfaceData surface, out Ray transmissionRay)
{
    transmissionRay = ray;

    if (!SurfaceHasTransmissionSignal(surface))
        return false;

    float3 transmissionDirection = ray.direction;
    if (IsThickDielectricSurface(surface))
    {
        float3 incidentNormal;
        bool frontFace;
        float etaI;
        float etaT;
        float cosThetaI;
        bool totalInternalReflection;
        float fresnel;
        GetDielectricInterface(ray.direction, surface.normal, surface.frontFace != 0u, surface.eta, incidentNormal, frontFace, etaI, etaT, cosThetaI, totalInternalReflection, fresnel);
        if (totalInternalReflection || !RefractDirection(ray.direction, incidentNormal, etaI, etaT, transmissionDirection))
            return false;
    }
    else if (!IsThinDielectricSurface(surface))
    {
        return false;
    }

    transmissionRay = SpawnRayFromSurface(surface.interaction, SafeNormalize(transmissionDirection, ray.direction));
    return true;
}

bool CanContinuePrimaryTransmissionGuide(Ray ray, SurfaceData surface, out Ray nextRay)
{
    nextRay = ray;

    if (IsThinDielectricSurface(surface))
    {
        nextRay = SpawnRayFromSurface(surface.interaction, ray.direction);
        return SurfaceHasDeltaTransmission(surface);
    }

    if (!IsThickDielectricSurface(surface) || surface.transmissionWeight <= 1e-4 || SurfaceHasDeltaTransmission(surface) == false)
        return false;

    float3 incidentNormal;
    bool frontFace;
    float etaI;
    float etaT;
    float cosThetaI;
    bool totalInternalReflection;
    float fresnel;
    GetDielectricInterface(ray.direction, surface.normal, surface.frontFace != 0u, surface.eta, incidentNormal, frontFace, etaI, etaT, cosThetaI, totalInternalReflection, fresnel);
    if (totalInternalReflection)
        return false;

    float3 refractedDirection;
    if (!RefractDirection(ray.direction, incidentNormal, etaI, etaT, refractedDirection))
        return false;

    nextRay = SpawnRayFromSurface(surface.interaction, refractedDirection);
    return true;
}

static const float SURFACE_ANCHORED_TRANSMISSION_HIT_DISTANCE = 1e-4;

void AnchorPrimaryTransmissionGuideToSurface(uint2 pixel)
{
    _TransmissionHitDistance[pixel] = SURFACE_ANCHORED_TRANSMISSION_HIT_DISTANCE;
}

void StorePrimaryTransmissionGuideEvent(
    uint2 pixel,
    Ray transmissionRay,
    float hitDistance,
    float3 worldPosition,
    float3 previousWorldPosition,
    uint previousPositionValid,
    float3 worldNormalOrDirection,
    uint stableId,
    uint historyVersion,
    uint flags,
    float eta,
    uint reservedFlags)
{
    if (_TransmissionHitDistance[pixel] > 0.0)
        return;

    float pathDist = max(hitDistance, 1e-4);
    float3 rayDirection = SafeNormalize(transmissionRay.direction, float3(0.0, 0.0, 1.0));
    bool skyEvent = (flags & LUCENT_PATH_SECONDARY_GUIDE_FLAG_SKY) != 0u;
    bool primaryVirtual = (reservedFlags & LUCENT_PATH_SECONDARY_GUIDE_RESERVED_PRIMARY_VIRTUAL) != 0u;
    float3 virtualWorldPosition = ResolvePrimaryAnchoredSecondaryPosition(pixel, pathDist);
    float3 resolvedWorldPosition = primaryVirtual ? virtualWorldPosition : worldPosition;

    GpuSecondaryGuideEvent eventData;
    eventData.worldPosition = resolvedWorldPosition;
    eventData.hitDistance = pathDist;
    eventData.worldNormalOrDirection = skyEvent ? rayDirection : SafeNormalize(worldNormalOrDirection, rayDirection);
    eventData.valid = 1u;
    eventData.stableId = stableId;
    eventData.historyVersion = historyVersion;
    eventData.pathKind = (uint)GUIDE_PATH_KIND_TRANSMISSION;
    eventData.flags = flags;
    eventData.roughness = 0.0;
    eventData.eta = max(eta, 1.0);
    eventData.mediumState = 0u;
    eventData.reserved = reservedFlags | (skyEvent ? LUCENT_PATH_SECONDARY_GUIDE_RESERVED_SKY : 0u);
    eventData.previousWorldPosition = previousPositionValid != 0u ? previousWorldPosition : resolvedWorldPosition;
    if (!skyEvent && primaryVirtual)
    {
        eventData.previousWorldPosition = virtualWorldPosition;
        if (previousPositionValid != 0u)
        {
            float3 surfaceMotion = previousWorldPosition - worldPosition;
            eventData.previousWorldPosition = virtualWorldPosition + surfaceMotion;
        }
    }
    // In NVIDIA-style PSR/PSTR reprojection, camera motion comes from the matrices and
    // previous position carries only virtual/object-space motion. A static surface therefore
    // still has a valid previous virtual position equal to the current virtual position.
    eventData.previousPositionValid = skyEvent ? 0u : (primaryVirtual ? 1u : previousPositionValid);

    _TransmissionHitDistance[pixel] = pathDist;
    _TransmissionGuideEvent0[pixel] = PackSecondaryGuideEvent0(eventData);
    _TransmissionGuideEvent1[pixel] = PackSecondaryGuideEvent1(eventData);
    _TransmissionGuideEvent2[pixel] = PackSecondaryGuideEvent2(eventData);
    _TransmissionGuideEvent3[pixel] = PackSecondaryGuideEvent3(eventData);
}

void StorePrimaryTransmissionSkyGuideEvent(
    uint2 pixel,
    Ray transmissionRay,
    float hitDistance,
    float eta,
    uint reservedFlags)
{
    float skyDistance = max(hitDistance, 1e-4);
    float3 skyPosition = transmissionRay.origin + SafeNormalize(transmissionRay.direction, float3(0.0, 0.0, 1.0)) * skyDistance;
    StorePrimaryTransmissionGuideEvent(
        pixel,
        transmissionRay,
        skyDistance,
        skyPosition,
        skyPosition,
        0u,
        SafeNormalize(transmissionRay.direction, float3(0.0, 0.0, 1.0)),
        0u,
        0u,
        LUCENT_PATH_SECONDARY_GUIDE_FLAG_SKY,
        eta,
        reservedFlags);
}

void StoreSecondaryGuideEvent(
    uint2 pixel,
    uint signalKind,
    Ray ray,
    float hitDistance,
    SurfaceData surface,
    uint stableId,
    uint historyVersion,
    uint flags,
    float3 previousWorldPosition,
    uint previousPositionValid,
    PathState state)
{
    float secondaryHitDistance = max(hitDistance, 1e-4);
    bool reflectionSignal = signalKind == PATH_SIGNAL_REFLECTION;

    GpuSecondaryGuideEvent eventData;
    eventData.worldPosition = UsesPrimaryAnchoredSecondaryAddress(signalKind)
        ? ResolvePrimaryAnchoredSecondaryPosition(pixel, secondaryHitDistance)
        : surface.position;
    eventData.hitDistance = secondaryHitDistance;
    eventData.worldNormalOrDirection = signalKind == PATH_SIGNAL_TRANSMISSION
        ? SafeNormalize(surface.normal, surface.geomNormal)
        : SafeNormalize(ray.direction, surface.normal);
    eventData.valid = 1u;
    eventData.stableId = stableId;
    eventData.historyVersion = historyVersion;
    eventData.pathKind = GetSecondaryGuidePathKind(signalKind);
    eventData.flags = flags;
    eventData.roughness = saturate(state.guideRoughness);
    eventData.eta = ResolveMaterialIor(surface.eta);
    eventData.mediumState = IsInsideDielectricVolume(state) ? 1u : 0u;
    eventData.reserved = 0u;
    eventData.previousWorldPosition = eventData.worldPosition;
    if (previousPositionValid != 0u)
    {
        float3 surfaceMotion = previousWorldPosition - surface.position;
        eventData.previousWorldPosition = UsesPrimaryAnchoredSecondaryAddress(signalKind)
            ? eventData.worldPosition + surfaceMotion
            : previousWorldPosition;
    }
    eventData.previousPositionValid = previousPositionValid;

    if (reflectionSignal)
    {
        _ReflectionGuideEvent0[pixel] = PackSecondaryGuideEvent0(eventData);
        _ReflectionGuideEvent1[pixel] = PackSecondaryGuideEvent1(eventData);
        _ReflectionGuideEvent2[pixel] = PackSecondaryGuideEvent2(eventData);
        _ReflectionGuideEvent3[pixel] = PackSecondaryGuideEvent3(eventData);    }
    else if (signalKind == PATH_SIGNAL_TRANSMISSION)
    {
        _TransmissionGuideEvent0[pixel] = PackSecondaryGuideEvent0(eventData);
        _TransmissionGuideEvent1[pixel] = PackSecondaryGuideEvent1(eventData);
        _TransmissionGuideEvent2[pixel] = PackSecondaryGuideEvent2(eventData);
        _TransmissionGuideEvent3[pixel] = PackSecondaryGuideEvent3(eventData);    }
}

void StoreSecondaryTerminalGuideEvent(
    uint2 pixel,
    uint signalKind,
    Ray ray,
    float terminalDistance,
    uint stableId,
    uint flags,
    PathState state)
{
    if (signalKind != PATH_SIGNAL_REFLECTION && signalKind != PATH_SIGNAL_TRANSMISSION)
        return;

    bool reflectionSignal = signalKind == PATH_SIGNAL_REFLECTION;
    if (reflectionSignal && _ReflectionHitDistance[pixel] > 0.0)
        return;
    if (!reflectionSignal && _TransmissionHitDistance[pixel] > 0.0)
        return;

    float pathDist = max(terminalDistance, 1e-4);
    float3 rayDirection = SafeNormalize(ray.direction, float3(0.0, 0.0, 1.0));

    GpuSecondaryGuideEvent eventData;
    eventData.worldPosition = UsesPrimaryAnchoredSecondaryAddress(signalKind)
        ? ResolvePrimaryAnchoredSecondaryPosition(pixel, pathDist)
        : ray.origin + rayDirection * max(terminalDistance, 1.0);
    eventData.hitDistance = pathDist;
    eventData.worldNormalOrDirection = rayDirection;
    eventData.valid = 1u;
    eventData.stableId = stableId;
    eventData.historyVersion = 0u;
    eventData.pathKind = GetSecondaryGuidePathKind(signalKind);
    eventData.flags = flags;
    eventData.roughness = saturate(state.guideRoughness);
    eventData.eta = 1.0;
    eventData.mediumState = IsInsideDielectricVolume(state) ? 1u : 0u;
    eventData.reserved = 0u;
    eventData.previousWorldPosition = eventData.worldPosition;
    eventData.previousPositionValid = 0u;

    if (reflectionSignal)
    {
        _ReflectionHitDistance[pixel] = pathDist;
        _ReflectionGuideEvent0[pixel] = PackSecondaryGuideEvent0(eventData);
        _ReflectionGuideEvent1[pixel] = PackSecondaryGuideEvent1(eventData);
        _ReflectionGuideEvent2[pixel] = PackSecondaryGuideEvent2(eventData);
        _ReflectionGuideEvent3[pixel] = PackSecondaryGuideEvent3(eventData);    }
    else
    {
        _TransmissionHitDistance[pixel] = pathDist;
        _TransmissionGuideEvent0[pixel] = PackSecondaryGuideEvent0(eventData);
        _TransmissionGuideEvent1[pixel] = PackSecondaryGuideEvent1(eventData);
        _TransmissionGuideEvent2[pixel] = PackSecondaryGuideEvent2(eventData);
        _TransmissionGuideEvent3[pixel] = PackSecondaryGuideEvent3(eventData);    }
}

void ResolveSurfaceTerminal(
    uint2 pixel,
    Ray ray,
    SurfaceData surface,
    uint stableId,
    uint historyVersion,
    uint flags,
    inout PathState state)
{
    if (state.depth == 0u)
    {
        BSDF primaryBsdf = BuildBSDF(surface);
        SetPrimaryReflectionSurface(state, BSDFHasReflectionSignal(primaryBsdf), SurfaceGuideRoughness(surface));
        SetPrimaryTransmissionSurface(state, BSDFHasTransmissionSignal(primaryBsdf));
        AccumulateFrameAlpha(pixel, ResolvePrimarySurfaceAlpha(surface));
        AccumulateFrameHitMask(pixel, 1.0);
    }

    float3 emissionTerminal = state.throughput * surface.emission * ComputeClearcoatBaseAttenuation(surface, surface.interaction.wo);
    if (state.depth > 0u && state.prevBsdfPdf > 1e-6 && !WasPreviousBounceSpecular(state) && _EmissiveSurfelNeeEnabled != 0 && HadSurfelNeeAtPrevVertex(state) && Max3(surface.emission) > 1e-8)
    {
        float surfelPdf = EvaluateSurfelGuidePdf(ray.origin, ray.direction);
        if (surfelPdf > 0.0)
            emissionTerminal *= PowerHeuristic(state.prevBsdfPdf, surfelPdf);
    }
    AccumulateFrameEmission(pixel, state, emissionTerminal);

    if (state.depth > 0u && !IsDeltaSurface(surface))
    {
        uint signalKind = GetPathSignalKind(state);
        float secondarySegmentDistance = ResolveSecondarySegmentDistance(ray, surface.position);
        if (signalKind == PATH_SIGNAL_REFLECTION && _ReflectionHitDistance[pixel] <= 0.0)
        {
            _ReflectionHitDistance[pixel] = secondarySegmentDistance;
            float3 previousWorldPosition;
            uint previousPositionValid = ResolveSecondaryPreviousWorldPosition(surface, previousWorldPosition);
            StoreSecondaryGuideEvent(pixel, signalKind, ray, secondarySegmentDistance, surface, stableId, historyVersion, flags, previousWorldPosition, previousPositionValid, state);
        }
        else if (signalKind == PATH_SIGNAL_TRANSMISSION && _TransmissionHitDistance[pixel] <= 0.0)
        {
            _TransmissionHitDistance[pixel] = secondarySegmentDistance;
            float3 previousWorldPosition;
            uint previousPositionValid = ResolveSecondaryPreviousWorldPosition(surface, previousWorldPosition);
            StoreSecondaryGuideEvent(pixel, signalKind, ray, secondarySegmentDistance, surface, stableId, historyVersion, flags, previousWorldPosition, previousPositionValid, state);
        }
    }
}

bool ResolveFinalizeSampleBeforeSurface(uint2 pixel, Ray ray, Hit hit, inout PathState state)
{
    float maxLightHitDistance = hit.didHit != 0u ? hit.t : ray.maxDistance;
    float areaLightT;
    float3 areaLightRadiance;
    float areaLightPdf;
    if (ShouldAccumulateBsdfSampledAnalyticLight(state.depth, WasPreviousBounceSpecular(state), state.prevBsdfPdf) &&
        FindClosestAnalyticAreaLightHit(ray, maxLightHitDistance, areaLightT, areaLightRadiance, areaLightPdf))
    {
        if (IsInsideDielectricVolume(state))
            state.throughput *= ComputeVolumeAttenuationFromTint(state.dielectricTint, areaLightT, state.dielectricDepth);

        if (Max3(state.throughput) > 1e-4)
        {
            uint signalKind = GetPathSignalKind(state);
            float misWeight = GetBsdfSampledLightMisWeight(state.depth, WasPreviousBounceSpecular(state), state.prevBsdfPdf, areaLightPdf);
            float3 radiance = state.throughput * areaLightRadiance * _DirectIntensity * misWeight;
            AccumulateTerminalRadiance(pixel, ray, signalKind, radiance, areaLightT, 1u, 0u, state, true);
        }

        return true;
    }

    if (hit.didHit == 0u)
    {
        uint signalKind = GetPathSignalKind(state);
        float environmentLod = (state.depth == 0u || WasPreviousBounceSpecular(state)) ? 0.0 : saturate(state.guideRoughness) * (float)_EnvironmentMaxMip;
        float3 environmentRadiance = state.throughput * EvaluateEnvironmentRadianceLod(ray.direction, environmentLod);

        if (_SkyObjectLighting == 0 && state.depth > 0u && !WasPreviousBounceSpecular(state))
            environmentRadiance = float3(0.0, 0.0, 0.0);

        if (state.depth > 0u && state.prevBsdfPdf > 1e-6 && !WasPreviousBounceSpecular(state) && _EnvSamplingReady != 0 && HadEnvNeeAtPrevVertex(state))
        {
            float envPdf = EvaluateEnvironmentPdf(ray.direction);
            if (envPdf > 0.0)
            {
                float mis = PowerHeuristic(state.prevBsdfPdf, envPdf);
                environmentRadiance *= mis;
            }
        }

        AccumulateTerminalRadiance(
            pixel,
            ray,
            signalKind,
            environmentRadiance,
            max(ray.maxDistance, _BounceMaxDistance),
            0u,
            LUCENT_PATH_SECONDARY_GUIDE_FLAG_SKY,
            state,
            true);
        return true;
    }

    if (IsInsideDielectricVolume(state))
    {
        state.throughput *= ComputeVolumeAttenuationFromTint(state.dielectricTint, hit.t, state.dielectricDepth);
        if (Max3(state.throughput) <= 1e-4)
            return true;
    }

    return false;
}

#endif
