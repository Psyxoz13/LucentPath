using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using static LucentPath.Rendering.PipelineShaderProperties;

namespace LucentPath.Rendering
{
    internal class DXRKernelLibrary
    {
        private readonly DXRPipelineResources _pipelineResources;
        private EnvironmentImportanceSampler _envSampler;

        private MultiScatterLut _msLut;

        public DXRKernelLibrary(DXRPipelineResources context)
        {
            _pipelineResources = context;
        }

        public void SetEnvironmentImportanceSampler(EnvironmentImportanceSampler sampler)
        {
            _envSampler = sampler;
        }

        public void SetMultiScatterLut(MultiScatterLut lut)
        {
            _msLut = lut;
        }

        public void Refresh()
        {
            _pipelineResources.ResolveSecondaryGuides = FindKernel(
                _pipelineResources.Asset.ResolveSecondaryGuidesCompute,
                "Resolve Secondary Guides",
                "CSResolveSecondaryGuides");

            _pipelineResources.AccumulateFrame = FindKernel(_pipelineResources.Asset.AccumulateFrameCompute, "Accumulate Frame", "CSAccumulateFrame");

            _pipelineResources.BuildSurfaceFastHistory = FindKernel(
                _pipelineResources.Asset.AccumulateFrameCompute,
                "Build Surface Fast History",
                "CSBuildSurfaceFastHistory");

            _pipelineResources.AccumulateFrameSpecular = FindKernel(
                _pipelineResources.Asset.AccumulateFrameCompute,
                "Accumulate Frame Specular",
                "CSAccumulateFrameSpecular");

            _pipelineResources.CheckerboardResolve = FindKernel(
                _pipelineResources.Asset.AccumulateFrameCompute,
                "Checkerboard Resolve",
                "CSCheckerboardResolve");

            _pipelineResources.PrepareBuffers = FindKernel(_pipelineResources.Asset.PrepareBuffersCompute, "Prepare Buffers", "CSPrepareBuffers");

            _pipelineResources.DecayGuidingField = FindKernel(
                _pipelineResources.Asset.PrepareBuffersCompute,
                "Decay Guiding Field",
                "CSDecayGuidingField");

            _pipelineResources.BuildLightingAliases = FindKernel(
                _pipelineResources.Asset.AccumulateFrameCompute,
                "Build Lighting Aliases",
                "CSBuildLightingAliases");

            _pipelineResources.AccumulateSecondary = FindKernel(
                _pipelineResources.Asset.AccumulateFrameCompute,
                "Accumulate Secondary",
                "CSAccumulateSecondary");

            _pipelineResources.ReflectionPrePass = FindKernel(
                _pipelineResources.Asset.AccumulateFrameCompute,
                "Reflection Pre-Pass",
                "CSReflectionPrePass");

            _pipelineResources.ClearSecondaryArgs = FindKernel(
                _pipelineResources.Asset.AccumulateFrameCompute,
                "Clear Secondary Args",
                "CSClearSecondaryArgs");

            _pipelineResources.CompactSecondary = FindKernel(
                _pipelineResources.Asset.AccumulateFrameCompute,
                "Compact Secondary",
                "CSCompactSecondary");

            _pipelineResources.FinalizeSecondaryArgs = FindKernel(
                _pipelineResources.Asset.AccumulateFrameCompute,
                "Finalize Secondary Args",
                "CSFinalizeSecondaryArgs");

            _pipelineResources.BootstrapSurface = FindKernel(
                _pipelineResources.Asset.AccumulateFrameCompute,
                "Bootstrap Surface",
                "CSBootstrapSurface");

            _pipelineResources.ClearBootstrapArgs = FindKernel(
                _pipelineResources.Asset.AccumulateFrameCompute,
                "Clear Bootstrap Args",
                "CSClearBootstrapArgs");

            _pipelineResources.FinalizeBootstrapArgs = FindKernel(
                _pipelineResources.Asset.AccumulateFrameCompute,
                "Finalize Bootstrap Args",
                "CSFinalizeBootstrapArgs");

            _pipelineResources.HistoryFix = FindKernel(_pipelineResources.Asset.AtrousFilterCompute, "A-trous History Fix", "CSHistoryFix");
            _pipelineResources.AtrousFilter = FindKernel(_pipelineResources.Asset.AtrousFilterCompute, "Atrous Filter", "CSAtrousFilter");

            var shadeCompute = _pipelineResources.Asset.ShadeCompute;

            _pipelineResources.ShadeNee = shadeCompute != null
                ? FindKernel(shadeCompute, "Shade NEE", "CSShadeNee")
                : default;

            _pipelineResources.ShadeAdvance = shadeCompute != null
                ? FindKernel(shadeCompute, "Shade Advance", "CSShadeAdvance")
                : default;

            var compactCompute = _pipelineResources.Asset.CompactPathsCompute;

            _pipelineResources.ClearPathArgs = compactCompute != null
                ? FindKernel(compactCompute, "Clear Path Args", "CSClearPathArgs")
                : default;

            _pipelineResources.CompactPaths = compactCompute != null
                ? FindKernel(compactCompute, "Compact Paths", "CSCompactPaths")
                : default;

            _pipelineResources.BuildShadeArgs = compactCompute != null
                ? FindKernel(compactCompute, "Build Shade Args", "CSBuildShadeArgs")
                : default;

            var taaCompute = _pipelineResources.Asset.TaaCompute;

            _pipelineResources.TaaResolve = taaCompute != null
                ? FindKernel(taaCompute, "TAA Resolve", "CSTaaResolve")
                : default;

            _pipelineResources.AntiFirefly = taaCompute != null
                ? FindKernel(taaCompute, "Anti-Firefly", "CSAntiFirefly")
                : default;

            var baseFrameComputes = new[]
            {
                _pipelineResources.ResolveSecondaryGuides,
                _pipelineResources.AccumulateFrame,
                _pipelineResources.AccumulateFrameSpecular,
                _pipelineResources.PrepareBuffers,
                _pipelineResources.BuildLightingAliases,
                _pipelineResources.HistoryFix,
                _pipelineResources.AtrousFilter
            };

            if (shadeCompute != null)
            {
                baseFrameComputes = baseFrameComputes.Concat(new[] { _pipelineResources.ShadeNee, _pipelineResources.ShadeAdvance }).ToArray();
            }

            if (compactCompute != null)
            {
                baseFrameComputes = baseFrameComputes.Concat(new[] { _pipelineResources.CompactPaths, _pipelineResources.ClearPathArgs, _pipelineResources.BuildShadeArgs }).ToArray();
            }

            _pipelineResources.FrameComputes = taaCompute != null
                            ? baseFrameComputes.Concat(new[] { _pipelineResources.TaaResolve }).ToArray()
                            : baseFrameComputes;

            _pipelineResources.PathTracingRayGenShader = FindRayTracingShader(
                _pipelineResources.Asset.PathTracingRayGenShader,
                "Path Tracing Ray Gen");
        }

