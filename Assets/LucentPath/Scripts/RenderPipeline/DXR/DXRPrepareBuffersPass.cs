using LucentPath.Upscale.DLSS;
using UnityEngine;
using UnityEngine.Rendering;
using static LucentPath.Rendering.PipelineShaderProperties;

namespace LucentPath.Rendering
{
    internal class DXRPrepareBuffersPass
    {
        private readonly DXRPipelineResources _resources;

        private string _lastWarning;

        public DXRPrepareBuffersPass(DXRPipelineResources resources)
        {
            _resources = resources;
        }

        public void Dispatch(CommandBuffer cmd, CameraState state, Matrix4x4 gpuProjection, int motionVectorSpace, int groupsX, int groupsY)
        {
            var compute = _resources.PrepareBuffers.Compute;
            var kernel = _resources.PrepareBuffers.Kernel;
            var viewToClipZ = new Vector4(gpuProjection.m22, gpuProjection.m23, gpuProjection.m32, gpuProjection.m33);
            float skyDepth = SystemInfo.usesReversedZBuffer ? 0f : 1f;
            bool singleAtrousIteration = Mathf.Max(1, _resources.Asset.ATrousSettings.AtrousIterations) == 1;
            var surfaceDenoised = singleAtrousIteration ? state.SurfaceSignal.Signal : state.SurfaceSignal.DenoisedSignal;
            var reflectionDenoised = singleAtrousIteration ? state.ReflectionSignal.Signal : state.ReflectionSignal.DenoisedSignal;
            var transmissionDenoised = singleAtrousIteration ? state.TransmissionSignal.Signal : state.TransmissionSignal.DenoisedSignal;

            cmd.SetComputeVectorParam(compute, PreparedViewToClipZProperty, viewToClipZ);
            cmd.SetComputeVectorParam(compute, PreparedMotionVectorScaleProperty, new Vector4(1f, 1f, 0f, 0f));
            cmd.SetComputeFloatParam(compute, PreparedSkyDepthProperty, skyDepth);
            cmd.SetComputeIntParam(compute, PreparedMotionVectorSpaceProperty, motionVectorSpace);
            cmd.SetComputeTextureParam(compute, kernel, Guide0Property, state.SurfaceSignal.Guide0.nameID);
            cmd.SetComputeTextureParam(compute, kernel, Guide1Property, state.SurfaceSignal.Guide1.nameID);
            cmd.SetComputeTextureParam(compute, kernel, ReflectionGuide0Property, state.ReflectionSignal.Guide0.nameID);
            cmd.SetComputeTextureParam(compute, kernel, ReflectionGuide1Property, state.ReflectionSignal.Guide1.nameID);
            cmd.SetComputeTextureParam(compute, kernel, TransmissionGuide0Property, state.TransmissionSignal.Guide0.nameID);
            cmd.SetComputeTextureParam(compute, kernel, TransmissionGuide1Property, state.TransmissionSignal.Guide1.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PreparedDepthProperty, state.PreparedDepth.nameID);
            cmd.SetComputeTextureParam(compute, kernel, PreparedMotionVectorsProperty, state.PreparedMotionVectors.nameID);
            cmd.SetComputeTextureParam(compute, kernel, DlssSurfaceSignalProperty, surfaceDenoised.nameID);
            cmd.SetComputeTextureParam(compute, kernel, DlssReflectionSignalProperty, reflectionDenoised.nameID);
            cmd.SetComputeTextureParam(compute, kernel, DlssTransmissionSignalProperty, transmissionDenoised.nameID);
            cmd.SetComputeTextureParam(compute, kernel, DlssTransparencyMaskProperty, state.DlssTransparencyMask.nameID);
            cmd.SetComputeTextureParam(compute, kernel, DlssBiasColorMaskProperty, state.DlssBiasColorMask.nameID);
            DXRKernelLibrary.DispatchCompute(cmd, _resources.PrepareBuffers, groupsX, groupsY);
        }

        public int ResolveMotionVectorSpace(bool dlssActive)
        {
            if (dlssActive && _resources.Asset.DlssSettings != null)
            {
                return (int)_resources.Asset.DlssSettings.MotionVectorSpace;
            }

            return (int)LucentPathDlssMotionVectorSpace.NormalizedDeviceCoordinates;
        }

        public void WarnOnce(string message)
        {
            if (string.IsNullOrEmpty(message) || string.Equals(_lastWarning, message, System.StringComparison.Ordinal))
            {
                return;
            }

            _lastWarning = message;

            Debug.LogWarning($"LucentPath Prepare Buffers: {message}");
        }
    }
}
