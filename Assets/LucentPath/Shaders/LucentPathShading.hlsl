#ifndef LUCENT_PATH_SHADING_INCLUDED
#define LUCENT_PATH_SHADING_INCLUDED

#include "LucentPathInteraction.hlsl"
#include "LucentPathMicrofacet.hlsl"

#include "Bsdf/BsdfFoundation.hlsl"
#include "Bsdf/BsdfLobes.hlsl"
#include "Lighting/LightingShadow.hlsl"

bool SampleSurfaceInteraction(
    SurfaceData surface,
    Ray incomingRay,
    PathSampler pathSample,
    uint bounce,
    out Ray nextRay,
    out float3 weight,
    out float bsdfPdf,
    out uint sampleFlags,
    out bool dielectricTransmission,
    out bool dielectricFrontFace)
{
    nextRay.origin = 0.0;
    nextRay.direction = 0.0;
    nextRay.maxDistance = max(_BounceMaxDistance, EPSILON);
    weight = 0.0;
    bsdfPdf = 0.0;
    sampleFlags = 0u;
    dielectricTransmission = false;
    dielectricFrontFace = false;

    BSDF bsdf = BuildBSDF(surface);
    if (BSDFIsEmpty(bsdf))
        return false;

    BSDFSample sample;
    if (!BSDF_Sample_f(bsdf, surface.interaction.wo, GetBounceBsdfSelector(pathSample, bounce), GetBounceBsdfSample(pathSample, bounce), TRANSPORT_MODE_RADIANCE, BXDF_FLAG_ALL, sample))
        return false;

    float cosTheta = abs(dot(surface.normal, sample.wi));
    if (cosTheta <= 0.0 || sample.pdf <= 0.0)
        return false;

    bsdfPdf = sample.pdf;
    sampleFlags = sample.flags;
    weight = sample.f * (cosTheta / sample.pdf);
    if (Max3(weight) <= 1e-6)
        return false;

    nextRay = SpawnRayFromSurface(surface.interaction, sample.wi);
    dielectricTransmission = IsThickDielectricSurface(surface) && (sample.flags & BXDF_FLAG_TRANSMISSION) != 0u;
    dielectricFrontFace = dot(surface.interaction.wo, surface.geomNormal) > 0.0;
    return true;
}

bool IsDeltaDielectricPlaneSplit(SurfaceData surface)
{
    return _PathPlane != PATH_PLANE_UNSPLIT && IsDeltaSurface(surface) && SurfaceHasTransmissionSignal(surface);
}

uint GetAllowedLobeFlags(PathState state, SurfaceData surface)
{
    if (state.depth > 1u || IsDeltaDielectricPlaneSplit(surface) == false)
    {
        return state.depth == 1u && _PathPlane == PATH_PLANE_SECOND_DELTA_REFLECTION ? 0u : BXDF_FLAG_ALL;
    }

    if (state.depth == 0u)
    {
        return _PathPlane == PATH_PLANE_DELTA_REFLECTION ? BXDF_FLAG_REFLECTION : BXDF_FLAG_TRANSMISSION;
    }

    if (IsInDeltaTransmissionChain(state) == false)
    {
        return BXDF_FLAG_ALL;
    }

    return _PathPlane == PATH_PLANE_SECOND_DELTA_REFLECTION ? BXDF_FLAG_REFLECTION : BXDF_FLAG_TRANSMISSION;
}

bool SampleSurfaceInteractionWithBSDF(
    SurfaceData surface,
    ShadingFrame frame,
    LobeSet set,
    PathSampler pathSample,
    uint bounce,
    uint allowedLobeFlags,
    out Ray nextRay,
    out float3 weight,
    out float bsdfPdf,
    out uint sampleFlags,
    out bool dielectricTransmission,
    out bool dielectricFrontFace)
{
    nextRay.origin = 0.0;
    nextRay.direction = 0.0;
    nextRay.maxDistance = max(_BounceMaxDistance, EPSILON);
    weight = 0.0;
    bsdfPdf = 0.0;
    sampleFlags = 0u;
    dielectricTransmission = false;
    dielectricFrontFace = false;

    if (SelectableLobeCount(set) == 0u)
        return false;

    BSDFSample sample;
    if (!BSDF_Sample_f(surface, frame, set, surface.interaction.wo, GetBounceBsdfSelector(pathSample, bounce), GetBounceBsdfSample(pathSample, bounce), TRANSPORT_MODE_RADIANCE, allowedLobeFlags, sample))
        return false;

    float cosTheta = abs(dot(surface.normal, sample.wi));
    if (cosTheta <= 0.0 || sample.pdf <= 0.0)
        return false;

    bsdfPdf = sample.pdf;
    sampleFlags = sample.flags;
    weight = sample.f * (cosTheta / sample.pdf);
    if (Max3(weight) <= 1e-6)
        return false;

    nextRay = SpawnRayFromSurface(surface.interaction, sample.wi);
    dielectricTransmission = IsThickDielectricSurface(surface) && (sample.flags & BXDF_FLAG_TRANSMISSION) != 0u;
    dielectricFrontFace = dot(surface.interaction.wo, surface.geomNormal) > 0.0;
    return true;
}

#endif