        public void SetFrameComputeParameters(
            CommandBuffer cmd,
            ComputeShader compute,
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
            Vector3 cameraWorldPos,
            float nearClipNdc,
            float farClipNdc,
            bool isOrthographic,
            CameraLensState lensState,
            int frameIndex,
            bool hasValidHistory)
        {
            cmd.SetComputeIntParam(compute, AccumulationSampleCountProperty, Mathf.Max(0, state.SampleCount));
            cmd.SetComputeIntParam(compute, WidthProperty, state.Width);
            cmd.SetComputeIntParam(compute, HeightProperty, state.Height);
            cmd.SetComputeIntParam(compute, LightCountProperty, lightCount);
            cmd.SetComputeIntParam(compute, AnalyticAreaLightCountProperty, analyticAreaLightCount);
            cmd.SetComputeIntParam(compute, FrameIndexProperty, frameIndex);
            cmd.SetComputeIntParam(compute, FrameParityProperty, frameIndex & 1);
            cmd.SetComputeIntParam(compute, EnableCheckerboardProperty, _pipelineResources.Asset.CheckerboardSettings.IsEnabled ? 1 : 0);
            cmd.SetComputeIntParam(compute, IsOrthographicProperty, isOrthographic ? 1 : 0);
            cmd.SetComputeIntParam(compute, HasValidHistoryProperty, hasValidHistory ? 1 : 0);
            cmd.SetComputeFloatParam(compute, NearClipNdcProperty, nearClipNdc);
            cmd.SetComputeFloatParam(compute, FarClipNdcProperty, farClipNdc);
            lensState.BindCompute(cmd, compute);
            cmd.SetComputeVectorParam(compute, CameraWorldPosProperty, cameraWorldPos);
            cmd.SetComputeVectorParam(compute, PrevCameraWorldPosProperty, prevCameraWorldPos);
            cmd.SetComputeMatrixParam(compute, InvViewProjMatrixProperty, invViewProj);
            cmd.SetComputeMatrixParam(compute, ViewProjMatrixProperty, viewProj);
            cmd.SetComputeMatrixParam(compute, NonJitteredViewProjMatrixProperty, nonJitteredViewProj);
            cmd.SetComputeMatrixParam(compute, ViewMatrixProperty, viewMatrix);
            cmd.SetComputeMatrixParam(compute, PrevViewProjMatrixProperty, prevViewProj);
            cmd.SetComputeMatrixParam(compute, PrevViewMatrixProperty, prevViewMatrix);

            cmd.SetComputeIntParam(compute, SamplesPerPixelProperty, Mathf.Max(1, _pipelineResources.Asset.PathTracingSettings.SamplesPerPixel));
            cmd.SetComputeIntParam(compute, SurfaceBouncesProperty, Mathf.Max(1, _pipelineResources.Asset.PathTracingSettings.SurfaceBounces));
            cmd.SetComputeIntParam(compute, ReflectionBouncesProperty, Mathf.Max(1, _pipelineResources.Asset.PathTracingSettings.ReflectionBounces));

            cmd.SetComputeIntParam(
                compute,
                TransmissionBouncesProperty,
                Mathf.Max(1, _pipelineResources.Asset.PathTracingSettings.TransmissionBounces));

            cmd.SetComputeFloatParam(
                compute,
                BounceMaxDistanceProperty,
                Mathf.Max(_pipelineResources.Asset.PathTracingSettings.MaxBounceDistance, .001f));

            cmd.SetComputeIntParam(
                compute,
                RussianRouletteStartProperty,
                Mathf.Max(1, _pipelineResources.Asset.PathTracingSettings.RussianRouletteStart));

            cmd.SetComputeFloatParam(compute, DirectIntensityProperty, _pipelineResources.Asset.PathTracingSettings.DirectIntensity);
            cmd.SetComputeFloatParam(compute, SurfaceBiasProperty, _pipelineResources.Asset.PathTracingSettings.SurfaceBias);
            cmd.SetComputeIntParam(compute, DebugVisualizationModeProperty, (int)_pipelineResources.Asset.PathTracingSettings.DebugVisualizationMode);

            cmd.SetComputeIntParam(
                compute,
                MaxHistoryLengthProperty,
                Mathf.Max(1, _pipelineResources.Asset.TemporalHistorySettings.MaxHistoryLength));

            cmd.SetComputeFloatParam(
                compute,
                LowConfidenceSpatialBlendProperty,
                Mathf.Clamp01(_pipelineResources.Asset.TemporalHistorySettings.LowConfidenceSpatialBlend));

            cmd.SetComputeFloatParam(
                compute,
                GuideMinSignalWeightProperty,
                Mathf.Max(0f, _pipelineResources.Asset.TemporalHistorySettings.GuideMinSignalWeight));

            cmd.SetComputeFloatParam(
                compute,
                GuidePlanarNormalThresholdProperty,
                Mathf.Clamp(_pipelineResources.Asset.TemporalHistorySettings.GuidePlanarNormalThreshold, .9f, 1f));

            cmd.SetComputeFloatParam(
                compute,
                GuideMaxVirtualProjectionErrorPixelsProperty,
                Mathf.Max(0f, _pipelineResources.Asset.TemporalHistorySettings.GuideMaxVirtualProjectionErrorPixels));

            cmd.SetComputeFloatParam(
                compute,
                GuideMaxPlaneMotionMetersProperty,
                Mathf.Max(0f, _pipelineResources.Asset.TemporalHistorySettings.GuideMaxPlaneMotionMeters));

            cmd.SetComputeFloatParam(
                compute,
                ShadowAntiLagSensitivityProperty,
                Mathf.Max(1f, _pipelineResources.Asset.TemporalHistorySettings.ShadowAntiLagSensitivity));

            cmd.SetComputeFloatParam(
                compute,
                ShadowAntiLagWarmupProperty,
                Mathf.Clamp01(_pipelineResources.Asset.TemporalHistorySettings.ShadowAntiLagWarmup));

            cmd.SetComputeIntParam(
                compute,
                ShadowAntiLagRadiusProperty,
                Mathf.Clamp(_pipelineResources.Asset.TemporalHistorySettings.ShadowAntiLagRadius, 0, 2));

            cmd.SetComputeFloatParam(
                compute,
                TemporalDepthRejectThresholdProperty,
                Mathf.Max(.001f, _pipelineResources.Asset.TemporalHistoryRejectionSettings.DepthRejectThreshold));

            cmd.SetComputeFloatParam(
                compute,
                TemporalNormalRejectThresholdProperty,
                Mathf.Clamp01(_pipelineResources.Asset.TemporalHistoryRejectionSettings.NormalRejectThreshold));

            var motionRejectSlider = _pipelineResources.Asset.TemporalHistoryRejectionSettings.ResolvedMotionRejectionStrength;
            var motionRejectScreenSideMax = Mathf.Max(Mathf.Max(state.Width, state.Height), 1);
            var motionRejectT = Mathf.InverseLerp(.01f, 1f, motionRejectSlider);
            var motionRejectThreshold = Mathf.Lerp(motionRejectScreenSideMax, 1f, motionRejectT);

            cmd.SetComputeFloatParam(compute, MotionRejectionStrengthProperty, motionRejectThreshold);

            cmd.SetComputeFloatParam(
                compute,
                ReflectionPrePassRadiusProperty,
                Mathf.Max(0f, _pipelineResources.Asset.ATrousSettings.ReflectionPrePassRadius));

            cmd.SetComputeFloatParam(
                compute,
                HistoryColorClipSigmaProperty,
                Mathf.Max(0f, _pipelineResources.Asset.TemporalHistoryRejectionSettings.HistoryColorClipSigma));

            cmd.SetComputeFloatParam(
                compute,
                HistoryDepthConfidenceScaleProperty,
                Mathf.Max(.25f, _pipelineResources.Asset.TemporalHistoryConfidenceSettings.DepthConfidenceScale));

            cmd.SetComputeFloatParam(
                compute,
                HistoryNormalConfidencePowerProperty,
                Mathf.Max(.25f, _pipelineResources.Asset.TemporalHistoryConfidenceSettings.NormalConfidencePower));

            cmd.SetComputeFloatParam(
                compute,
                HistoryConfidencePowerProperty,
                Mathf.Max(.01f, _pipelineResources.Asset.TemporalHistoryConfidenceSettings.ConfidencePower));

            cmd.SetComputeFloatParam(
                compute,
                HistoryConfidenceDitherStrengthProperty,
                Mathf.Clamp01(_pipelineResources.Asset.TemporalHistoryConfidenceSettings.ConfidenceDitherStrength));

            var disocclusion = _pipelineResources.Asset.DisocclusionRecoverySettings;

            cmd.SetComputeIntParam(compute, SpatialBootstrapRadiusProperty, Mathf.Clamp(disocclusion.SpatialBootstrapRadius, 0, 3));
            cmd.SetComputeFloatParam(compute, BootstrapMissThresholdProperty, Mathf.Clamp01(disocclusion.BootstrapCompactionThreshold));

            cmd.SetComputeFloatParam(
                compute,
                SpatialBootstrapDepthRejectThresholdProperty,
                Mathf.Max(.001f, disocclusion.SpatialBootstrapDepthRejectThreshold));

            cmd.SetComputeFloatParam(compute, SpatialBootstrapNormalThresholdProperty, Mathf.Clamp01(disocclusion.SpatialBootstrapNormalThreshold));
            cmd.SetComputeFloatParam(compute, SpatialBootstrapDepthPhiProperty, Mathf.Max(.01f, disocclusion.SpatialBootstrapDepthPhi));
            cmd.SetComputeFloatParam(compute, SpatialBootstrapNormalPhiProperty, Mathf.Max(.01f, disocclusion.SpatialBootstrapNormalPhi));
            cmd.SetComputeFloatParam(compute, HistoryMissSpatialBlendProperty, Mathf.Clamp01(disocclusion.HistoryMissSpatialBlend));
            cmd.SetComputeFloatParam(compute, SpatialBootstrapLuminancePhiProperty, Mathf.Max(0f, disocclusion.SpatialBootstrapLuminancePhi));
            cmd.SetComputeFloatParam(compute, SpatialBootstrapLuminanceFloorProperty, Mathf.Max(0f, disocclusion.SpatialBootstrapLuminanceFloor));
            cmd.SetComputeFloatParam(compute, SpatialBootstrapVarianceCutoffProperty, Mathf.Max(.001f, disocclusion.SpatialBootstrapVarianceCutoff));
            cmd.SetComputeFloatParam(compute, SpatialBootstrapVarianceMinBlendProperty, Mathf.Clamp01(disocclusion.SpatialBootstrapVarianceMinBlend));
            cmd.SetComputeIntParam(compute, YoungHistoryFramesProperty, Mathf.Clamp(disocclusion.YoungHistoryFrames, 0, 16));
            cmd.SetComputeIntParam(compute, YoungHistoryRadiusProperty, Mathf.Clamp(disocclusion.YoungHistoryRadius, 0, 4));
            cmd.SetComputeFloatParam(compute, YoungHistoryBlendProperty, Mathf.Clamp01(disocclusion.YoungHistoryBlend));
            cmd.SetComputeFloatParam(compute, YoungHistoryVarianceBoostProperty, Mathf.Max(1f, disocclusion.YoungHistoryVarianceBoost));

            cmd.SetComputeFloatParam(compute, AtrousDepthPhiProperty, Mathf.Max(.1f, _pipelineResources.Asset.ATrousSettings.AtrousDepthPhi));
            cmd.SetComputeFloatParam(compute, AtrousNormalPhiProperty, Mathf.Max(1f, _pipelineResources.Asset.ATrousSettings.AtrousNormalPhi));

            cmd.SetComputeFloatParam(
                compute,
                IndirectClampMultiplierSurfaceProperty,
                Mathf.Max(0f, _pipelineResources.Asset.FireflySuppressionSettings.IndirectClampMultiplierSurface));

            cmd.SetComputeFloatParam(
                compute,
                IndirectClampMultiplierReflectionProperty,
                Mathf.Max(0f, _pipelineResources.Asset.FireflySuppressionSettings.IndirectClampMultiplierReflection));

            cmd.SetComputeFloatParam(
                compute,
                IndirectClampMultiplierTransmissionProperty,
                Mathf.Max(0f, _pipelineResources.Asset.FireflySuppressionSettings.IndirectClampMultiplierTransmission));
        }

