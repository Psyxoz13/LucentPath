using LucentPath.Streamline;
using LucentPath.Upscale.DLSS;
using UnityEngine;
using UnityEngine.Rendering;

namespace LucentPath.Rendering
{
    internal class DXRStreamlineDlssRayReconstructionPass
    {
        private readonly DXRPipelineResources _resources;

        private string _lastWarning;
        private bool _isReportedEvaluateSuccess;

        public DXRStreamlineDlssRayReconstructionPass(DXRPipelineResources resources)
        {
            _resources = resources;
        }

        public bool ShouldRun(Camera camera, DlssRenderPlan dlssPlan)
        {
            var settings = _resources.Asset.StreamlineSettings;

            if (settings == null || settings.IsRuntimeActive == false)
            {
                return false;
            }

            settings.Validate();

            if (dlssPlan.IsIntegration == false)
            {
                WarnOnce("DLSS-RR requires an active DLSS quality mode because Streamline RR uses the DLSS render/output resolution plan.");

                return false;
            }

            if (camera.cameraType != CameraType.Game && settings.IsSceneViewAllowed == false)
            {
                return false;
            }

            if (_resources.Asset.DlssSettings != null &&
                _resources.Asset.DlssSettings.IsDisableWhenDebugViewIsEnabled &&
                _resources.Asset.PathTracingSettings.IsDebugVisualizationAvailable)
            {
                return false;
            }

            return true;
        }

        public bool Dispatch(
            CommandBuffer cmd,
            Camera camera,
            CameraState state,
            DlssRenderPlan dlssPlan,
            Matrix4x4 nonJitteredGpuProjection,
            Matrix4x4 nonJitteredViewProjection,
            Matrix4x4 inverseNonJitteredViewProjection,
            Vector2 jitterPixels,
            bool resetHistory)
        {
            var settings = _resources.Asset.StreamlineSettings;

            if (settings == null)
            {
                return false;
            }

            string fallbackProjectId = _resources.Asset.DlssSettings != null ? _resources.Asset.DlssSettings.ProjectId : string.Empty;

            if (LucentPathStreamlineNative.TryInitialize(settings, fallbackProjectId, out string initError) == false)
            {
                WarnOnce($"Streamline initialization failed: {initError}");

                return false;
            }

            int lastStatus = LucentPathStreamlineNative.GetLastEvaluateStatus(out string evaluateError);

            if (lastStatus < 0)
            {
                WarnOnce($"Previous DLSS-RR evaluate failed: {evaluateError}");

                return false;
            }

            if (lastStatus > 0 && _isReportedEvaluateSuccess == false)
            {
                _isReportedEvaluateSuccess = true;

                Debug.Log("LucentPath Streamline DLSS-RR: native evaluate completed successfully.");
            }

            if (LucentPathStreamlineNative.IsDlssRayReconstructionSupported(out string supportError) == false)
            {
                WarnOnce($"DLSS-RR is unavailable: {supportError}");

                return false;
            }

            if (TryBuildDesc(
                camera,
                state,
                dlssPlan,
                nonJitteredGpuProjection,
                nonJitteredViewProjection,
                inverseNonJitteredViewProjection,
                jitterPixels,
                resetHistory,
                out LucentPathStreamlineNative.EvaluateDesc desc,
                out string error) == false)
            {
                WarnOnce(error);

                return false;
            }

            desc.ApplySettings(settings);

            if (LucentPathStreamlineNative.IssueEvaluateDlssRayReconstructionEvent(cmd, ref desc, out error))
            {
                return true;
            }

            WarnOnce($"Failed to issue DLSS-RR render event: {error}");

            return false;
        }

        public void WarnOnce(string message)
        {
            if (string.IsNullOrEmpty(message) || string.Equals(_lastWarning, message, System.StringComparison.Ordinal))
            {
                return;
            }

            _lastWarning = message;

            if (_resources.Asset.StreamlineSettings == null || _resources.Asset.StreamlineSettings.IsWarnWhenUnavailable)
            {
                Debug.LogWarning($"LucentPath Streamline DLSS-RR: {message}");
            }
        }

