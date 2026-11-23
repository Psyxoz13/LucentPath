using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using static LucentPath.Rendering.PipelineShaderProperties;

namespace LucentPath.Rendering
{
    internal class DXRPathTracingRayGenPass
    {
        private readonly DXRPipelineResources _resources;
        private readonly DXRKernelLibrary _kernels;

        private const int DefaultThreadGroupSize = 8;
        private const int PathPlaneUnsplit = 0;
        private const int PathPlaneMain = 1;
        private const int PathPlaneDeltaReflection = 2;
        private const int PathPlaneSecondDeltaReflection = 3;
        private static readonly CustomSampler _marker = CustomSampler.Create("LucentPath.DXR.PathTracingRayGen", true);
        private static readonly CustomSampler _primarySampleMarker = CustomSampler.Create("LucentPath.DXR.PrimarySample", true);
        private static readonly CustomSampler _intersectMarker = CustomSampler.Create("LucentPath.DXR.RTASIntersectResolve", true);
        private static readonly CustomSampler _resolveSecondaryGuidesMarker = CustomSampler.Create("LucentPath.DXR.ResolveSecondaryGuides", true);
        private static readonly CustomSampler _accumulateMarker = CustomSampler.Create("LucentPath.DXR.AccumulateFrame", true);
        private static readonly CustomSampler _primaryShadeMarker = CustomSampler.Create("LucentPath.DXR.Shade.Primary", true);
        private static readonly CustomSampler _bounceShadeMarker = CustomSampler.Create("LucentPath.DXR.Shade.Bounce", true);
        private static readonly CustomSampler _deltaReflectionPlaneMarker = CustomSampler.Create("LucentPath.DXR.DeltaReflectionPlane", true);
        private static readonly uint[] _deltaReflectionArgsReset = { 0u, 1u, 1u };

        public DXRPathTracingRayGenPass(DXRPipelineResources context, DXRKernelLibrary kernels)
        {
            _resources = context;
            _kernels = kernels;
        }