        public static void DispatchCompute(CommandBuffer cmd, ComputeKernel kernel, int groupsX, int groupsY)
        {
            cmd.DispatchCompute(kernel.Compute, kernel.Kernel, groupsX, groupsY, 1);
        }

        public void DispatchGuidingFieldDecay(CommandBuffer cmd, CameraState state)
        {
            var settings = _pipelineResources.Asset.RadianceCacheSettings;

            if (settings == null || settings.IsEnabled == false || state.RadianceCacheBuffer == null)
            {
                return;
            }

            var kernel = _pipelineResources.DecayGuidingField;

            if (kernel.Compute == null)
            {
                return;
            }

            int capacity = state.RadianceCacheBuffer.count / DXRCameraResourceCache.RadianceCacheEntryStride;

            cmd.SetComputeBufferParam(kernel.Compute, kernel.Kernel, RadianceCacheProperty, state.RadianceCacheBuffer);
            cmd.SetComputeIntParam(kernel.Compute, RadianceCacheCapacityProperty, capacity);
            cmd.SetComputeFloatParam(kernel.Compute, RadianceCacheDecayProperty, Mathf.Clamp(settings.TemporalDecay, 0f, .999f));
            cmd.DispatchCompute(kernel.Compute, kernel.Kernel, Mathf.CeilToInt(capacity / 64f), 1, 1);
        }

        public void SetFrameRayTracingParameters(
            CommandBuffer cmd,
            RayTracingShader shader,
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
            Vector3 cameraWorldPos,
            float nearClipNdc,
            float farClipNdc,
            bool isOrthographic,
            CameraLensState lensState,
            int frameIndex,
            bool hasValidHistory)
        {
            cmd.SetRayTracingIntParam(shader, AccumulationSampleCountProperty, Mathf.Max(0, state.SampleCount));
            cmd.SetRayTracingIntParam(shader, WidthProperty, state.Width);
            cmd.SetRayTracingIntParam(shader, HeightProperty, state.Height);
            cmd.SetRayTracingIntParam(shader, LightCountProperty, lightCount);
            cmd.SetRayTracingIntParam(shader, AnalyticAreaLightCountProperty, analyticAreaLightCount);
            cmd.SetRayTracingIntParam(shader, FrameIndexProperty, frameIndex);
            cmd.SetRayTracingIntParam(shader, FrameParityProperty, frameIndex & 1);
            cmd.SetRayTracingIntParam(shader, EnableCheckerboardProperty, _pipelineResources.Asset.CheckerboardSettings.IsEnabled ? 1 : 0);
            cmd.SetRayTracingIntParam(shader, IsOrthographicProperty, isOrthographic ? 1 : 0);
            cmd.SetRayTracingIntParam(shader, HasValidHistoryProperty, hasValidHistory ? 1 : 0);
            cmd.SetRayTracingFloatParam(shader, NearClipNdcProperty, nearClipNdc);
            cmd.SetRayTracingFloatParam(shader, FarClipNdcProperty, farClipNdc);
            lensState.BindRayTracing(cmd, shader);
            cmd.SetRayTracingVectorParam(shader, CameraWorldPosProperty, cameraWorldPos);
            cmd.SetRayTracingVectorParam(shader, PrevCameraWorldPosProperty, prevCameraWorldPos);
            cmd.SetRayTracingMatrixParam(shader, InvViewProjMatrixProperty, invViewProj);
            cmd.SetRayTracingMatrixParam(shader, ViewProjMatrixProperty, viewProj);
            cmd.SetRayTracingMatrixParam(shader, NonJitteredViewProjMatrixProperty, nonJitteredViewProj);
            cmd.SetRayTracingMatrixParam(shader, ViewMatrixProperty, viewMatrix);
            cmd.SetRayTracingMatrixParam(shader, PrevViewProjMatrixProperty, prevViewProj);
            cmd.SetRayTracingMatrixParam(shader, PrevViewMatrixProperty, prevViewMatrix);

            cmd.SetRayTracingIntParam(shader, SamplesPerPixelProperty, Mathf.Max(1, _pipelineResources.Asset.PathTracingSettings.SamplesPerPixel));
            cmd.SetRayTracingIntParam(shader, SurfaceBouncesProperty, Mathf.Max(1, _pipelineResources.Asset.PathTracingSettings.SurfaceBounces));

            cmd.SetRayTracingIntParam(
                shader,
                ReflectionBouncesProperty,
                Mathf.Max(1, _pipelineResources.Asset.PathTracingSettings.ReflectionBounces));

            cmd.SetRayTracingIntParam(
                shader,
                TransmissionBouncesProperty,
                Mathf.Max(1, _pipelineResources.Asset.PathTracingSettings.TransmissionBounces));

            cmd.SetRayTracingFloatParam(
                shader,
                BounceMaxDistanceProperty,
                Mathf.Max(_pipelineResources.Asset.PathTracingSettings.MaxBounceDistance, .001f));

            cmd.SetRayTracingIntParam(
                shader,
                RussianRouletteStartProperty,
                Mathf.Max(1, _pipelineResources.Asset.PathTracingSettings.RussianRouletteStart));

            cmd.SetRayTracingFloatParam(shader, DirectIntensityProperty, _pipelineResources.Asset.PathTracingSettings.DirectIntensity);
            cmd.SetRayTracingFloatParam(shader, SurfaceBiasProperty, _pipelineResources.Asset.PathTracingSettings.SurfaceBias);

            cmd.SetRayTracingFloatParam(
                shader,
                IndirectClampMultiplierSurfaceProperty,
                Mathf.Max(0f, _pipelineResources.Asset.FireflySuppressionSettings.IndirectClampMultiplierSurface));

            cmd.SetRayTracingFloatParam(
                shader,
                IndirectClampMultiplierReflectionProperty,
                Mathf.Max(0f, _pipelineResources.Asset.FireflySuppressionSettings.IndirectClampMultiplierReflection));

            cmd.SetRayTracingFloatParam(
                shader,
                IndirectClampMultiplierTransmissionProperty,
                Mathf.Max(0f, _pipelineResources.Asset.FireflySuppressionSettings.IndirectClampMultiplierTransmission));
        }

