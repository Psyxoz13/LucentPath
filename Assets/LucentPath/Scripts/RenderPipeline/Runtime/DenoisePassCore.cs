using System;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using static LucentPath.Rendering.PipelineShaderProperties;

namespace LucentPath.Rendering
{
    internal static class DenoisePassCore
    {
        private static readonly CustomSampler _surfaceDenoiseMarker = CustomSampler.Create("LucentPath.DXR.Denoise.Surface", true);
        private static readonly CustomSampler _reflectionDenoiseMarker = CustomSampler.Create("LucentPath.DXR.Denoise.Reflection", true);
        private static readonly CustomSampler _transmissionDenoiseMarker = CustomSampler.Create("LucentPath.DXR.Denoise.Transmission", true);
        private static readonly CustomSampler _compositeMarker = CustomSampler.Create("LucentPath.DXR.Denoise.Composite", true);
        private static readonly CustomSampler _historyFixMarker = CustomSampler.Create("LucentPath.DXR.Denoise.HistoryFix", true);
        private static readonly CustomSampler _atrousMarker = CustomSampler.Create("LucentPath.DXR.Denoise.Atrous", true);

        private static readonly string[] _signalKeywords =
        {
            "ATROUS_SIGNAL_SURFACE",
            "ATROUS_SIGNAL_REFLECTION",
            "ATROUS_SIGNAL_TRANSMISSION"
        };

        public static void Dispatch(
            CommandBuffer cmd,
            CameraState state,
            LucentPathRendererSettings asset,
            ComputeKernel historyFix,
            ComputeKernel atrousFilter,
            ComputeKernel buildLightingAliases,
            Action<CommandBuffer, CameraState, RTHandle, RTHandle, RTHandle, RTHandle> bindLightingAliasResources,
            int groupsX,
            int groupsY)
        {
            var surfaceIterations = Mathf.Max(1, asset.ATrousSettings.SurfaceAtrousIterations);
            var reflectionIterations = Mathf.Max(1, asset.ATrousSettings.ReflectionAtrousIterations);
            var transmissionIterations = Mathf.Max(1, asset.ATrousSettings.TransmissionAtrousIterations);

            cmd.BeginSample(_surfaceDenoiseMarker);

            var surfaceDenoised = DispatchSignal(
                cmd,
                state,
                asset,
                historyFix,
                atrousFilter,
                groupsX,
                groupsY,
                surfaceIterations,
                state.SurfaceSignal.Signal,
                state.SurfaceSignal.HistoryFixedSignal,
                state.SurfaceSignal.DenoisedSignal,
                state.SurfaceSignal.Guide0,
                state.SurfaceSignal.Guide1,
                state.SurfaceSignal.Guide2,
                state.SurfaceSignal.Moments,
                state.SurfaceSignal.YoungHistorySignal,
                state.PrimaryHitDistance,
                state.SurfaceSignal.Signal,
                0);

            cmd.EndSample(_surfaceDenoiseMarker);

            cmd.BeginSample(_reflectionDenoiseMarker);

            var reflectionDenoised = DispatchSignal(
                cmd,
                state,
                asset,
                historyFix,
                atrousFilter,
                groupsX,
                groupsY,
                reflectionIterations,
                state.ReflectionSignal.Signal,
                state.ReflectionSignal.HistoryFixedSignal,
                state.ReflectionSignal.DenoisedSignal,
                state.ReflectionSignal.Guide0,
                state.ReflectionSignal.Guide1,
                state.ReflectionSignal.Guide2,
                state.ReflectionSignal.Moments,
                state.ReflectionSignal.YoungHistorySignal,
                state.ReflectionHitDistance,
                state.ReflectionSignal.Signal,
                1);

            cmd.EndSample(_reflectionDenoiseMarker);

            cmd.BeginSample(_transmissionDenoiseMarker);

            var transmissionDenoised = DispatchSignal(
                cmd,
                state,
                asset,
                historyFix,
                atrousFilter,
                groupsX,
                groupsY,
                transmissionIterations,
                state.TransmissionSignal.Signal,
                state.TransmissionSignal.HistoryFixedSignal,
                state.TransmissionSignal.DenoisedSignal,
                state.TransmissionSignal.Guide0,
                state.TransmissionSignal.Guide1,
                state.TransmissionSignal.Guide2,
                state.TransmissionSignal.Moments,
                state.TransmissionSignal.YoungHistorySignal,
                state.TransmissionHitDistance,
                state.TransmissionSignal.Signal,
                2);

            cmd.EndSample(_transmissionDenoiseMarker);

            cmd.BeginSample(_compositeMarker);

            bindLightingAliasResources(
                cmd,
                state,
                surfaceDenoised,
                reflectionDenoised,
                transmissionDenoised,
                state.Result);

            DispatchCompute(cmd, buildLightingAliases, groupsX, groupsY);
            cmd.EndSample(_compositeMarker);
        }

        private static void DispatchCompute(CommandBuffer cmd, ComputeKernel kernel, int groupsX, int groupsY)
        {
            cmd.DispatchCompute(kernel.Compute, kernel.Kernel, groupsX, groupsY, 1);
        }

        private static void SetSignalKeyword(CommandBuffer cmd, ComputeShader compute, int signalKind)
        {
            for (var i = 0; i < _signalKeywords.Length; i++)
            {
                var keyword = new LocalKeyword(compute, _signalKeywords[i]);

                cmd.SetKeyword(compute, keyword, i == signalKind);
            }
        }