        public void Dispatch(
            CommandBuffer cmd,
            Camera camera,
            CameraState state,
            int lightCount,
            int analyticAreaLightCount,
            Matrix4x4 viewMatrix,
            Matrix4x4 viewProj,
            Matrix4x4 nonJitteredViewProj,
            Matrix4x4 invViewProj,
            Matrix4x4 prevViewMatrix,
            Matrix4x4 prevViewProj,
            Vector3 prevCameraWorldPos,
            float nearClipNdc,
            float farClipNdc,
            bool isOrthographic,
            CameraLensState lensState,
            Vector3 cameraWorldPos,
            int frameIndex,
            bool hasValidHistory)
        {
            cmd.BeginSample(_marker);

            {
                var samplesPerPixel = Mathf.Max(1, _resources.Asset.PathTracingSettings.SamplesPerPixel);
                var surfaceBounces = Mathf.Max(1, _resources.Asset.PathTracingSettings.SurfaceBounces);
                var reflectionBounces = Mathf.Max(1, _resources.Asset.PathTracingSettings.ReflectionBounces);
                var transmissionBounces = Mathf.Max(1, _resources.Asset.PathTracingSettings.TransmissionBounces);
                var groupsX = Mathf.CeilToInt(state.Width / (float)DefaultThreadGroupSize);
                var groupsY = Mathf.CeilToInt(state.Height / (float)DefaultThreadGroupSize);
                var shadeNeededGroups = Mathf.CeilToInt(state.Width * state.Height / 64f);
                var shadeGroupsX = Mathf.Min(shadeNeededGroups, 65535);
                var shadeGroupsY = Mathf.CeilToInt(shadeNeededGroups / (float)shadeGroupsX);
                var shadeDispatchStride = shadeGroupsX * 64;
                var shader = _resources.PathTracingRayGenShader;

                for (var i = 0; i < _resources.FrameComputes.Length; i++)
                {
                    var frameCompute = _resources.FrameComputes[i];

                    _kernels.SetFrameComputeParameters(
                        cmd,
                        frameCompute.Compute,
                        state,
                        lightCount,
                        analyticAreaLightCount,
                        viewMatrix,
                        viewProj,
                        nonJitteredViewProj,
                        invViewProj,
                        prevViewMatrix,
                        prevViewProj,
                        prevCameraWorldPos,
                        cameraWorldPos,
                        nearClipNdc,
                        farClipNdc,
                        isOrthographic,
                        lensState,
                        frameIndex,
                        hasValidHistory);
                }

                cmd.SetRenderTarget(state.FrameSurfaceSignalSum.nameID);
                cmd.ClearRenderTarget(false, true, Color.clear);

                cmd.SetRenderTarget(state.FrameReflectionSignalSum.nameID);
                cmd.ClearRenderTarget(false, true, Color.clear);

                cmd.SetRenderTarget(state.FrameTransmissionSignalSum.nameID);
                cmd.ClearRenderTarget(false, true, Color.clear);
                cmd.SetRenderTarget(state.FrameShadowSum.nameID);
                cmd.ClearRenderTarget(false, true, Color.clear);
                cmd.SetRenderTarget(state.FrameEmissionSum.nameID);
                cmd.ClearRenderTarget(false, true, Color.clear);
                cmd.SetRenderTarget(state.PrimaryDiffuseAlbedo.nameID);
                cmd.ClearRenderTarget(false, true, Color.white);
                cmd.SetRenderTarget(state.PrimarySpecularF0.nameID);
                cmd.ClearRenderTarget(false, true, Color.white);
                cmd.SetRenderTarget(state.PrimaryTransmissionTint.nameID);
                cmd.ClearRenderTarget(false, true, Color.white);
                cmd.SetRenderTarget(state.ReflectionHitDistance.nameID);
                cmd.ClearRenderTarget(false, true, Color.clear);
                cmd.SetRenderTarget(state.TransmissionHitDistance.nameID);
                cmd.ClearRenderTarget(false, true, Color.clear);
                cmd.SetRenderTarget(state.ReflectionGuideEvent.Event0.nameID);
                cmd.ClearRenderTarget(false, true, Color.clear);
                cmd.SetRenderTarget(state.ReflectionGuideEvent.Event1.nameID);
                cmd.ClearRenderTarget(false, true, Color.clear);
                cmd.SetRenderTarget(state.ReflectionGuideEvent.Event2.nameID);
                cmd.ClearRenderTarget(false, true, Color.clear);
                cmd.SetRenderTarget(state.ReflectionGuideEvent.Event3.nameID);
                cmd.ClearRenderTarget(false, true, Color.clear);
                cmd.SetRenderTarget(state.TransmissionGuideEvent.Event0.nameID);
                cmd.ClearRenderTarget(false, true, Color.clear);
                cmd.SetRenderTarget(state.TransmissionGuideEvent.Event1.nameID);
                cmd.ClearRenderTarget(false, true, Color.clear);
                cmd.SetRenderTarget(state.TransmissionGuideEvent.Event2.nameID);
                cmd.ClearRenderTarget(false, true, Color.clear);
                cmd.SetRenderTarget(state.TransmissionGuideEvent.Event3.nameID);
                cmd.ClearRenderTarget(false, true, Color.clear);

                _kernels.BindResolveSecondaryGuideResources(cmd, state);
                _kernels.BindTemporalAccumulationResources(cmd, state);

                _kernels.SetFrameRayTracingParameters(
                    cmd,
                    shader,
                    state,
                    lightCount,
                    analyticAreaLightCount,
                    viewMatrix,
                    viewProj,
                    nonJitteredViewProj,
                    invViewProj,
                    prevViewMatrix,
                    prevViewProj,
                    prevCameraWorldPos,
                    cameraWorldPos,
                    nearClipNdc,
                    farClipNdc,
                    isOrthographic,
                    lensState,
                    frameIndex,
                    hasValidHistory);

                cmd.SetRayTracingAccelerationStructure(shader, "_RayTracingAccelerationStructure", _resources.RTAS);
                cmd.SetRayTracingBufferParam(shader, "_Lights", _resources.LightBuffer);
                cmd.SetRayTracingBufferParam(shader, "_SobolMatricesBuffer", _resources.SobolMatricesBuffer);
                cmd.SetRayTracingBufferParam(shader, "_PathStates", state.PathStateBuffer);
                cmd.SetRayTracingBufferParam(shader, PathFlagsProperty, state.PathFlagsBuffer);
                cmd.SetRayTracingBufferParam(shader, HitGBufferProperty, state.HitGBuffer);
                cmd.SetRayTracingBufferParam(shader, ActivePathIndicesProperty, state.ActivePathIndicesBuffer);
                cmd.SetRayTracingBufferParam(shader, DeltaReflectionPathIndicesProperty, state.DeltaReflectionPathIndicesBuffer);
                cmd.SetRayTracingBufferParam(shader, DeltaReflectionArgsProperty, state.DeltaReflectionArgsBuffer);

                if (_resources.MaterialCache != null)
                {
                    cmd.SetRayTracingBufferParam(shader, MaterialCacheProperty, _resources.MaterialCache);
                    cmd.SetRayTracingIntParam(shader, MaterialCacheCapacityProperty, _resources.MaterialCache.count);
                }

                if (_resources.EmissiveSurfelBuffer != null && _resources.EmissiveSurfelCounter != null && _resources.EmissiveSurfelOccupancy != null)
                {
                    cmd.SetRayTracingBufferParam(shader, "_EmissiveSurfels", _resources.EmissiveSurfelBuffer);
                    cmd.SetRayTracingBufferParam(shader, "_EmissiveSurfelCounter", _resources.EmissiveSurfelCounter);
                    cmd.SetRayTracingBufferParam(shader, "_EmissiveSurfelOccupancy", _resources.EmissiveSurfelOccupancy);
                    cmd.SetRayTracingIntParam(shader, "_EmissiveSurfelCapacity", _resources.EmissiveSurfelBuffer.count);
                }

                cmd.SetRayTracingBufferParam(shader, "_PathDistances", state.PathDistanceBuffer);

                if (state.RadianceCacheBuffer != null)
                {
                    var guidingSettings = _resources.Asset.RadianceCacheSettings;

                    cmd.SetRayTracingBufferParam(shader, "_RadianceCache", state.RadianceCacheBuffer);

                    cmd.SetRayTracingIntParam(
                        shader,
                        "_RadianceCacheCapacity",
                        state.RadianceCacheBuffer.count / DXRCameraResourceCache.RadianceCacheEntryStride);

                    cmd.SetRayTracingIntParam(shader, "_RadianceCacheEnabled", guidingSettings != null && guidingSettings.IsEnabled ? 1 : 0);

                    cmd.SetRayTracingIntParam(
                        shader,
                        "_RadianceCacheMinSamples",
                        guidingSettings != null ? Mathf.Max(1, guidingSettings.MinSamples) : 16);

                    cmd.SetRayTracingIntParam(shader, "_RadianceCacheMinDepth", guidingSettings != null ? Mathf.Max(0, guidingSettings.MinDepth) : 1);

                    cmd.SetRayTracingFloatParam(
                        shader,
                        "_RadianceCacheBaseCellSize",
                        guidingSettings != null ? Mathf.Max(1e-4f, guidingSettings.BaseCellSize) : .05f);

                    cmd.SetRayTracingFloatParam(
                        shader,
                        "_RadianceCacheLodScale",
                        guidingSettings != null ? Mathf.Max(1e-4f, guidingSettings.LodScale) : 1f);

                    cmd.SetRayTracingFloatParam(
                        shader,
                        "_RadianceCacheCorrectionProbability",
                        guidingSettings != null ? Mathf.Clamp(guidingSettings.CorrectionProbability, .05f, 1f) : .5f);
                }

                cmd.SetRayTracingTextureParam(shader, "_FrameSurfaceSignalSum", state.FrameSurfaceSignalSum.nameID);
                cmd.SetRayTracingTextureParam(shader, "_FrameReflectionSignalSum", state.FrameReflectionSignalSum.nameID);
                cmd.SetRayTracingTextureParam(shader, "_FrameTransmissionSignalSum", state.FrameTransmissionSignalSum.nameID);
                cmd.SetRayTracingTextureParam(shader, "_FrameShadowSum", state.FrameShadowSum.nameID);
                cmd.SetRayTracingTextureParam(shader, "_FrameEmissionSum", state.FrameEmissionSum.nameID);

                var fireflySettings = _resources.Asset.FireflySuppressionSettings;

                cmd.SetRayTracingFloatParam(
                    shader,
                    "_IndirectClampHistoryScale",
                    1f / Mathf.Max(1, _resources.Asset.TemporalHistorySettings.MaxHistoryLength));

                cmd.SetRayTracingTextureParam(
                    shader,
                    "_IndirectClampHistoryLutSurface",
                    fireflySettings != null && fireflySettings.IndirectClampHistoryLutSurface != null ? (Texture)fireflySettings.IndirectClampHistoryLutSurface : Texture2D.whiteTexture);

                cmd.SetRayTracingTextureParam(
                    shader,
                    "_IndirectClampHistoryLutReflection",
                    fireflySettings != null && fireflySettings.IndirectClampHistoryLutReflection != null ? (Texture)fireflySettings.IndirectClampHistoryLutReflection : Texture2D.whiteTexture);

                cmd.SetRayTracingTextureParam(
                    shader,
                    "_IndirectClampHistoryLutTransmission",
                    fireflySettings != null && fireflySettings.IndirectClampHistoryLutTransmission != null ? (Texture)fireflySettings.IndirectClampHistoryLutTransmission : Texture2D.whiteTexture);

                cmd.SetRayTracingTextureParam(shader, "_IndirectClampHistoryMomentsSurface", state.SurfaceSignal.PrevMoments.nameID);
                cmd.SetRayTracingTextureParam(shader, "_IndirectClampHistoryMomentsReflection", state.ReflectionSignal.PrevMoments.nameID);
                cmd.SetRayTracingTextureParam(shader, "_IndirectClampHistoryMomentsTransmission", state.TransmissionSignal.PrevMoments.nameID);
                cmd.SetRayTracingTextureParam(shader, "_PrimaryDiffuseAlbedo", state.PrimaryDiffuseAlbedo.nameID);
                cmd.SetRayTracingTextureParam(shader, "_PrimarySpecularF0", state.PrimarySpecularF0.nameID);
                cmd.SetRayTracingTextureParam(shader, "_PrimaryTransmissionTint", state.PrimaryTransmissionTint.nameID);
                cmd.SetRayTracingTextureParam(shader, "_ReflectionHitDistance", state.ReflectionHitDistance.nameID);
                cmd.SetRayTracingTextureParam(shader, "_TransmissionHitDistance", state.TransmissionHitDistance.nameID);
                cmd.SetRayTracingTextureParam(shader, "_Guide0", state.SurfaceSignal.Guide0.nameID);
                cmd.SetRayTracingTextureParam(shader, "_Guide1", state.SurfaceSignal.Guide1.nameID);
                cmd.SetRayTracingTextureParam(shader, "_Guide2", state.SurfaceSignal.Guide2.nameID);
                cmd.SetRayTracingTextureParam(shader, "_ShadowGuide0", state.ShadowSignal.Guide0.nameID);
                cmd.SetRayTracingTextureParam(shader, "_ShadowGuide1", state.ShadowSignal.Guide1.nameID);
                cmd.SetRayTracingTextureParam(shader, "_ShadowGuide2", state.ShadowSignal.Guide2.nameID);
                cmd.SetRayTracingTextureParam(shader, "_PrimaryHitDistance", state.PrimaryHitDistance.nameID);
                cmd.SetRayTracingTextureParam(shader, "_PrimaryRoughness", state.PrimaryRoughness.nameID);
                cmd.SetRayTracingTextureParam(shader, "_ReflectionGuideEvent0", state.ReflectionGuideEvent.Event0.nameID);
                cmd.SetRayTracingTextureParam(shader, "_ReflectionGuideEvent1", state.ReflectionGuideEvent.Event1.nameID);
                cmd.SetRayTracingTextureParam(shader, "_ReflectionGuideEvent2", state.ReflectionGuideEvent.Event2.nameID);
                cmd.SetRayTracingTextureParam(shader, "_ReflectionGuideEvent3", state.ReflectionGuideEvent.Event3.nameID);
                cmd.SetRayTracingTextureParam(shader, "_TransmissionGuideEvent0", state.TransmissionGuideEvent.Event0.nameID);
                cmd.SetRayTracingTextureParam(shader, "_TransmissionGuideEvent1", state.TransmissionGuideEvent.Event1.nameID);
                cmd.SetRayTracingTextureParam(shader, "_TransmissionGuideEvent2", state.TransmissionGuideEvent.Event2.nameID);
                cmd.SetRayTracingTextureParam(shader, "_TransmissionGuideEvent3", state.TransmissionGuideEvent.Event3.nameID);
                cmd.SetRayTracingIntParam(shader, "_Width", state.Width);
                cmd.SetRayTracingIntParam(shader, "_Height", state.Height);
                cmd.SetRayTracingIntParam(shader, "_IsOrthographic", isOrthographic ? 1 : 0);
                cmd.SetRayTracingFloatParam(shader, "_NearClipNdc", nearClipNdc);
                cmd.SetRayTracingFloatParam(shader, "_FarClipNdc", farClipNdc);
                lensState.BindRayTracing(cmd, shader);
                cmd.SetRayTracingFloatParam(shader, "_DirectIntensity", Mathf.Max(0f, _resources.Asset.PathTracingSettings.DirectIntensity));
                cmd.SetRayTracingFloatParam(shader, "_BounceMaxDistance", Mathf.Max(.001f, _resources.Asset.PathTracingSettings.MaxBounceDistance));
                cmd.SetRayTracingFloatParam(shader, "_SurfaceBias", Mathf.Max(.00001f, _resources.Asset.PathTracingSettings.SurfaceBias));

                cmd.SetRayTracingFloatParam(
                    shader,
                    "_IndirectClampMultiplierSurface",
                    Mathf.Max(0f, _resources.Asset.FireflySuppressionSettings.IndirectClampMultiplierSurface));

                cmd.SetRayTracingFloatParam(
                    shader,
                    "_IndirectClampMultiplierReflection",
                    Mathf.Max(0f, _resources.Asset.FireflySuppressionSettings.IndirectClampMultiplierReflection));

                cmd.SetRayTracingFloatParam(
                    shader,
                    "_IndirectClampMultiplierTransmission",
                    Mathf.Max(0f, _resources.Asset.FireflySuppressionSettings.IndirectClampMultiplierTransmission));

                cmd.SetRayTracingIntParam(shader, "_SurfaceBounces", surfaceBounces);
                cmd.SetRayTracingIntParam(shader, "_ReflectionBounces", reflectionBounces);
                cmd.SetRayTracingIntParam(shader, "_TransmissionBounces", transmissionBounces);
                cmd.SetRayTracingIntParam(shader, "_LightCount", lightCount);
                cmd.SetRayTracingIntParam(shader, "_AnalyticAreaLightCount", analyticAreaLightCount);
                cmd.SetRayTracingVectorParam(shader, "_CameraWorldPos", cameraWorldPos);
                cmd.SetRayTracingVectorParam(shader, "_PrevCameraWorldPos", prevCameraWorldPos);
                cmd.SetRayTracingMatrixParam(shader, "_InvViewProjMatrix", invViewProj);
                cmd.SetRayTracingMatrixParam(shader, "_PrevViewProjMatrix", prevViewProj);
                cmd.SetRayTracingMatrixParam(shader, "_ViewMatrix", viewMatrix);
                cmd.SetRayTracingMatrixParam(shader, "_PrevViewMatrix", prevViewMatrix);
                cmd.SetRayTracingShaderPass(shader, "RayTracing");

                var shadeActive = _resources.ShadeAdvance.Compute != null;

                if (shadeActive)
                {
                    _kernels.BindPathTracingResources(cmd, state, _resources.ShadeNee);
                    _kernels.BindPathTracingResources(cmd, state, _resources.ShadeAdvance);
                    cmd.SetComputeIntParam(_resources.ShadeNee.Compute, ShadeDispatchStrideProperty, shadeDispatchStride);
                    cmd.SetComputeBufferParam(_resources.ShadeNee.Compute, _resources.ShadeNee.Kernel, HitGBufferProperty, state.HitGBuffer);
                    cmd.SetComputeBufferParam(_resources.ShadeAdvance.Compute, _resources.ShadeAdvance.Kernel, HitGBufferProperty, state.HitGBuffer);

                    if (_resources.MaterialCache != null)
                    {
                        cmd.SetComputeBufferParam(
                            _resources.ShadeNee.Compute,
                            _resources.ShadeNee.Kernel,
                            MaterialCacheProperty,
                            _resources.MaterialCache);

                        cmd.SetComputeBufferParam(
                            _resources.ShadeAdvance.Compute,
                            _resources.ShadeAdvance.Kernel,
                            MaterialCacheProperty,
                            _resources.MaterialCache);

                        cmd.SetComputeIntParam(_resources.ShadeAdvance.Compute, MaterialCacheCapacityProperty, _resources.MaterialCache.count);
                    }
                }

                var compactActive = shadeActive && _resources.CompactPaths.Compute != null && _resources.ClearPathArgs.Compute != null;

                if (compactActive)
                {
                    cmd.SetComputeBufferParam(
                        _resources.ClearPathArgs.Compute,
                        _resources.ClearPathArgs.Kernel,
                        DispatchArgsProperty,
                        state.DispatchArgsBuffer);

                    cmd.SetComputeBufferParam(
                        _resources.CompactPaths.Compute,
                        _resources.CompactPaths.Kernel,
                        PathFlagsProperty,
                        state.PathFlagsBuffer);

                    cmd.SetComputeBufferParam(
                        _resources.CompactPaths.Compute,
                        _resources.CompactPaths.Kernel,
                        ActivePathIndicesProperty,
                        state.ActivePathIndicesBuffer);

                    cmd.SetComputeBufferParam(
                        _resources.CompactPaths.Compute,
                        _resources.CompactPaths.Kernel,
                        SimplePathIndicesProperty,
                        state.SimplePathIndicesBuffer);

                    cmd.SetComputeBufferParam(
                        _resources.CompactPaths.Compute,
                        _resources.CompactPaths.Kernel,
                        ComplexPathIndicesProperty,
                        state.ComplexPathIndicesBuffer);

                    cmd.SetComputeBufferParam(_resources.CompactPaths.Compute, _resources.CompactPaths.Kernel, HitGBufferProperty, state.HitGBuffer);

                    cmd.SetComputeBufferParam(
                        _resources.CompactPaths.Compute,
                        _resources.CompactPaths.Kernel,
                        DispatchArgsProperty,
                        state.DispatchArgsBuffer);

                    cmd.SetComputeBufferParam(
                        _resources.BuildShadeArgs.Compute,
                        _resources.BuildShadeArgs.Kernel,
                        DispatchArgsProperty,
                        state.DispatchArgsBuffer);

                    cmd.SetComputeBufferParam(
                        _resources.ShadeNee.Compute,
                        _resources.ShadeNee.Kernel,
                        ActivePathIndicesProperty,
                        state.ActivePathIndicesBuffer);

                    cmd.SetComputeBufferParam(
                        _resources.ShadeNee.Compute,
                        _resources.ShadeNee.Kernel,
                        DispatchArgsProperty,
                        state.DispatchArgsBuffer);

                    cmd.SetComputeBufferParam(
                        _resources.ShadeAdvance.Compute,
                        _resources.ShadeAdvance.Kernel,
                        ActivePathIndicesProperty,
                        state.ActivePathIndicesBuffer);

                    cmd.SetComputeBufferParam(
                        _resources.ShadeAdvance.Compute,
                        _resources.ShadeAdvance.Kernel,
                        DispatchArgsProperty,
                        state.DispatchArgsBuffer);
                }

                var sampleIndexBase = state.SampleCount * samplesPerPixel;
                var dispatchBounces = Mathf.Max(surfaceBounces, Mathf.Max(reflectionBounces, transmissionBounces));

                for (var sample = 0; sample < samplesPerPixel; sample++)
                {
                    var currentSampleIndex = sampleIndexBase + sample;
                    var writePrimaryGuides = sample == 0 ? 1 : 0;

                    cmd.SetGlobalInt(PathPlaneProperty, PathPlaneMain);
                    cmd.SetBufferData(state.DeltaReflectionArgsBuffer, _deltaReflectionArgsReset);
                    cmd.SetRayTracingIntParam(shader, CurrentSampleIndexProperty, currentSampleIndex);
                    cmd.SetRayTracingIntParam(shader, UseCompactPathsProperty, 0);
                    cmd.SetRayTracingIntParam(shader, WritePrimaryGuidesProperty, writePrimaryGuides);

                    cmd.BeginSample(_primarySampleMarker);
                    cmd.DispatchRays(shader, "RayGenPrimarySample", (uint)state.Width, (uint)state.Height, 1);
                    cmd.EndSample(_primarySampleMarker);

                    DispatchPathPlane(
                        cmd,
                        state,
                        shader,
                        currentSampleIndex,
                        1,
                        dispatchBounces,
                        shadeActive,
                        compactActive,
                        groupsX,
                        groupsY,
                        shadeGroupsX,
                        shadeGroupsY);

                    cmd.SetGlobalInt(PathPlaneProperty, PathPlaneDeltaReflection);
                    cmd.SetRayTracingIntParam(shader, UseCompactPathsProperty, 0);

                    cmd.BeginSample(_deltaReflectionPlaneMarker);
                    cmd.DispatchRays(shader, "RayGenDeltaReflectionPlane", state.DeltaReflectionArgsBuffer, 0);
                    cmd.EndSample(_deltaReflectionPlaneMarker);

                    DispatchPathPlane(
                        cmd,
                        state,
                        shader,
                        currentSampleIndex,
                        2,
                        dispatchBounces,
                        shadeActive,
                        compactActive,
                        groupsX,
                        groupsY,
                        shadeGroupsX,
                        shadeGroupsY);

                    cmd.SetGlobalInt(PathPlaneProperty, PathPlaneSecondDeltaReflection);
                    cmd.SetRayTracingIntParam(shader, UseCompactPathsProperty, 0);

                    cmd.BeginSample(_deltaReflectionPlaneMarker);
                    cmd.DispatchRays(shader, "RayGenSecondDeltaReflectionPlane", state.DeltaReflectionArgsBuffer, 0);
                    cmd.EndSample(_deltaReflectionPlaneMarker);

                    DispatchPathPlane(
                        cmd,
                        state,
                        shader,
                        currentSampleIndex,
                        2,
                        dispatchBounces,
                        shadeActive,
                        compactActive,
                        groupsX,
                        groupsY,
                        shadeGroupsX,
                        shadeGroupsY);
                }

                cmd.SetGlobalInt(PathPlaneProperty, PathPlaneUnsplit);

                cmd.BeginSample(_resolveSecondaryGuidesMarker);

                DXRKernelLibrary.DispatchCompute(
                    cmd,
                    _resources.ResolveSecondaryGuides,
                    Mathf.CeilToInt(state.Width / 8f),
                    Mathf.CeilToInt(state.Height / 4f));

                cmd.EndSample(_resolveSecondaryGuidesMarker);

                _kernels.DispatchCheckerboardResolve(cmd, state, groupsX, groupsY);

                cmd.BeginSample(_accumulateMarker);
                _kernels.DispatchAccumulateFrame(cmd, state, groupsX, groupsY);
                cmd.EndSample(_accumulateMarker);
            }

            cmd.EndSample(_marker);
        }