        public void BindSceneResources(CommandBuffer cmd, ComputeKernel kernel)
        {
            var compute = kernel.Compute;

            cmd.SetComputeBufferParam(compute, kernel.Kernel, LightsProperty, _pipelineResources.LightBuffer);
            cmd.SetComputeBufferParam(compute, kernel.Kernel, SobolMatricesBufferProperty, _pipelineResources.SobolMatricesBuffer);

            if (_envSampler != null)
            {
                _envSampler.BindKernel(cmd, compute, kernel.Kernel);
            }

            if (_msLut != null)
            {
                _msLut.BindKernel(cmd, compute, kernel.Kernel);
            }
        }

        public void BindPathTracingResources(CommandBuffer cmd, CameraState state, ComputeKernel kernel)
        {
            var compute = kernel.Compute;

            BindSceneResources(cmd, kernel);

            if (_pipelineResources.RTAS != null)
            {
                cmd.SetRayTracingAccelerationStructure(compute, kernel.Kernel, RayTracingAccelerationStructureProperty, _pipelineResources.RTAS);
            }

            cmd.SetComputeBufferParam(compute, kernel.Kernel, PathStatesProperty, state.PathStateBuffer);
            cmd.SetComputeBufferParam(compute, kernel.Kernel, PathFlagsProperty, state.PathFlagsBuffer);
            cmd.SetComputeBufferParam(compute, kernel.Kernel, PathHitsProperty, state.PathHitBuffer);
            cmd.SetComputeBufferParam(compute, kernel.Kernel, PathDistancesProperty, state.PathDistanceBuffer);
            cmd.SetComputeBufferParam(compute, kernel.Kernel, RadianceCacheProperty, state.RadianceCacheBuffer);

            cmd.SetComputeIntParam(
                compute,
                RadianceCacheCapacityProperty,
                state.RadianceCacheBuffer.count / DXRCameraResourceCache.RadianceCacheEntryStride);

            var radianceCacheSettings = _pipelineResources.Asset.RadianceCacheSettings;

            cmd.SetComputeIntParam(compute, RadianceCacheEnabledProperty, radianceCacheSettings != null && radianceCacheSettings.IsEnabled ? 1 : 0);

            cmd.SetComputeIntParam(
                compute,
                RadianceCacheMinSamplesProperty,
                radianceCacheSettings != null ? Mathf.Max(1, radianceCacheSettings.MinSamples) : 16);

            cmd.SetComputeIntParam(
                compute,
                RadianceCacheMinDepthProperty,
                radianceCacheSettings != null ? Mathf.Max(0, radianceCacheSettings.MinDepth) : 1);

            cmd.SetComputeFloatParam(
                compute,
                RadianceCacheCorrectionProbabilityProperty,
                radianceCacheSettings != null ? Mathf.Clamp(radianceCacheSettings.CorrectionProbability, .05f, 1f) : .5f);

            cmd.SetComputeFloatParam(
                compute,
                RadianceCacheBaseCellSizeProperty,
                radianceCacheSettings != null ? Mathf.Max(1e-4f, radianceCacheSettings.BaseCellSize) : .05f);

            cmd.SetComputeFloatParam(
                compute,
                RadianceCacheLodScaleProperty,
                radianceCacheSettings != null ? Mathf.Max(1e-4f, radianceCacheSettings.LodScale) : 1f);

            cmd.SetComputeIntParam(compute, GuidingDebugProperty, radianceCacheSettings != null && radianceCacheSettings.IsDebugView ? 1 : 0);

            cmd.SetComputeTextureParam(compute, kernel.Kernel, FrameSurfaceSignalSumProperty, state.FrameSurfaceSignalSum.nameID);
            cmd.SetComputeTextureParam(compute, kernel.Kernel, FrameReflectionSignalSumProperty, state.FrameReflectionSignalSum.nameID);
            cmd.SetComputeTextureParam(compute, kernel.Kernel, FrameTransmissionSignalSumProperty, state.FrameTransmissionSignalSum.nameID);
            cmd.SetComputeTextureParam(compute, kernel.Kernel, FrameShadowSumUavProperty, state.FrameShadowSum.nameID);
            cmd.SetComputeTextureParam(compute, kernel.Kernel, FrameEmissionSumProperty, state.FrameEmissionSum.nameID);

            var fireflySettings = _pipelineResources.Asset.FireflySuppressionSettings;

            cmd.SetComputeFloatParam(
                compute,
                IndirectClampHistoryScaleProperty,
                1f / Mathf.Max(1, _pipelineResources.Asset.TemporalHistorySettings.MaxHistoryLength));

            cmd.SetComputeTextureParam(
                compute,
                kernel.Kernel,
                IndirectClampHistoryLutSurfaceProperty,
                IndirectClampLutOrWhite(fireflySettings != null ? fireflySettings.IndirectClampHistoryLutSurface : null));

            cmd.SetComputeTextureParam(
                compute,
                kernel.Kernel,
                IndirectClampHistoryLutReflectionProperty,
                IndirectClampLutOrWhite(fireflySettings != null ? fireflySettings.IndirectClampHistoryLutReflection : null));

            cmd.SetComputeTextureParam(
                compute,
                kernel.Kernel,
                IndirectClampHistoryLutTransmissionProperty,
                IndirectClampLutOrWhite(fireflySettings != null ? fireflySettings.IndirectClampHistoryLutTransmission : null));

            cmd.SetComputeTextureParam(compute, kernel.Kernel, IndirectClampHistoryMomentsSurfaceProperty, state.SurfaceSignal.PrevMoments.nameID);

            cmd.SetComputeTextureParam(
                compute,
                kernel.Kernel,
                IndirectClampHistoryMomentsReflectionProperty,
                state.ReflectionSignal.PrevMoments.nameID);

            cmd.SetComputeTextureParam(
                compute,
                kernel.Kernel,
                IndirectClampHistoryMomentsTransmissionProperty,
                state.TransmissionSignal.PrevMoments.nameID);

            cmd.SetComputeTextureParam(compute, kernel.Kernel, PrimaryDiffuseAlbedoProperty, state.PrimaryDiffuseAlbedo.nameID);
            cmd.SetComputeTextureParam(compute, kernel.Kernel, PrimarySpecularF0Property, state.PrimarySpecularF0.nameID);
            cmd.SetComputeTextureParam(compute, kernel.Kernel, PrimaryTransmissionTintProperty, state.PrimaryTransmissionTint.nameID);
            cmd.SetComputeTextureParam(compute, kernel.Kernel, ShadowGuide0Property, state.ShadowSignal.Guide0.nameID);
            cmd.SetComputeTextureParam(compute, kernel.Kernel, ShadowGuide1Property, state.ShadowSignal.Guide1.nameID);
            cmd.SetComputeTextureParam(compute, kernel.Kernel, ShadowGuide2Property, state.ShadowSignal.Guide2.nameID);

            cmd.SetComputeTextureParam(compute, kernel.Kernel, PrimaryHitDistanceProperty, state.PrimaryHitDistance.nameID);
            cmd.SetComputeTextureParam(compute, kernel.Kernel, ReflectionHitDistanceProperty, state.ReflectionHitDistance.nameID);
            cmd.SetComputeTextureParam(compute, kernel.Kernel, TransmissionHitDistanceProperty, state.TransmissionHitDistance.nameID);
            cmd.SetComputeTextureParam(compute, kernel.Kernel, PrimaryRoughnessProperty, state.PrimaryRoughness.nameID);

            cmd.SetComputeTextureParam(compute, kernel.Kernel, ReflectionGuideEvent0Property, state.ReflectionGuideEvent.Event0.nameID);
            cmd.SetComputeTextureParam(compute, kernel.Kernel, ReflectionGuideEvent1Property, state.ReflectionGuideEvent.Event1.nameID);
            cmd.SetComputeTextureParam(compute, kernel.Kernel, ReflectionGuideEvent2Property, state.ReflectionGuideEvent.Event2.nameID);
            cmd.SetComputeTextureParam(compute, kernel.Kernel, ReflectionGuideEvent3Property, state.ReflectionGuideEvent.Event3.nameID);
            cmd.SetComputeTextureParam(compute, kernel.Kernel, TransmissionGuideEvent0Property, state.TransmissionGuideEvent.Event0.nameID);
            cmd.SetComputeTextureParam(compute, kernel.Kernel, TransmissionGuideEvent1Property, state.TransmissionGuideEvent.Event1.nameID);
            cmd.SetComputeTextureParam(compute, kernel.Kernel, TransmissionGuideEvent2Property, state.TransmissionGuideEvent.Event2.nameID);
            cmd.SetComputeTextureParam(compute, kernel.Kernel, TransmissionGuideEvent3Property, state.TransmissionGuideEvent.Event3.nameID);
        }