        private static RTHandle DispatchSignal(
            CommandBuffer cmd,
            CameraState state,
            LucentPathRendererSettings asset,
            ComputeKernel historyFix,
            ComputeKernel atrousFilter,
            int groupsX,
            int groupsY,
            int iterations,
            RTHandle source,
            RTHandle historyFixedSource,
            RTHandle target,
            RTHandle guide0,
            RTHandle guide1,
            RTHandle guide2,
            RTHandle moments,
            RTHandle youngHistorySignal,
            RTHandle hitDistance,
            RTHandle historyFeedback,
            int signalKind)
        {
            SetSignalKeyword(cmd, historyFix.Compute, signalKind);

            cmd.BeginSample(_historyFixMarker);

            DispatchHistoryFix(
                cmd,
                state,
                asset,
                historyFix,
                groupsX,
                groupsY,
                source,
                historyFixedSource,
                guide0,
                guide1,
                guide2,
                moments,
                youngHistorySignal,
                hitDistance);

            cmd.EndSample(_historyFixMarker);

            cmd.BeginSample(_atrousMarker);

            var compute = atrousFilter.Compute;
            var kernel = atrousFilter.Kernel;
            RTHandle input = historyFixedSource;
            RTHandle resolvedDenoised = target;

            cmd.SetComputeFloatParam(compute, AtrousDepthPhiProperty, Mathf.Max(.1f, asset.ATrousSettings.AtrousDepthPhi));
            cmd.SetComputeFloatParam(compute, AtrousNormalPhiProperty, Mathf.Max(1f, asset.ATrousSettings.AtrousNormalPhi));
            cmd.SetComputeIntParam(compute, YoungHistoryFramesProperty, Mathf.Clamp(asset.DisocclusionRecoverySettings.YoungHistoryFrames, 0, 16));
            cmd.SetComputeTextureParam(compute, kernel, PrimaryRoughnessProperty, state.PrimaryRoughness.nameID);

            for (var i = 0; i < iterations; i++)
            {
                RTHandle output;

                if (i == 0 && historyFeedback != null)
                {
                    output = historyFeedback;
                }
                else if (i == iterations - 1)
                {
                    output = target;
                }
                else
                {
                    var scratchIndex = historyFeedback != null ? i - 1 : i;

                    output = (scratchIndex & 1) == 0 ? state.FilterPing : state.FilterPong;
                }

                cmd.SetComputeIntParam(compute, AtrousStepSizeProperty, 1 << i);
                cmd.SetComputeTextureParam(compute, kernel, AtrousInputProperty, input.nameID);
                cmd.SetComputeTextureParam(compute, kernel, AtrousOutputProperty, output.nameID);
                cmd.SetComputeTextureParam(compute, kernel, Guide0Property, guide0.nameID);
                cmd.SetComputeTextureParam(compute, kernel, Guide1Property, guide1.nameID);
                cmd.SetComputeTextureParam(compute, kernel, Guide2Property, guide2.nameID);
                cmd.SetComputeTextureParam(compute, kernel, MomentsProperty, moments.nameID);
                cmd.SetComputeTextureParam(compute, kernel, YoungHistorySignalProperty, youngHistorySignal.nameID);
                cmd.SetComputeTextureParam(compute, kernel, PrimaryRoughnessProperty, state.PrimaryRoughness.nameID);

                DispatchCompute(cmd, atrousFilter, groupsX, groupsY);

                input = output;
                resolvedDenoised = output;
            }

            cmd.EndSample(_atrousMarker);

            return resolvedDenoised;
        }

        private static void DispatchHistoryFix(
            CommandBuffer cmd,
            CameraState state,
            LucentPathRendererSettings asset,
            ComputeKernel historyFix,
            int groupsX,
            int groupsY,
            RTHandle source,
            RTHandle output,
            RTHandle guide0,
            RTHandle guide1,
            RTHandle guide2,
            RTHandle moments,
            RTHandle youngHistorySignal,
            RTHandle hitDistance)
        {
            var compute = historyFix.Compute;
            var kernel = historyFix.Kernel;

            cmd.SetComputeFloatParam(compute, AtrousDepthPhiProperty, Mathf.Max(.1f, asset.ATrousSettings.AtrousDepthPhi));
            cmd.SetComputeFloatParam(compute, AtrousNormalPhiProperty, Mathf.Max(1f, asset.ATrousSettings.AtrousNormalPhi));
            cmd.SetComputeTextureParam(compute, kernel, HistoryFixInputProperty, source.nameID);
            cmd.SetComputeTextureParam(compute, kernel, HistoryFixOutputProperty, output.nameID);
            cmd.SetComputeTextureParam(compute, kernel, Guide0Property, guide0.nameID);
            cmd.SetComputeTextureParam(compute, kernel, Guide1Property, guide1.nameID);
            cmd.SetComputeTextureParam(compute, kernel, Guide2Property, guide2.nameID);
            cmd.SetComputeTextureParam(compute, kernel, MomentsProperty, moments.nameID);
            cmd.SetComputeTextureParam(compute, kernel, YoungHistorySignalProperty, youngHistorySignal.nameID);
            cmd.SetComputeTextureParam(compute, kernel, HistoryFixHitDistanceProperty, hitDistance.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PrimaryRoughnessProperty, state.PrimaryRoughness.nameID);

            DispatchCompute(cmd, historyFix, groupsX, groupsY);
        }
    }
}