        private bool TryBuildDesc(
            Camera camera,
            CameraState state,
            DlssRenderPlan dlssPlan,
            Matrix4x4 nonJitteredGpuProjection,
            Matrix4x4 nonJitteredViewProjection,
            Matrix4x4 inverseNonJitteredViewProjection,
            Vector2 jitterPixels,
            bool resetHistory,
            out LucentPathStreamlineNative.EvaluateDesc desc,
            out string error)
        {
            desc = default;

            if (state.Result == null || state.DlssOutput == null || state.PreparedDepth == null || state.PreparedMotionVectors == null ||
                state.PrimaryDiffuseAlbedo == null || state.PrimarySpecularF0 == null || state.SurfaceSignal.Guide0 == null || state.PrimaryRoughness == null)
            {
                error = "DLSS-RR resources are incomplete. Required: color input/output, prepared depth/motion, diffuse/specular albedo, normals and roughness.";

                return false;
            }

            var colorInput = GetNativeTexturePtr(state.Result.rt);
            var colorOutput = GetNativeTexturePtr(state.DlssOutput.rt);
            var depth = GetNativeTexturePtr(state.PreparedDepth.rt);
            var motion = GetNativeTexturePtr(state.PreparedMotionVectors.rt);
            var diffuseAlbedo = GetNativeTexturePtr(state.PrimaryDiffuseAlbedo.rt);
            var specularAlbedo = GetNativeTexturePtr(state.PrimarySpecularF0.rt);
            var normals = GetNativeTexturePtr(state.SurfaceSignal.Guide0.rt);
            var roughness = GetNativeTexturePtr(state.PrimaryRoughness.rt);
            var specularHitDistance = state.ReflectionHitDistance != null ? GetNativeTexturePtr(state.ReflectionHitDistance.rt) : System.IntPtr.Zero;

            if (colorInput == System.IntPtr.Zero || colorOutput == System.IntPtr.Zero || depth == System.IntPtr.Zero ||
                motion == System.IntPtr.Zero || diffuseAlbedo == System.IntPtr.Zero || specularAlbedo == System.IntPtr.Zero ||
                normals == System.IntPtr.Zero || roughness == System.IntPtr.Zero)
            {
                error = "DLSS-RR failed to resolve one or more native D3D12 texture pointers.";

                return false;
            }

            var view = camera.worldToCameraMatrix;
            var clipToPrevClip = state.IsValid ? state.LastViewProj * inverseNonJitteredViewProjection : Matrix4x4.identity;
            var prevClipToClip = clipToPrevClip.inverse;
            var transform = camera.transform;
            Vector2 mvecScale = ResolveMotionVectorScale(_resources.Asset.DlssSettings, dlssPlan.InputWidth, dlssPlan.InputHeight);

            desc = new LucentPathStreamlineNative.EvaluateDesc
            {
                FrameIndex = _resources.FrameIndex,
                ViewportId = state.ViewportId,
                InputWidth = dlssPlan.InputWidth,
                InputHeight = dlssPlan.InputHeight,
                OutputWidth = dlssPlan.OutputWidth,
                OutputHeight = dlssPlan.OutputHeight,
                DlssMode = (int)dlssPlan.Quality,
                ResetHistory = resetHistory ? 1 : 0,
                DepthInverted = SystemInfo.usesReversedZBuffer ? 1 : 0,
                Orthographic = camera.orthographic ? 1 : 0,
                JitterX = jitterPixels.x,
                JitterY = jitterPixels.y,
                MotionVectorScaleX = mvecScale.x,
                MotionVectorScaleY = mvecScale.y,
                CameraNear = camera.nearClipPlane,
                CameraFar = camera.farClipPlane,
                CameraFovRadians = camera.fieldOfView * Mathf.Deg2Rad,
                CameraAspectRatio = camera.aspect,
                CameraPositionX = transform.position.x,
                CameraPositionY = transform.position.y,
                CameraPositionZ = transform.position.z,
                CameraUpX = transform.up.x,
                CameraUpY = transform.up.y,
                CameraUpZ = transform.up.z,
                CameraRightX = transform.right.x,
                CameraRightY = transform.right.y,
                CameraRightZ = transform.right.z,
                CameraForwardX = transform.forward.x,
                CameraForwardY = transform.forward.y,
                CameraForwardZ = transform.forward.z,
                ColorInput = colorInput,
                ColorOutput = colorOutput,
                Depth = depth,
                MotionVectors = motion,
                DiffuseAlbedo = diffuseAlbedo,
                SpecularAlbedo = specularAlbedo,
                Normals = normals,
                Roughness = roughness,
                NormalRoughness = System.IntPtr.Zero,
                SpecularMotionVectors = System.IntPtr.Zero,
                SpecularHitDistance = specularHitDistance,
                CameraViewToClip = LucentPathStreamlineNative.Matrix4x4Native.From(nonJitteredGpuProjection),
                ClipToCameraView = LucentPathStreamlineNative.Matrix4x4Native.From(nonJitteredGpuProjection.inverse),
                ClipToPrevClip = LucentPathStreamlineNative.Matrix4x4Native.From(clipToPrevClip),
                PrevClipToClip = LucentPathStreamlineNative.Matrix4x4Native.From(prevClipToClip),
                WorldToCameraView = LucentPathStreamlineNative.Matrix4x4Native.From(view),
                CameraViewToWorld = LucentPathStreamlineNative.Matrix4x4Native.From(view.inverse)
            };

            error = null;

            return true;
        }

        private static System.IntPtr GetNativeTexturePtr(RenderTexture texture)
        {
            return texture != null && texture.IsCreated() ? texture.GetNativeTexturePtr() : System.IntPtr.Zero;
        }

        private static Vector2 ResolveMotionVectorScale(DlssSettings settings, int inputWidth, int inputHeight)
        {
            if (settings == null || settings.IsAutoMotionVectorApiScale == false)
            {
                return settings != null ? new Vector2(settings.MotionVectorScaleX, settings.MotionVectorScaleY) : Vector2.one;
            }

            switch (settings.MotionVectorSpace)
            {
                case LucentPathDlssMotionVectorSpace.PixelSpace:
                    return new Vector2(1.0f / Mathf.Max(1, inputWidth), 1.0f / Mathf.Max(1, inputHeight));
                case LucentPathDlssMotionVectorSpace.UvSpace:
                    return new Vector2(2.0f, 2.0f);
                case LucentPathDlssMotionVectorSpace.NormalizedDeviceCoordinates:
                default:
                    return Vector2.one;
            }
        }
    }
}