        public void BindResolveSecondaryGuideResources(CommandBuffer cmd, CameraState state)
        {
            var compute = _pipelineResources.ResolveSecondaryGuides.Compute;
            var kernel = _pipelineResources.ResolveSecondaryGuides.Kernel;

            cmd.SetComputeTextureParam(compute, kernel, ReflectionGuideEvent0Property, state.ReflectionGuideEvent.Event0.nameID);
            cmd.SetComputeTextureParam(compute, kernel, ReflectionGuideEvent1Property, state.ReflectionGuideEvent.Event1.nameID);
            cmd.SetComputeTextureParam(compute, kernel, ReflectionGuideEvent2Property, state.ReflectionGuideEvent.Event2.nameID);
            cmd.SetComputeTextureParam(compute, kernel, ReflectionGuideEvent3Property, state.ReflectionGuideEvent.Event3.nameID);
            cmd.SetComputeTextureParam(compute, kernel, TransmissionGuideEvent0Property, state.TransmissionGuideEvent.Event0.nameID);
            cmd.SetComputeTextureParam(compute, kernel, TransmissionGuideEvent1Property, state.TransmissionGuideEvent.Event1.nameID);
            cmd.SetComputeTextureParam(compute, kernel, TransmissionGuideEvent2Property, state.TransmissionGuideEvent.Event2.nameID);
            cmd.SetComputeTextureParam(compute, kernel, TransmissionGuideEvent3Property, state.TransmissionGuideEvent.Event3.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PrevTransmissionGuideEvent0Property, state.TransmissionGuideEvent.PrevEvent0.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PrevTransmissionGuideEvent1Property, state.TransmissionGuideEvent.PrevEvent1.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PrevTransmissionGuideEvent2Property, state.TransmissionGuideEvent.PrevEvent2.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PrevTransmissionGuideEvent3Property, state.TransmissionGuideEvent.PrevEvent3.nameID);

            cmd.SetComputeTextureParam(compute, kernel, FrameReflectionSignalSumProperty, state.FrameReflectionSignalSum.nameID);
            cmd.SetComputeTextureParam(compute, kernel, FrameTransmissionSignalSumProperty, state.FrameTransmissionSignalSum.nameID);
            cmd.SetComputeTextureParam(compute, kernel, Guide0Property, state.SurfaceSignal.Guide0.nameID);
            cmd.SetComputeTextureParam(compute, kernel, Guide1Property, state.SurfaceSignal.Guide1.nameID);
            cmd.SetComputeTextureParam(compute, kernel, Guide2Property, state.SurfaceSignal.Guide2.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PrimaryHitDistanceProperty, state.PrimaryHitDistance.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PrimaryRoughnessProperty, state.PrimaryRoughness.nameID);
            cmd.SetComputeTextureParam(compute, kernel, ReflectionGuide0Property, state.ReflectionSignal.Guide0.nameID);
            cmd.SetComputeTextureParam(compute, kernel, ReflectionGuide1Property, state.ReflectionSignal.Guide1.nameID);
            cmd.SetComputeTextureParam(compute, kernel, ReflectionGuide2Property, state.ReflectionSignal.Guide2.nameID);
            cmd.SetComputeTextureParam(compute, kernel, TransmissionGuide0Property, state.TransmissionSignal.Guide0.nameID);
            cmd.SetComputeTextureParam(compute, kernel, TransmissionGuide1Property, state.TransmissionSignal.Guide1.nameID);
            cmd.SetComputeTextureParam(compute, kernel, TransmissionGuide2Property, state.TransmissionSignal.Guide2.nameID);
        }

        public void BindTemporalAccumulationResources(CommandBuffer cmd, CameraState state)
        {
            var compute = _pipelineResources.AccumulateFrame.Compute;

            BindAccumulationTextures(cmd, state, compute, _pipelineResources.AccumulateFrame.Kernel);
            BindAccumulationTextures(cmd, state, compute, _pipelineResources.AccumulateFrameSpecular.Kernel);
            BindAccumulationTextures(cmd, state, compute, _pipelineResources.ReflectionPrePass.Kernel);

            if (state.BootstrapActiveIndicesBuffer != null && state.DispatchArgsBuffer != null)
            {
                cmd.SetComputeBufferParam(
                    compute,
                    _pipelineResources.AccumulateFrame.Kernel,
                    BootstrapActiveIndicesProperty,
                    state.BootstrapActiveIndicesBuffer);

                cmd.SetComputeBufferParam(
                    compute,
                    _pipelineResources.AccumulateFrame.Kernel,
                    BootstrapDispatchArgsProperty,
                    state.DispatchArgsBuffer);
            }

            if (_pipelineResources.AccumulateSecondary.Compute != null && state.ActivePathIndicesBuffer != null && state.DispatchArgsBuffer != null)
            {
                var secondaryKernel = _pipelineResources.AccumulateSecondary.Kernel;

                BindAccumulationTextures(cmd, state, compute, secondaryKernel);
                cmd.SetComputeBufferParam(compute, secondaryKernel, ActiveSecondaryIndicesProperty, state.ActivePathIndicesBuffer);
                cmd.SetComputeBufferParam(compute, secondaryKernel, SecondaryActiveCountProperty, state.DispatchArgsBuffer);
            }

            if (_pipelineResources.BootstrapSurface.Compute != null && state.BootstrapActiveIndicesBuffer != null && state.DispatchArgsBuffer != null)
            {
                var bootstrapKernel = _pipelineResources.BootstrapSurface.Kernel;

                BindAccumulationTextures(cmd, state, compute, bootstrapKernel);
                cmd.SetComputeBufferParam(compute, bootstrapKernel, BootstrapActiveIndicesProperty, state.BootstrapActiveIndicesBuffer);
                cmd.SetComputeBufferParam(compute, bootstrapKernel, BootstrapDispatchInfoProperty, state.DispatchArgsBuffer);
            }
        }