        public void Dispose()
        { }

        private void DispatchPathPlane(
            CommandBuffer cmd,
            CameraState state,
            RayTracingShader shader,
            int currentSampleIndex,
            int firstBounce,
            int dispatchBounces,
            bool shadeActive,
            bool compactActive,
            int groupsX,
            int groupsY,
            int shadeGroupsX,
            int shadeGroupsY)
        {
            if (shadeActive)
            {
                cmd.BeginSample(_primaryShadeMarker);
                cmd.SetComputeIntParam(_resources.ShadeAdvance.Compute, CurrentSampleIndexProperty, currentSampleIndex);

                if (compactActive)
                {
                    DispatchPathCompaction(cmd, groupsX, groupsY);
                }

                DispatchShadeBuckets(cmd, state, compactActive, shadeGroupsX, shadeGroupsY);
                cmd.EndSample(_primaryShadeMarker);
            }

            for (var bounce = firstBounce; bounce <= dispatchBounces; bounce++)
            {
                cmd.BeginSample(_intersectMarker);
                cmd.SetRayTracingIntParam(shader, CurrentSampleIndexProperty, currentSampleIndex);

                if (compactActive)
                {
                    DispatchPathCompaction(cmd, groupsX, groupsY);
                    cmd.SetRayTracingIntParam(shader, UseCompactPathsProperty, 1);
                    cmd.DispatchRays(shader, "RayGenMain", state.DispatchArgsBuffer, 0);
                }
                else
                {
                    cmd.SetRayTracingIntParam(shader, UseCompactPathsProperty, 0);
                    cmd.DispatchRays(shader, "RayGenMain", (uint)state.Width, (uint)state.Height, 1);
                }

                cmd.EndSample(_intersectMarker);

                if (shadeActive == false || bounce == dispatchBounces)
                {
                    continue;
                }

                cmd.BeginSample(_bounceShadeMarker);
                cmd.SetComputeIntParam(_resources.ShadeAdvance.Compute, CurrentSampleIndexProperty, currentSampleIndex);
                DispatchShadeBuckets(cmd, state, compactActive, shadeGroupsX, shadeGroupsY);
                cmd.EndSample(_bounceShadeMarker);
            }
        }