        public void BindLightingAliasResources(
            CommandBuffer cmd,
            CameraState state,
            RTHandle surface,
            RTHandle reflection,
            RTHandle transmission,
            RTHandle compositeOutput)
        {
            var compute = _pipelineResources.BuildLightingAliases.Compute;
            var kernel = _pipelineResources.BuildLightingAliases.Kernel;

            cmd.SetComputeTextureParam(compute, kernel, AliasSurfaceSignalProperty, surface.nameID);
            cmd.SetComputeTextureParam(compute, kernel, AliasReflectionSignalProperty, reflection.nameID);
            cmd.SetComputeTextureParam(compute, kernel, AliasTransmissionSignalProperty, transmission.nameID);
            cmd.SetComputeTextureParam(compute, kernel, DirectLightingUavProperty, state.DirectLighting.nameID);
            cmd.SetComputeTextureParam(compute, kernel, IndirectLightingUavProperty, state.IndirectLighting.nameID);
            cmd.SetComputeTextureParam(compute, kernel, HitMaskUavProperty, state.HitMask.nameID);
            cmd.SetComputeTextureParam(compute, kernel, CompositeOutputProperty, compositeOutput.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PrimaryDiffuseAlbedoProperty, state.PrimaryDiffuseAlbedo.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PrimarySpecularF0Property, state.PrimarySpecularF0.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PrimaryTransmissionTintProperty, state.PrimaryTransmissionTint.nameID);
            cmd.SetComputeTextureParam(compute, kernel, Guide1Property, state.SurfaceSignal.Guide1.nameID);
            cmd.SetComputeTextureParam(compute, kernel, FrameEmissionSumProperty, state.FrameEmissionSum.nameID);
            cmd.SetComputeTextureParam(compute, kernel, FrameShadowSumUavProperty, state.FrameShadowSum.nameID);
            cmd.SetComputeTextureParam(compute, kernel, SurfaceMomentsProperty, state.SurfaceSignal.Moments.nameID);
            cmd.SetComputeTextureParam(compute, kernel, ReflectionMomentsProperty, state.ReflectionSignal.Moments.nameID);
            cmd.SetComputeTextureParam(compute, kernel, TransmissionMomentsProperty, state.TransmissionSignal.Moments.nameID);
            cmd.SetComputeTextureParam(compute, kernel, FrameSurfaceSignalSumProperty, state.FrameSurfaceSignalSum.nameID);
        }

        public void DispatchCheckerboardResolve(CommandBuffer cmd, CameraState state, int groupsX, int groupsY)
        {
            if (_pipelineResources.CheckerboardResolve.Compute == null)
            {
                return;
            }

            var checkerboardSettings = _pipelineResources.Asset.CheckerboardSettings;

            if (checkerboardSettings.IsEnabled == false)
            {
                return;
            }

            var compute = _pipelineResources.CheckerboardResolve.Compute;
            var kernel = _pipelineResources.CheckerboardResolve.Kernel;

            cmd.SetComputeIntParam(compute, CheckerboardReconstructionRadiusProperty, Mathf.Clamp(checkerboardSettings.ReconstructionRadius, 1, 3));

            cmd.SetComputeFloatParam(
                compute,
                CheckerboardNeighborNormalThresholdProperty,
                Mathf.Clamp01(checkerboardSettings.NeighborNormalThreshold));

            cmd.SetComputeFloatParam(
                compute,
                CheckerboardNeighborDepthThresholdProperty,
                Mathf.Max(1e-4f, checkerboardSettings.NeighborDepthThreshold));

            cmd.SetComputeTextureParam(compute, kernel, FrameSurfaceSignalSumProperty, state.FrameSurfaceSignalSum.nameID);
            cmd.SetComputeTextureParam(compute, kernel, FrameReflectionSignalSumProperty, state.FrameReflectionSignalSum.nameID);
            cmd.SetComputeTextureParam(compute, kernel, FrameTransmissionSignalSumProperty, state.FrameTransmissionSignalSum.nameID);
            cmd.SetComputeTextureParam(compute, kernel, FrameShadowSumUavProperty, state.FrameShadowSum.nameID);
            cmd.SetComputeTextureParam(compute, kernel, Guide0Property, state.SurfaceSignal.Guide0.nameID);
            cmd.SetComputeTextureParam(compute, kernel, Guide1Property, state.SurfaceSignal.Guide1.nameID);
            cmd.SetComputeTextureParam(compute, kernel, Guide2Property, state.SurfaceSignal.Guide2.nameID);

            DispatchCompute(cmd, _pipelineResources.CheckerboardResolve, Mathf.CeilToInt(state.Width / 16f), groupsY);
        }

        public void DispatchAccumulateFrame(CommandBuffer cmd, CameraState state, int groupsX, int groupsY)
        {
            var compute = _pipelineResources.AccumulateFrame.Compute;

            var compactionActive = _pipelineResources.AccumulateSecondary.Compute != null
                && _pipelineResources.CompactSecondary.Compute != null
                && _pipelineResources.ClearSecondaryArgs.Compute != null
                && _pipelineResources.FinalizeSecondaryArgs.Compute != null
                && _pipelineResources.BootstrapSurface.Compute != null
                && _pipelineResources.ClearBootstrapArgs.Compute != null
                && _pipelineResources.FinalizeBootstrapArgs.Compute != null
                && state.ActivePathIndicesBuffer != null
                && state.BootstrapActiveIndicesBuffer != null
                && state.DispatchArgsBuffer != null;

            cmd.SetComputeIntParam(compute, SurfaceFastHistoryLengthProperty, 3);
            var fastHistoryKernel = _pipelineResources.BuildSurfaceFastHistory.Kernel;
            cmd.SetComputeTextureParam(compute, fastHistoryKernel, FrameSurfaceSignalSumProperty, state.FrameSurfaceSignalSum.nameID);
            cmd.SetComputeTextureParam(compute, fastHistoryKernel, FrameShadowSumUavProperty, state.FrameShadowSum.nameID);
            cmd.SetComputeTextureParam(compute, fastHistoryKernel, Guide0Property, state.SurfaceSignal.Guide0.nameID);
            cmd.SetComputeTextureParam(compute, fastHistoryKernel, Guide1Property, state.SurfaceSignal.Guide1.nameID);
            cmd.SetComputeTextureParam(compute, fastHistoryKernel, Guide2Property, state.SurfaceSignal.Guide2.nameID);
            cmd.SetComputeTextureParam(compute, fastHistoryKernel, PrevGuide0Property, state.SurfaceSignal.PrevGuide0.nameID);
            cmd.SetComputeTextureParam(compute, fastHistoryKernel, PrevGuide1Property, state.SurfaceSignal.PrevGuide1.nameID);
            cmd.SetComputeTextureParam(compute, fastHistoryKernel, PrevGuide2Property, state.SurfaceSignal.PrevGuide2.nameID);
            cmd.SetComputeTextureParam(compute, fastHistoryKernel, SurfaceFastHistoryProperty, state.SurfaceFastHistory.nameID);
            cmd.SetComputeTextureParam(compute, fastHistoryKernel, PrevSurfaceFastHistoryProperty, state.PrevSurfaceFastHistory.nameID);
            DispatchCompute(cmd, _pipelineResources.BuildSurfaceFastHistory, groupsX, groupsY);
            cmd.SetComputeTextureParam(compute, _pipelineResources.AccumulateFrame.Kernel, SurfaceFastHistoryProperty, state.SurfaceFastHistory.nameID);

            cmd.SetComputeIntParam(compute, UseSecondaryCompactionProperty, compactionActive ? 1 : 0);
            DispatchCompute(cmd, _pipelineResources.ReflectionPrePass, groupsX, groupsY);

            if (compactionActive == false)
            {
                DispatchCompute(cmd, _pipelineResources.AccumulateFrameSpecular, groupsX, groupsY);
                DispatchCompute(cmd, _pipelineResources.AccumulateFrame, groupsX, groupsY);

                return;
            }

            cmd.SetComputeTextureParam(
                compute,
                _pipelineResources.CompactSecondary.Kernel,
                ReflectionGuide1Property,
                state.ReflectionSignal.Guide1.nameID);

            cmd.SetComputeTextureParam(
                compute,
                _pipelineResources.CompactSecondary.Kernel,
                TransmissionGuide1Property,
                state.TransmissionSignal.Guide1.nameID);

            cmd.SetComputeBufferParam(
                compute,
                _pipelineResources.CompactSecondary.Kernel,
                ActiveSecondaryIndicesProperty,
                state.ActivePathIndicesBuffer);

            cmd.SetComputeBufferParam(compute, _pipelineResources.CompactSecondary.Kernel, SecondaryDispatchArgsProperty, state.DispatchArgsBuffer);
            cmd.SetComputeBufferParam(compute, _pipelineResources.ClearSecondaryArgs.Kernel, SecondaryDispatchArgsProperty, state.DispatchArgsBuffer);

            cmd.SetComputeBufferParam(
                compute,
                _pipelineResources.FinalizeSecondaryArgs.Kernel,
                SecondaryDispatchArgsProperty,
                state.DispatchArgsBuffer);

            cmd.SetComputeBufferParam(compute, _pipelineResources.ClearBootstrapArgs.Kernel, BootstrapDispatchArgsProperty, state.DispatchArgsBuffer);

            cmd.SetComputeBufferParam(
                compute,
                _pipelineResources.FinalizeBootstrapArgs.Kernel,
                BootstrapDispatchArgsProperty,
                state.DispatchArgsBuffer);

            DispatchCompute(cmd, _pipelineResources.ClearSecondaryArgs, 1, 1);
            DispatchCompute(cmd, _pipelineResources.ClearBootstrapArgs, 1, 1);
            DispatchCompute(cmd, _pipelineResources.CompactSecondary, groupsX, groupsY);
            DispatchCompute(cmd, _pipelineResources.FinalizeSecondaryArgs, 1, 1);
            DispatchCompute(cmd, _pipelineResources.AccumulateFrameSpecular, groupsX, groupsY);
            cmd.DispatchCompute(compute, _pipelineResources.AccumulateSecondary.Kernel, state.DispatchArgsBuffer, 0);
            DispatchCompute(cmd, _pipelineResources.AccumulateFrame, groupsX, groupsY);
            DispatchCompute(cmd, _pipelineResources.FinalizeBootstrapArgs, 1, 1);
            cmd.DispatchCompute(compute, _pipelineResources.BootstrapSurface.Kernel, state.DispatchArgsBuffer, 16);
        }

        public bool TryDispatchAntiFirefly(CommandBuffer cmd, CameraState state, RTHandle input, RTHandle output)
        {
            if (_pipelineResources.AntiFirefly.Compute == null || output == null)
            {
                return false;
            }

            var compute = _pipelineResources.AntiFirefly.Compute;
            var kernel = _pipelineResources.AntiFirefly.Kernel;

            cmd.SetComputeIntParam(compute, WidthProperty, state.Width);
            cmd.SetComputeIntParam(compute, HeightProperty, state.Height);
            cmd.SetComputeTextureParam(compute, kernel, AntiFireflyInputProperty, input.nameID);
            cmd.SetComputeTextureParam(compute, kernel, AntiFireflyOutputProperty, output.nameID);

            cmd.DispatchCompute(compute, kernel, Mathf.CeilToInt(state.Width / 8f), Mathf.CeilToInt(state.Height / 8f), 1);

            return true;
        }

        public bool DispatchTaa(CommandBuffer cmd, CameraState state, RTHandle current, RTHandle output, bool hasValidHistory, float feedback)
        {
            if (_pipelineResources.TaaResolve.Compute == null)
            {
                return false;
            }

            var compute = _pipelineResources.TaaResolve.Compute;
            var kernel = _pipelineResources.TaaResolve.Kernel;

            cmd.SetComputeIntParam(compute, WidthProperty, state.Width);
            cmd.SetComputeIntParam(compute, HeightProperty, state.Height);
            cmd.SetComputeIntParam(compute, HasValidHistoryProperty, hasValidHistory ? 1 : 0);
            var pathSettings = _pipelineResources.Asset.PathTracingSettings;
            cmd.SetComputeIntParam(compute, SamplesPerPixelProperty, Mathf.Max(1, pathSettings.SamplesPerPixel));
            cmd.SetComputeIntParam(compute, DebugVisualizationModeProperty, (int)pathSettings.DebugVisualizationMode);
            cmd.SetComputeFloatParam(compute, TaaFeedbackProperty, feedback);
            cmd.SetComputeTextureParam(compute, kernel, TaaCurrentColorProperty, current.nameID);
            cmd.SetComputeTextureParam(compute, kernel, TaaPrevColorProperty, state.PrevResult.nameID);
            cmd.SetComputeTextureParam(compute, kernel, TaaMotionGuideProperty, state.SurfaceSignal.Guide1.nameID);
            cmd.SetComputeTextureParam(compute, kernel, TaaPrevSurfaceGuideProperty, state.SurfaceSignal.PrevGuide1.nameID);
            cmd.SetComputeTextureParam(compute, kernel, ReflectionGuide1Property, state.ReflectionSignal.Guide1.nameID);
            cmd.SetComputeTextureParam(compute, kernel, ReflectionGuide2Property, state.ReflectionSignal.Guide2.nameID);
            cmd.SetComputeTextureParam(compute, kernel, FrameEmissionSumProperty, state.FrameEmissionSum.nameID);
            cmd.SetComputeTextureParam(compute, kernel, TaaOutputProperty, output.nameID);
            cmd.SetComputeTextureParam(compute, kernel, TaaHistoryOutputProperty, state.FilterPong.nameID);

            var groupsX = Mathf.CeilToInt(state.Width / 8f);
            var groupsY = Mathf.CeilToInt(state.Height / 8f);

            cmd.DispatchCompute(compute, kernel, groupsX, groupsY, 1);

            return true;
        }

        private static ComputeKernel FindKernel(ComputeShader compute, string label, string kernelName)
        {
            if (compute == null)
            {
                throw new InvalidOperationException($"{label} compute shader is not assigned.");
            }

            var kernel = compute.FindKernel(kernelName);

            if (kernel < 0)
            {
                throw new InvalidOperationException($"{kernelName} kernel not found in {label} compute shader.");
            }

            return new ComputeKernel(compute, kernel);
        }

        private static RayTracingShader FindRayTracingShader(RayTracingShader shader, string label)
        {
            if (shader == null)
            {
                throw new InvalidOperationException($"{label} ray tracing shader is not assigned.");
            }

            return shader;
        }