        private void DispatchPathCompaction(CommandBuffer cmd, int groupsX, int groupsY)
        {
            DXRKernelLibrary.DispatchCompute(cmd, _resources.ClearPathArgs, 1, 1);
            DXRKernelLibrary.DispatchCompute(cmd, _resources.CompactPaths, groupsX, groupsY);
            DXRKernelLibrary.DispatchCompute(cmd, _resources.BuildShadeArgs, 1, 1);
        }

        private void DispatchShadeBuckets(CommandBuffer cmd, CameraState state, bool compactActive, int shadeGroupsX, int shadeGroupsY)
        {
            if (compactActive == false)
            {
                cmd.DisableShaderKeyword("LUCENT_SHADE_SIMPLE");
                cmd.SetComputeIntParam(_resources.ShadeNee.Compute, UseCompactPathsProperty, 0);
                DXRKernelLibrary.DispatchCompute(cmd, _resources.ShadeNee, shadeGroupsX, shadeGroupsY);
                DXRKernelLibrary.DispatchCompute(cmd, _resources.ShadeAdvance, shadeGroupsX, shadeGroupsY);

                return;
            }

            cmd.SetComputeIntParam(_resources.ShadeNee.Compute, UseCompactPathsProperty, 1);

            cmd.EnableShaderKeyword("LUCENT_SHADE_SIMPLE");
            DispatchShadeBucket(cmd, state, state.SimplePathIndicesBuffer, 3, 11, 32);

            cmd.DisableShaderKeyword("LUCENT_SHADE_SIMPLE");
            DispatchShadeBucket(cmd, state, state.ComplexPathIndicesBuffer, 4, 15, 48);
        }

        private void DispatchShadeBucket(CommandBuffer cmd, CameraState state, ComputeBuffer pathIndices, int countIndex, int strideIndex, uint argsOffset)
        {
            cmd.SetComputeIntParam(_resources.ShadeNee.Compute, ShadeCountIndexProperty, countIndex);
            cmd.SetComputeIntParam(_resources.ShadeNee.Compute, ShadeStrideIndexProperty, strideIndex);

            cmd.SetComputeBufferParam(
                _resources.ShadeNee.Compute,
                _resources.ShadeNee.Kernel,
                ActivePathIndicesProperty,
                pathIndices);

            cmd.SetComputeBufferParam(
                _resources.ShadeAdvance.Compute,
                _resources.ShadeAdvance.Kernel,
                ActivePathIndicesProperty,
                pathIndices);

            cmd.DispatchCompute(_resources.ShadeNee.Compute, _resources.ShadeNee.Kernel, state.DispatchArgsBuffer, argsOffset);
            cmd.DispatchCompute(_resources.ShadeAdvance.Compute, _resources.ShadeAdvance.Kernel, state.DispatchArgsBuffer, argsOffset);
        }
    }
}