        private static Texture IndirectClampLutOrWhite(Texture2D lut)
        {
            return lut != null ? (Texture)lut : Texture2D.whiteTexture;
        }

        private void BindAccumulationTextures(CommandBuffer cmd, CameraState state, ComputeShader compute, int kernel)
        {
            cmd.SetComputeTextureParam(compute, kernel, FrameSurfaceSignalSumProperty, state.FrameSurfaceSignalSum.nameID);
            cmd.SetComputeTextureParam(compute, kernel, FrameReflectionSignalSumProperty, state.FrameReflectionSignalSum.nameID);
            cmd.SetComputeTextureParam(compute, kernel, ReflectionPrePassSignalProperty, state.ReflectionPrePassSignal.nameID);
            cmd.SetComputeTextureParam(compute, kernel, FrameTransmissionSignalSumProperty, state.FrameTransmissionSignalSum.nameID);
            cmd.SetComputeTextureParam(compute, kernel, FrameShadowSumUavProperty, state.FrameShadowSum.nameID);
            cmd.SetComputeTextureParam(compute, kernel, FrameEmissionSumProperty, state.FrameEmissionSum.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PrimaryDiffuseAlbedoProperty, state.PrimaryDiffuseAlbedo.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PrimarySpecularF0Property, state.PrimarySpecularF0.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PrimaryTransmissionTintProperty, state.PrimaryTransmissionTint.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PrevMomentsProperty, state.PrevMoments.nameID);
            cmd.SetComputeTextureParam(compute, kernel, MomentsProperty, state.Moments.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PrevSurfaceMomentsProperty, state.SurfaceSignal.PrevMoments.nameID);
            cmd.SetComputeTextureParam(compute, kernel, SurfaceMomentsProperty, state.SurfaceSignal.Moments.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PrevReflectionMomentsProperty, state.ReflectionSignal.PrevMoments.nameID);
            cmd.SetComputeTextureParam(compute, kernel, ReflectionMomentsProperty, state.ReflectionSignal.Moments.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PrevTransmissionMomentsProperty, state.TransmissionSignal.PrevMoments.nameID);
            cmd.SetComputeTextureParam(compute, kernel, TransmissionMomentsProperty, state.TransmissionSignal.Moments.nameID);
            cmd.SetComputeTextureParam(compute, kernel, Guide0Property, state.SurfaceSignal.Guide0.nameID);
            cmd.SetComputeTextureParam(compute, kernel, Guide1Property, state.SurfaceSignal.Guide1.nameID);
            cmd.SetComputeTextureParam(compute, kernel, Guide2Property, state.SurfaceSignal.Guide2.nameID);
            cmd.SetComputeTextureParam(compute, kernel, ReflectionGuide0Property, state.ReflectionSignal.Guide0.nameID);
            cmd.SetComputeTextureParam(compute, kernel, ReflectionGuide1Property, state.ReflectionSignal.Guide1.nameID);
            cmd.SetComputeTextureParam(compute, kernel, ReflectionGuide2Property, state.ReflectionSignal.Guide2.nameID);
            cmd.SetComputeTextureParam(compute, kernel, TransmissionGuide0Property, state.TransmissionSignal.Guide0.nameID);
            cmd.SetComputeTextureParam(compute, kernel, TransmissionGuide1Property, state.TransmissionSignal.Guide1.nameID);
            cmd.SetComputeTextureParam(compute, kernel, TransmissionGuide2Property, state.TransmissionSignal.Guide2.nameID);
            cmd.SetComputeTextureParam(compute, kernel, ShadowGuide0Property, state.ShadowSignal.Guide0.nameID);
            cmd.SetComputeTextureParam(compute, kernel, ShadowGuide1Property, state.ShadowSignal.Guide1.nameID);
            cmd.SetComputeTextureParam(compute, kernel, ShadowGuide2Property, state.ShadowSignal.Guide2.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PrevGuide0Property, state.SurfaceSignal.PrevGuide0.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PrevGuide1Property, state.SurfaceSignal.PrevGuide1.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PrevGuide2Property, state.SurfaceSignal.PrevGuide2.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PrevReflectionGuide0Property, state.ReflectionSignal.PrevGuide0.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PrevReflectionGuide1Property, state.ReflectionSignal.PrevGuide1.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PrevReflectionGuide2Property, state.ReflectionSignal.PrevGuide2.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PrevTransmissionGuide0Property, state.TransmissionSignal.PrevGuide0.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PrevTransmissionGuide1Property, state.TransmissionSignal.PrevGuide1.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PrevTransmissionGuide2Property, state.TransmissionSignal.PrevGuide2.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PrevShadowGuide0Property, state.ShadowSignal.PrevGuide0.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PrevShadowGuide1Property, state.ShadowSignal.PrevGuide1.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PrevShadowGuide2Property, state.ShadowSignal.PrevGuide2.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PrevSurfaceSignalProperty, state.SurfaceSignal.PrevSignal.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PrevReflectionSignalProperty, state.ReflectionSignal.PrevSignal.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PrevTransmissionSignalProperty, state.TransmissionSignal.PrevSignal.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PrevHitMaskProperty, state.PrevHitMask.nameID);
            cmd.SetComputeTextureParam(compute, kernel, CurrentHitMaskProperty, state.HitMask.nameID);
            cmd.SetComputeTextureParam(compute, kernel, ShadowHitMaskUavProperty, state.ShadowHitMask.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PrevShadowHitMaskProperty, state.PrevShadowHitMask.nameID);
            cmd.SetComputeTextureParam(compute, kernel, ReflectionShadowHitMaskUavProperty, state.ReflectionShadowHitMask.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PrevReflectionShadowHitMaskProperty, state.PrevReflectionShadowHitMask.nameID);
            cmd.SetComputeTextureParam(compute, kernel, TransmissionShadowHitMaskUavProperty, state.TransmissionShadowHitMask.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PrevTransmissionShadowHitMaskProperty, state.PrevTransmissionShadowHitMask.nameID);
            cmd.SetComputeTextureParam(compute, kernel, SurfaceSignalUavProperty, state.SurfaceSignal.Signal.nameID);
            cmd.SetComputeTextureParam(compute, kernel, ReflectionSignalUavProperty, state.ReflectionSignal.Signal.nameID);
            cmd.SetComputeTextureParam(compute, kernel, TransmissionSignalUavProperty, state.TransmissionSignal.Signal.nameID);
            cmd.SetComputeTextureParam(compute, kernel, SurfaceYoungHistorySignalProperty, state.SurfaceSignal.YoungHistorySignal.nameID);
            cmd.SetComputeTextureParam(compute, kernel, ReflectionYoungHistorySignalProperty, state.ReflectionSignal.YoungHistorySignal.nameID);
            cmd.SetComputeTextureParam(compute, kernel, TransmissionYoungHistorySignalProperty, state.TransmissionSignal.YoungHistorySignal.nameID);

            cmd.SetComputeTextureParam(compute, kernel, PrimaryHitDistanceProperty, state.PrimaryHitDistance.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PrevPrimaryHitDistanceProperty, state.PrevPrimaryHitDistance.nameID);
            cmd.SetComputeTextureParam(compute, kernel, ReflectionHitDistanceProperty, state.ReflectionHitDistance.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PrevReflectionHitDistanceProperty, state.PrevReflectionHitDistance.nameID);
            cmd.SetComputeTextureParam(compute, kernel, TransmissionHitDistanceProperty, state.TransmissionHitDistance.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PrevTransmissionHitDistanceProperty, state.PrevTransmissionHitDistance.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PrimaryRoughnessProperty, state.PrimaryRoughness.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PrevPrimaryRoughnessProperty, state.PrevPrimaryRoughness.nameID);
        }
    }
}
