using LucentPath.Tools;
using LucentPath.Upscale.DLSS;
using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace LucentPath.Rendering
{
    public class LucentPathRenderer : IDisposable
    {
        private static readonly CustomSampler _blitMarker = CustomSampler.Create("LucentPath.DXR.Blit", true);
        private static readonly CustomSampler _dlssSuperResolutionMarker = CustomSampler.Create("LucentPath.DXR.DLSS.SuperResolution", true);
        private static readonly CustomSampler _postSharpenMarker = CustomSampler.Create("LucentPath.DXR.PostProcess.Sharpen", true);
        private const int SobolDimensionCount = 256;
        private const int SobolMatrixSize = 52;
        private static uint[] _sobolMatrices;

        private readonly DXRPipelineResources _resources;
        private readonly DXRKernelLibrary _kernels;
        private readonly DXRCameraResourceCache _cameraResources;
        private readonly DXRDlssPass _dlss;
        private readonly DXRStreamlineDlssRayReconstructionPass _streamlineDlssRR;
        private readonly DXRPrepareBuffersPass _prepareBuffers;
        private readonly DXRPathTracingRayGenPass _pathTracingRayGen;
        private readonly DXRDenoisePass _denoise;
        private readonly DXRSceneGpuUploader _sceneUploader;
        private readonly DXRBloomPass _bloom;
        private readonly SkyboxCubemapBaker _skyboxBaker = new();
        private readonly EnvironmentImportanceSampler _envImportance = new();
        private readonly DistantLightIndexBuffer _distantLightIndices = new();
        private readonly MultiScatterLut _msLut = new();
        private readonly DXRMaterialTable _materialTable = new();
        private RayTracingAccelerationStructure _rtas;

        private RayTracingInstanceCullingConfig _rtasCullingConfig;
#if UNITY_EDITOR
        private bool _isEditorWarmupRequested;

#endif

        public LucentPathRenderer(LucentPathRendererSettings asset)
        {
            _resources = new DXRPipelineResources(asset);

            _kernels = new DXRKernelLibrary(_resources);
            _cameraResources = new DXRCameraResourceCache(_resources);
            _dlss = new DXRDlssPass(_resources);
            _streamlineDlssRR = new DXRStreamlineDlssRayReconstructionPass(_resources);
            _prepareBuffers = new DXRPrepareBuffersPass(_resources);
            _pathTracingRayGen = new DXRPathTracingRayGenPass(_resources, _kernels);
            _denoise = new DXRDenoisePass(_resources, _kernels);
            _sceneUploader = new DXRSceneGpuUploader(_resources);
            _bloom = new DXRBloomPass(_resources);

            CreateMaterials(asset);

            var rtasSettings = new RayTracingAccelerationStructure.Settings
            {
                rayTracingModeMask = RayTracingAccelerationStructure.RayTracingModeMask.Everything,
                managementMode = RayTracingAccelerationStructure.ManagementMode.Manual,
                layerMask = ~0
            };

            _rtas = new RayTracingAccelerationStructure(rtasSettings);
            _resources.RTAS = _rtas;
            _rtasCullingConfig = CreateRtasCullingConfig();

            _kernels.Refresh();
            EnsureSobolMatricesBuffer();

            _envImportance.SetBuildShader(_resources.Asset.EnvBuildCdfCompute);
            _kernels.SetEnvironmentImportanceSampler(_envImportance);
            _msLut.SetBakerShader(_resources.Asset.BsdfMultiScatterBakeCompute);
            _kernels.SetMultiScatterLut(_msLut);
        }

        public static bool IsSupported => SystemInfo.supportsComputeShaders && SystemInfo.supportsRayTracing && SystemInfo.supportsInlineRayTracing;

        public void BeginFrame()
        {
            _kernels.Refresh();
            EnsureSobolMatricesBuffer();
        }

        public void EndFrame()
        {
            _resources.FrameIndex++;

#if UNITY_EDITOR
            if (_isEditorWarmupRequested)
            {
                _isEditorWarmupRequested = false;

                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            }
#endif
        }

        public void RenderCamera(
            CommandBuffer cmd,
            Camera camera,
            CullingResults cullingResults,
            RTHandle target,
            bool isTargetYFlipped,
            int outputWidth,
            int outputHeight)
        {
            if (_resources.SobolMatricesBuffer == null)
            {
                return;
            }

#if UNITY_EDITOR
            _isEditorWarmupRequested |= TryRenderCamera(cmd, camera, cullingResults, target, isTargetYFlipped, outputWidth, outputHeight);
#else
            TryRenderCamera(cmd, camera, cullingResults, target, isTargetYFlipped, outputWidth, outputHeight);
#endif
        }

        private bool TryRenderCamera(
            CommandBuffer cmd,
            Camera camera,
            CullingResults cullingResults,
            RTHandle target,
            bool isTargetYFlipped,
            int outputWidth,
            int outputHeight)
        {
            var dlssPlan = _dlss.ResolveRenderPlan(camera, outputWidth, outputHeight);

            var state = _cameraResources.GetCameraState(camera);

            _cameraResources.EnsureTextures(
                state,
                dlssPlan.InputWidth,
                dlssPlan.InputHeight,
                dlssPlan.OutputWidth,
                dlssPlan.OutputHeight,
                dlssPlan.IsIntegration);

            if (dlssPlan.IsIntegration == false)
            {
                state.DlssUpscaler.ReleaseResources();
            }

            var dlssSettings = _resources.Asset.DlssSettings;
            var view = camera.worldToCameraMatrix;
            var nonJitteredProj = camera.nonJitteredProjectionMatrix;
            var nonJitteredGpuProj = GL.GetGPUProjectionMatrix(nonJitteredProj, false);
            var nonJitteredViewProj = nonJitteredGpuProj * view;
            var renderProj = nonJitteredProj;
            var dlssJitterPixels = Vector2.zero;
            var dlssJitterDevice = Vector2.zero;

            if (dlssPlan.IsIntegration && dlssSettings != null && dlssSettings.IsDlssJitterEnabled)
            {
                dlssJitterPixels = DlssJitter.GetHaltonJitterPixels(_resources.FrameIndex);
                dlssJitterDevice = DlssJitter.ToDeviceCoordinates(dlssJitterPixels, dlssPlan.InputWidth, dlssPlan.InputHeight);
                renderProj = DlssJitter.ApplyJitterToProjection(nonJitteredProj, dlssJitterPixels, dlssPlan.InputWidth, dlssPlan.InputHeight);
            }

            var gpuProj = GL.GetGPUProjectionMatrix(renderProj, false);
            var viewProj = gpuProj * view;
            var invViewProj = viewProj.inverse;
            var invNonJitteredViewProj = nonJitteredViewProj.inverse;
            int prepareBuffersMotionVectorSpace = _prepareBuffers.ResolveMotionVectorSpace(dlssPlan.IsIntegration);

            DXRPresentationPass.GetClipSpaceZRange(out float nearClipNdc, out float farClipNdc);

            var lensState = CameraLensState.Resolve(camera);
            var renderSettingsHash = lensState.AppendHash(GetRenderSettingsHash(_resources.Asset));
#if UNITY_EDITOR
            int editorCameraHash = GetEditorCameraHash(camera, dlssPlan, lensState);
            int editorInvalidationVersion = EditorRenderInvalidation.Version;
            bool editModeGameCamera = IsEditModeGameCamera(camera);

#endif

            var resetHistory =
                state.IsValid == false ||
                state.LastRenderSettingsHash != renderSettingsHash ||
                state.IsLastDlssActive != dlssPlan.IsIntegration ||
                state.LastDlssQuality != (int)dlssPlan.Quality ||
                state.LastDlssSettingsHash != dlssPlan.SettingsHash;

#if UNITY_EDITOR
            if (editModeGameCamera)
            {
                resetHistory |=
                    state.LastEditorCameraHash != editorCameraHash ||
                    state.LastEditorInvalidationVersion != editorInvalidationVersion;

                if (resetHistory == false && state.SampleCount >= GetEditorWarmupFrameCount(_resources.Asset))
                {
                    RTHandle cachedSource = state.IsLastPresentationUsedDlss && state.DlssOutput != null ? state.DlssOutput : state.Result;

                    PresentCameraSource(
                        cmd,
                        camera,
                        cachedSource,
                        target,
                        isTargetYFlipped,
                        dlssPlan,
                        state.IsLastPresentationUsedDlss,
                        dlssSettings);

                    return false;
                }
            }

#endif
            if (resetHistory)
            {
                state.SampleCount = 0;
                state.IsValid = false;
            }

            var cameraJitterUv = GetCameraJitterUv(camera, viewProj, nonJitteredViewProj);
            var prevCameraJitterUv = state.IsValid ? state.LastCameraJitterUv : cameraJitterUv;

            cmd.SetGlobalVector(PipelineShaderProperties.CameraJitterUvProperty, cameraJitterUv);
            cmd.SetGlobalVector(PipelineShaderProperties.PrevCameraJitterUvProperty, prevCameraJitterUv);
            cmd.SetGlobalTexture(PipelineShaderProperties.CheckerboardHistoryMomentsProperty, state.PrevMoments.nameID);

            cmd.SetGlobalInt(
                PipelineShaderProperties.CheckerboardFullRateHistoryFramesProperty,
                state.IsValid ? Mathf.Clamp(_resources.Asset.CheckerboardSettings.FullRateHistoryFrames, 0, 64) : 0);

            Vector3 cameraDisplacement = camera.transform.position - state.LastCameraWorldPos;
            float depthDisplacement = Vector3.Dot(cameraDisplacement, camera.transform.forward);
            bool hasDominantAxialMotion = depthDisplacement * depthDisplacement > 0.5f * cameraDisplacement.sqrMagnitude;
            bool limitCheckerboardPromotion = state.IsValid &&
                !hasDominantAxialMotion && !state.LastViewProj.Equals(nonJitteredViewProj);
            int promotionPhase = limitCheckerboardPromotion ? 1 + ((_resources.FrameIndex >> 1) & 3) : 0;
            cmd.SetGlobalInt(PipelineShaderProperties.CheckerboardPromotionPhaseProperty, promotionPhase);

            EnsureEmissiveSurfels();
            BindBlueNoiseGlobals(cmd);
            BindEnvironmentGlobals(cmd, camera);
            _msLut.BakeIfNeeded(cmd);
            _msLut.BindGlobals(cmd);

            _rtasCullingConfig.lodParameters = new LODParameters
            {
                isOrthographic = camera.orthographic,
                orthoSize = camera.orthographicSize,
                cameraPosition = camera.transform.position,
                fieldOfView = camera.fieldOfView,
                cameraPixelHeight = camera.pixelHeight
            };

            _rtas.ClearInstances();

            var rtasCullingResults = _rtas.CullInstances(ref _rtasCullingConfig);

            _materialTable.Update(rtasCullingResults.materialsCRC);
            cmd.BuildRayTracingAccelerationStructure(_rtas);
            EnsureMaterialCache();

            if (_resources.Asset.PathTracingSettings.IsEmissiveSurfelNee && state.IsValid == false)
            {
                ResetEmissiveSurfels(cmd);
            }

            _sceneUploader.BuildLightsFromCullingResults(cullingResults, out GpuLight[] lights, out int analyticAreaLightCount);
            _sceneUploader.UploadLights(lights);
            _distantLightIndices.Build(lights);
            _distantLightIndices.BindGlobals(cmd);

            LucentPath.Lighting.LightPowerEstimator.PushAnalyticTotalPower(
                cmd,
                LucentPath.Lighting.LightPowerEstimator.ComputeAnalyticTotalPower(lights));

            SubsurfaceGlobals.PushGlobals(cmd, _resources.Asset.SubsurfaceSettings);

            _kernels.DispatchGuidingFieldDecay(cmd, state);

            _pathTracingRayGen.Dispatch(
                cmd,
                camera,
                state,
                lights.Length,
                analyticAreaLightCount,
                view,
                viewProj,
                nonJitteredViewProj,
                invViewProj,
                state.LastViewMatrix,
                state.LastViewProj,
                state.LastCameraWorldPos,
                nearClipNdc,
                farClipNdc,
                camera.orthographic,
                lensState,
                camera.transform.position,
                _resources.FrameIndex,
                state.IsValid);

            var groupsX = Mathf.CeilToInt(state.Width / 8f);
            var groupsY = Mathf.CeilToInt(state.Height / 8f);
            bool streamlineRrRequested = _streamlineDlssRR.ShouldRun(camera, dlssPlan);

            _denoise.Dispatch(cmd, state, groupsX, groupsY);

            if (dlssPlan.IsIntegration)
            {
                _prepareBuffers.Dispatch(cmd, state, gpuProj, prepareBuffersMotionVectorSpace, groupsX, groupsY);
            }

            RTHandle presentationSource = state.Result;
            bool dlssSucceeded = false;
            bool streamlineRrSucceeded = false;

            if (streamlineRrRequested)
            {
                cmd.BeginSample(_dlssSuperResolutionMarker);

                streamlineRrSucceeded = _streamlineDlssRR.Dispatch(
                    cmd,
                    camera,
                    state,
                    dlssPlan,
                    nonJitteredGpuProj,
                    nonJitteredViewProj,
                    invNonJitteredViewProj,
                    dlssJitterPixels,
                    resetHistory);

                cmd.EndSample(_dlssSuperResolutionMarker);

                if (streamlineRrSucceeded)
                {
                    dlssSucceeded = true;
                    presentationSource = state.DlssOutput;
                }
            }

            if (dlssPlan.IsIntegration && streamlineRrSucceeded == false)
            {
                cmd.BeginSample(_dlssSuperResolutionMarker);

                if (state.PreparedDepth != null && state.PreparedMotionVectors != null)
                {
                    var dlssColorInput = _kernels.TryDispatchAntiFirefly(
                        cmd,
                        state,
                        state.Result,
                        state.FilterPing) ? state.FilterPing : state.Result;

                    var inputs = new DlssFrameInputs(
                        dlssColorInput.rt,
                        state.DlssOutput.rt,
                        state.PreparedDepth.rt,
                        state.PreparedMotionVectors.rt,
                        dlssPlan.InputWidth,
                        dlssPlan.InputHeight,
                        dlssPlan.OutputWidth,
                        dlssPlan.OutputHeight,
                        dlssJitterDevice,
                        null,
                        state.DlssTransparencyMask.rt,
                        state.DlssBiasColorMask.rt);

                    dlssSucceeded = state.DlssUpscaler.Execute(cmd, inputs, _resources.Asset.DlssSettings, resetHistory, out string dlssError);

                    if (dlssSucceeded == false)
                    {
                        _dlss.WarnOnce($"DLSS execution failed: {dlssError}. Native rendering will be used after this frame.");

                        _resources.IsDlssRuntimeFailed = _resources.Asset.DlssSettings == null || _resources.Asset.DlssSettings.IsFallbackToNativeWhenUnavailable;
                    }
                }
                else
                {
                    _prepareBuffers.WarnOnce("Prepared depth/motion buffers are unavailable. Native rendering will be used after this frame.");

                    _resources.IsDlssRuntimeFailed = _resources.Asset.DlssSettings == null || _resources.Asset.DlssSettings.IsFallbackToNativeWhenUnavailable;
                }

                cmd.EndSample(_dlssSuperResolutionMarker);

                if (dlssSucceeded)
                {
                    presentationSource = state.DlssOutput;
                }
            }

            var taaSettings = _resources.Asset.TaaSettings;

            if (dlssSucceeded == false && taaSettings != null && taaSettings.IsEnabled && _resources.TaaResolve.Compute != null && state.FilterPing != null)
            {
                if (_kernels.DispatchTaa(cmd, state, state.Result, state.FilterPing, state.IsValid, taaSettings.Feedback))
                {
                    cmd.CopyTexture(state.FilterPing.nameID, state.PrevResult.nameID);

                    presentationSource = state.FilterPing;
                }
            }

            PresentCameraSource(cmd, camera, presentationSource, target, isTargetYFlipped, dlssPlan, dlssSucceeded, dlssSettings);

            state.LastViewProj = nonJitteredViewProj;
            state.LastCameraJitterUv = cameraJitterUv;
            state.LastViewMatrix = view;
            state.LastCameraWorldPos = camera.transform.position;
            state.LastOrthographic = camera.orthographic ? 1 : 0;
            state.LastRenderSettingsHash = renderSettingsHash;
            state.IsLastDlssActive = dlssPlan.IsIntegration;
            state.LastDlssQuality = (int)dlssPlan.Quality;
            state.LastDlssSettingsHash = dlssPlan.SettingsHash;
            state.IsLastPresentationUsedDlss = dlssSucceeded;
#if UNITY_EDITOR
            state.LastEditorCameraHash = editorCameraHash;
            state.LastEditorInvalidationVersion = editorInvalidationVersion;
#endif
            state.IsValid = true;

            state.SampleCount++;
            _cameraResources.SwapHistoryBuffers(state);

#if UNITY_EDITOR
            return editModeGameCamera && state.SampleCount < GetEditorWarmupFrameCount(_resources.Asset);
#else
            return false;
#endif
        }

        private void PresentCameraSource(
            CommandBuffer blitCmd,
            Camera camera,
            RTHandle presentationSource,
            RTHandle target,
            bool isTargetYFlipped,
            DlssRenderPlan dlssPlan,
            bool dlssSucceeded,
            DlssSettings dlssSettings)
        {
            blitCmd.BeginSample(_blitMarker);

            RenderTargetIdentifier source = presentationSource.nameID;
            bool releasePostSharpenOutput = false;
            bool releaseBloomOutput = false;

            if (dlssSucceeded && dlssSettings != null && dlssSettings.IsSharpeningEnabled && _resources.PostSharpenMaterial != null)
            {
                float sharpness = dlssSettings.ResolveEffectiveSharpness(dlssPlan.OutputWidth, dlssPlan.OutputHeight);

                if (sharpness > 0f)
                {
                    blitCmd.BeginSample(_postSharpenMarker);

                    var sharpenDesc = new RenderTextureDescriptor(dlssPlan.OutputWidth, dlssPlan.OutputHeight, GraphicsFormat.R16G16B16A16_SFloat, 0)
                    {
                        msaaSamples = 1,
                        depthBufferBits = 0,
                        enableRandomWrite = false,
                        sRGB = false,
                        useMipMap = false,
                        autoGenerateMips = false
                    };

                    blitCmd.GetTemporaryRT(PipelineShaderProperties.PostSharpenOutputProperty, sharpenDesc, FilterMode.Bilinear);
                    blitCmd.SetGlobalTexture(PipelineShaderProperties.SourceTexProperty, presentationSource.nameID);
                    blitCmd.SetGlobalFloat(PipelineShaderProperties.PostSharpenAmountProperty, sharpness);
                    blitCmd.SetRenderTarget(PipelineShaderProperties.PostSharpenOutputProperty);
                    blitCmd.SetViewport(new Rect(0f, 0f, dlssPlan.OutputWidth, dlssPlan.OutputHeight));

                    Blitter.BlitTexture(
                        blitCmd,
                        presentationSource.nameID,
                        PipelineShaderProperties.FullScreenBlitScaleBias,
                        _resources.PostSharpenMaterial,
                        PipelineShaderProperties.CompositeCopyPass);

                    blitCmd.EndSample(_postSharpenMarker);

                    source = PipelineShaderProperties.PostSharpenOutputProperty;
                    releasePostSharpenOutput = true;
                }
            }

            if (_bloom.TryExecute(
                blitCmd,
                camera,
                source,
                dlssPlan.OutputWidth,
                dlssPlan.OutputHeight,
                out RenderTargetIdentifier bloomSource))
            {
                source = bloomSource;
                releaseBloomOutput = true;
            }

            if (TryGetTonemapPresentation(camera, out LucentTonemapMode mode, out Texture2D lutTexture, out LucentTonemapLook look) &&
                DXRPresentationPass.HasPass(_resources.CompositeMaterial, PipelineShaderProperties.CompositeTonemapPass))
            {
                if (lutTexture != null)
                {
                    blitCmd.SetGlobalTexture(PipelineShaderProperties.AgxDefaultContrastLutProperty, lutTexture);
                    blitCmd.SetGlobalFloat(PipelineShaderProperties.AgxDefaultContrastLutSizeProperty, lutTexture.width);
                }

                blitCmd.SetGlobalInt(PipelineShaderProperties.TonemapModeProperty, (int)mode);
                blitCmd.SetGlobalInt(PipelineShaderProperties.AgxLookProperty, (int)look);
                blitCmd.SetGlobalFloat(PipelineShaderProperties.PresentationFlipYProperty, isTargetYFlipped ? 1f : 0f);
                Blitter.BlitTexture(blitCmd, source, target, _resources.CompositeMaterial, PipelineShaderProperties.CompositeTonemapPass);
            }
            else
            {
                if (TryGetTonemapPresentation(camera, out _, out _, out _) && _resources.IsAgxPresentationWarningLogged == false)
                {
                    Debug.LogError("LucentPath tonemap is active, but the composite shader has no tonemap presentation pass.", _resources.Asset);

                    _resources.IsAgxPresentationWarningLogged = true;
                }

                Blitter.BlitTexture(blitCmd, source, target, _resources.CopyMaterial, PipelineShaderProperties.CompositeCopyPass);
            }

            if (releasePostSharpenOutput)
            {
                blitCmd.ReleaseTemporaryRT(PipelineShaderProperties.PostSharpenOutputProperty);
            }

            if (releaseBloomOutput)
            {
                blitCmd.ReleaseTemporaryRT(PipelineShaderProperties.BloomOutputProperty);
            }

            blitCmd.EndSample(_blitMarker);
        }

        private bool TryGetTonemapPresentation(Camera camera, out LucentTonemapMode mode, out Texture2D lutTexture, out LucentTonemapLook look)
        {
            mode = LucentTonemapMode.None;
            lutTexture = null;
            look = LucentTonemapLook.Base;

            var tonemap = VolumeManager.instance.stack?.GetComponent<LucentTonemapVolume>();

            if (tonemap == null || tonemap.IsActive() == false)
            {
                return false;
            }

            if (camera.cameraType == CameraType.SceneView && tonemap.ApplyInSceneView.value == false)
            {
                return false;
            }

            mode = tonemap.Mode.value;
            look = tonemap.Look.value;

            if (mode != LucentTonemapMode.AgX)
            {
                return true;
            }

            var lut = tonemap.DefaultContrastLut.value;

            if (lut == null || lut.IsValidOfficialDefaultContrast == false)
            {
                LogMissingAgxLut(lut, tonemap);

                return false;
            }

            lutTexture = lut.GetOrCreateTexture();

            if (lutTexture == null)
            {
                LogMissingAgxLut(lut, tonemap);

                return false;
            }

            return true;
        }

        private void LogMissingAgxLut(AgxSpi1dLut lut, LucentTonemapVolume tonemap)
        {
            if (_resources.IsAgxLutWarningLogged)
            {
                return;
            }

            string reason = lut == null ? "no LUT is assigned" : lut.ValidationMessage;
            UnityEngine.Object context = tonemap != null ? (UnityEngine.Object)tonemap : _resources.Asset;

            Debug.LogError(
                $"LucentPath AgX tonemap is active, but the Lucent Tonemap volume cannot use AgX_Default_Contrast.spi1d because {reason}. Assign the imported official .spi1d asset to the Lucent Tonemap volume's Default Contrast Lut parameter.",
                context);

            _resources.IsAgxLutWarningLogged = true;
        }

        private void EnsureSobolMatricesBuffer()
        {
            if (_resources.SobolMatricesBuffer != null)
            {
                return;
            }

            var sobolMatrices = LoadSobolMatrices();

            _resources.SobolMatricesBuffer = new ComputeBuffer(sobolMatrices.Length, sizeof(uint), ComputeBufferType.Structured);

            _resources.SobolMatricesBuffer.SetData(sobolMatrices);
        }

        private static uint[] LoadSobolMatrices()
        {
            var requiredCount = SobolDimensionCount * SobolMatrixSize;

            if (_sobolMatrices != null && _sobolMatrices.Length == requiredCount)
            {
                return _sobolMatrices;
            }

            _sobolMatrices = Sampling.LucentPathSampling.GetSobolMatrices(SobolDimensionCount, SobolMatrixSize);

            return _sobolMatrices;
        }

        private void BindBlueNoiseGlobals(CommandBuffer cmd)
        {
            var s = _resources.Asset.SamplerSettings;
            bool enabled = s != null && s.BlueNoiseTexture != null;

            cmd.SetGlobalInt(PipelineShaderProperties.BlueNoiseEnabledProperty, enabled ? 1 : 0);

            if (enabled)
            {
                cmd.SetGlobalTexture(PipelineShaderProperties.BlueNoiseTextureProperty, s.BlueNoiseTexture);

                int layers = Mathf.Max(1, s.BlueNoiseTexture.depth);

                cmd.SetGlobalInt(PipelineShaderProperties.BlueNoiseLayerCountProperty, layers);
                cmd.SetGlobalInt(PipelineShaderProperties.BlueNoiseTileWidthProperty, s.BlueNoiseTexture.width);
                cmd.SetGlobalInt(PipelineShaderProperties.BlueNoiseTileHeightProperty, s.BlueNoiseTexture.height);
            }
            else
            {
                cmd.SetGlobalInt(PipelineShaderProperties.BlueNoiseLayerCountProperty, 0);
                cmd.SetGlobalInt(PipelineShaderProperties.BlueNoiseTileWidthProperty, 0);
                cmd.SetGlobalInt(PipelineShaderProperties.BlueNoiseTileHeightProperty, 0);
            }
        }

        private void BindEnvironmentGlobals(CommandBuffer cmd, Camera camera)
        {
            bool hasSky = RenderSettings.skybox != null;

            if (hasSky)
            {
                _skyboxBaker.EnsureResources();

                bool needsRebuild = _skyboxBaker.RequiresBake(RenderSettings.skybox);
                Matrix4x4 restoreView = camera.worldToCameraMatrix;
                Matrix4x4 restoreProj = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true);

                _skyboxBaker.Bake(cmd, restoreView, restoreProj);
                cmd.SetGlobalInt(PipelineShaderProperties.EnvironmentModeProperty, 1);
                cmd.SetGlobalTexture(PipelineShaderProperties.EnvironmentCubemapProperty, _skyboxBaker.BakedCubemap);
                cmd.SetGlobalInt(PipelineShaderProperties.EnvironmentMaxMipProperty, Mathf.Max(0, _skyboxBaker.BakedCubemap.mipmapCount - 1));

                if (needsRebuild)
                {
                    _envImportance.Build(cmd, _skyboxBaker.BakedCubemap);
                }

                _envImportance.BindGlobals(cmd);
            }
            else
            {
                cmd.SetGlobalInt(PipelineShaderProperties.EnvironmentModeProperty, 0);
                cmd.SetGlobalInt(PipelineShaderProperties.EnvironmentMaxMipProperty, 0);

                Color ambientTop = RenderSettings.ambientSkyColor;
                Color ambientBottom = RenderSettings.ambientEquatorColor;

                cmd.SetGlobalColor(PipelineShaderProperties.EnvironmentTopColorProperty, ambientTop);
                cmd.SetGlobalColor(PipelineShaderProperties.EnvironmentBottomColorProperty, ambientBottom);
                cmd.SetGlobalFloat(PipelineShaderProperties.EnvironmentExponentProperty, 1f);
                _envImportance.BindGlobals(cmd);
            }

            cmd.SetGlobalInt(PipelineShaderProperties.EnvNeeEnabledProperty, _resources.Asset.PathTracingSettings.IsEnvironmentNee ? 1 : 0);

            cmd.SetGlobalInt(
                PipelineShaderProperties.LightNeeRisCandidatesProperty,
                Mathf.Clamp(_resources.Asset.PathTracingSettings.LightNeeRisCandidates, 1, 64));

            cmd.SetGlobalInt(PipelineShaderProperties.SkyObjectLightingProperty, _resources.Asset.PathTracingSettings.IsSkyObjectLighting ? 1 : 0);

            bool isSurfelNeeEnabled = _resources.Asset.PathTracingSettings.IsEmissiveSurfelNee;

            cmd.SetGlobalInt(PipelineShaderProperties.EmissiveSurfelNeeEnabledProperty, isSurfelNeeEnabled ? 1 : 0);
            cmd.SetGlobalBuffer(PipelineShaderProperties.EmissiveSurfelsReadProperty, _resources.EmissiveSurfelBuffer);
            cmd.SetGlobalBuffer(PipelineShaderProperties.EmissiveSurfelCounterProperty, _resources.EmissiveSurfelCounter);
            cmd.SetGlobalInt(PipelineShaderProperties.EmissiveSurfelCapacityProperty, _resources.EmissiveSurfelBuffer.count);

            if (isSurfelNeeEnabled)
            {
                cmd.SetGlobalInt(
                    PipelineShaderProperties.EmissiveSurfelNeeMaxCandidatesProperty,
                    Mathf.Clamp(_resources.Asset.PathTracingSettings.EmissiveSurfelNeeMaxCandidates, 1, 256));

                cmd.SetGlobalFloat(PipelineShaderProperties.SurfelDedupCellSizeProperty, SurfelDedupCellSize);
                cmd.SetGlobalInt(PipelineShaderProperties.SurfelDedupResolutionProperty, SurfelDedupResolution);
            }

            cmd.SetGlobalFloat(PipelineShaderProperties.EnvironmentRotationYawCosProperty, 1f);
            cmd.SetGlobalFloat(PipelineShaderProperties.EnvironmentRotationYawSinProperty, 0f);
            cmd.SetGlobalFloat(PipelineShaderProperties.EnvironmentIntensityProperty, 1f);
        }

        private static Vector2 GetCameraJitterUv(Camera camera, Matrix4x4 jitteredViewProj, Matrix4x4 viewProj)
        {
            var transform = camera.transform;
            var point = transform.position + transform.forward * Mathf.Max(camera.nearClipPlane * 2f, 1f);
            var position = new Vector4(point.x, point.y, point.z, 1f);
            var jittered = jitteredViewProj * position;
            var unjittered = viewProj * position;

            return new Vector2(
                unjittered.x / unjittered.w - jittered.x / jittered.w,
                unjittered.y / unjittered.w - jittered.y / jittered.w) * .5f;
        }

        private static int GetRenderSettingsHash(LucentPathRendererSettings asset)
        {
            unchecked
            {
                var hash = HashHelper.CreateHash();
                var path = asset.PathTracingSettings;

                if (path != null)
                {
                    hash = HashHelper.AppendHash(hash, path.SamplesPerPixel);
                    hash = HashHelper.AppendHash(hash, path.SurfaceBounces);
                    hash = HashHelper.AppendHash(hash, path.ReflectionBounces);
                    hash = HashHelper.AppendHash(hash, path.TransmissionBounces);
                    hash = HashHelper.AppendHash(hash, path.RussianRouletteStart);
                    hash = HashHelper.AppendHash(hash, path.MaxLights);
                    hash = HashHelper.AppendHash(hash, path.MaxBounceDistance);
                    hash = HashHelper.AppendHash(hash, path.SurfaceBias);
                    hash = HashHelper.AppendHash(hash, path.DirectIntensity);
                    hash = HashHelper.AppendHash(hash, (int)path.DebugVisualizationMode);
                }

                var temporal = asset.TemporalHistorySettings;

                if (temporal != null)
                {
                    hash = HashHelper.AppendHash(hash, temporal.MaxHistoryLength);
                    hash = HashHelper.AppendHash(hash, temporal.LowConfidenceSpatialBlend);
                    hash = HashHelper.AppendHash(hash, temporal.GuideMinSignalWeight);
                    hash = HashHelper.AppendHash(hash, temporal.GuidePlanarNormalThreshold);
                    hash = HashHelper.AppendHash(hash, temporal.GuideMaxVirtualProjectionErrorPixels);
                    hash = HashHelper.AppendHash(hash, temporal.GuideMaxPlaneMotionMeters);
                }

                var rejection = asset.TemporalHistoryRejectionSettings;

                if (rejection != null)
                {
                    hash = HashHelper.AppendHash(hash, rejection.DepthRejectThreshold);
                    hash = HashHelper.AppendHash(hash, rejection.NormalRejectThreshold);
                    hash = HashHelper.AppendHash(hash, rejection.MotionRejectionStrength);
                    hash = HashHelper.AppendHash(hash, rejection.HistoryColorClipSigma);
                }

                var confidence = asset.TemporalHistoryConfidenceSettings;

                if (confidence != null)
                {
                    hash = HashHelper.AppendHash(hash, confidence.DepthConfidenceScale);
                    hash = HashHelper.AppendHash(hash, confidence.NormalConfidencePower);
                    hash = HashHelper.AppendHash(hash, confidence.ConfidencePower);
                    hash = HashHelper.AppendHash(hash, confidence.ConfidenceDitherStrength);
                }

                var recovery = asset.DisocclusionRecoverySettings;

                if (recovery != null)
                {
                    hash = HashHelper.AppendHash(hash, recovery.SpatialBootstrapRadius);
                    hash = HashHelper.AppendHash(hash, recovery.SpatialBootstrapDepthRejectThreshold);
                    hash = HashHelper.AppendHash(hash, recovery.SpatialBootstrapNormalThreshold);
                    hash = HashHelper.AppendHash(hash, recovery.SpatialBootstrapDepthPhi);
                    hash = HashHelper.AppendHash(hash, recovery.SpatialBootstrapNormalPhi);
                    hash = HashHelper.AppendHash(hash, recovery.HistoryMissSpatialBlend);
                    hash = HashHelper.AppendHash(hash, recovery.SpatialBootstrapLuminancePhi);
                    hash = HashHelper.AppendHash(hash, recovery.SpatialBootstrapLuminanceFloor);
                    hash = HashHelper.AppendHash(hash, recovery.SpatialBootstrapVarianceCutoff);
                    hash = HashHelper.AppendHash(hash, recovery.SpatialBootstrapVarianceMinBlend);
                    hash = HashHelper.AppendHash(hash, recovery.YoungHistoryFrames);
                    hash = HashHelper.AppendHash(hash, recovery.YoungHistoryRadius);
                    hash = HashHelper.AppendHash(hash, recovery.YoungHistoryBlend);
                    hash = HashHelper.AppendHash(hash, recovery.YoungHistoryVarianceBoost);
                }

                var atrous = asset.ATrousSettings;

                if (atrous != null)
                {
                    hash = HashHelper.AppendHash(hash, atrous.AtrousIterations);
                    hash = HashHelper.AppendHash(hash, atrous.AtrousDepthPhi);
                    hash = HashHelper.AppendHash(hash, atrous.AtrousNormalPhi);
                    hash = HashHelper.AppendHash(hash, atrous.ReflectionPrePassRadius);
                }

                var firefly = asset.FireflySuppressionSettings;

                if (firefly != null)
                {
                    hash = HashHelper.AppendHash(hash, firefly.IndirectClampMultiplierSurface);
                    hash = HashHelper.AppendHash(hash, firefly.IndirectClampMultiplierReflection);
                    hash = HashHelper.AppendHash(hash, firefly.IndirectClampMultiplierTransmission);
                }

                var subsurface = asset.SubsurfaceSettings;

                if (subsurface != null)
                {
                    hash = HashHelper.AppendHash(hash, subsurface.IsEnabled ? 1 : 0);
                    hash = HashHelper.AppendHash(hash, subsurface.IndirectIntensity);
                }

                return hash;
            }
        }

#if UNITY_EDITOR
        private static int GetEditorWarmupFrameCount(LucentPathRendererSettings asset)
        {
            return Mathf.Max(1, asset.TemporalHistorySettings != null ? asset.TemporalHistorySettings.MaxHistoryLength : 1);
        }

        private static bool IsEditModeGameCamera(Camera camera)
        {
            return camera.cameraType == CameraType.Game && UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode == false;
        }

        private static int GetEditorCameraHash(Camera camera, DlssRenderPlan dlssPlan, CameraLensState lensState)
        {
            unchecked
            {
                var hash = HashHelper.CreateHash();
                var transform = camera.transform;
                var position = transform.position;
                var rotation = transform.rotation;

                hash = HashHelper.AppendHash(hash, camera.pixelWidth);
                hash = HashHelper.AppendHash(hash, camera.pixelHeight);
                hash = HashHelper.AppendHash(hash, dlssPlan.InputWidth);
                hash = HashHelper.AppendHash(hash, dlssPlan.InputHeight);
                hash = HashHelper.AppendHash(hash, dlssPlan.OutputWidth);
                hash = HashHelper.AppendHash(hash, dlssPlan.OutputHeight);
                hash = HashHelper.AppendHash(hash, position.x);
                hash = HashHelper.AppendHash(hash, position.y);
                hash = HashHelper.AppendHash(hash, position.z);
                hash = HashHelper.AppendHash(hash, rotation.x);
                hash = HashHelper.AppendHash(hash, rotation.y);
                hash = HashHelper.AppendHash(hash, rotation.z);
                hash = HashHelper.AppendHash(hash, rotation.w);
                hash = HashHelper.AppendHash(hash, camera.fieldOfView);
                hash = HashHelper.AppendHash(hash, camera.orthographic ? 1 : 0);
                hash = HashHelper.AppendHash(hash, camera.orthographicSize);
                hash = HashHelper.AppendHash(hash, camera.nearClipPlane);
                hash = HashHelper.AppendHash(hash, camera.farClipPlane);
                hash = HashHelper.AppendHash(hash, camera.targetTexture != null ? camera.targetTexture.GetEntityId().GetHashCode() : 0);

                return lensState.AppendHash(hash);
            }
        }

        private static class EditorRenderInvalidation
        {
            public static int Version
            {
                get
                {
                    EnsureInitialized();

                    return _version;
                }
            }

            private static int _version;
            private static bool _isInitialized;

            private static void EnsureInitialized()
            {
                if (_isInitialized)
                {
                    return;
                }

                _isInitialized = true;

                UnityEditor.EditorApplication.hierarchyChanged += MarkDirty;
                UnityEditor.EditorApplication.projectChanged += MarkDirty;
                UnityEditor.SceneManagement.EditorSceneManager.sceneDirtied += _ => MarkDirty();
                UnityEditor.Undo.undoRedoPerformed += MarkDirty;
                UnityEditor.Undo.postprocessModifications += PostprocessModifications;
            }

            private static UnityEditor.UndoPropertyModification[] PostprocessModifications(UnityEditor.UndoPropertyModification[] modifications)
            {
                MarkDirty();

                return modifications;
            }

            private static void MarkDirty()
            {
                unchecked
                {
                    _version++;
                }
            }
        }

#endif

        private void CreateMaterials(LucentPathRendererSettings asset)
        {
            var defaultCompositeShader = Shader.Find("Hidden/LucentPath/Composite");
            var compositeShader = asset.CompositeShader != null ? asset.CompositeShader : defaultCompositeShader;

            if (compositeShader != null)
            {
                _resources.CompositeMaterial = CoreUtils.CreateEngineMaterial(compositeShader);
            }

            if (defaultCompositeShader != null)
            {
                if (defaultCompositeShader == compositeShader)
                {
                    _resources.CopyMaterial = _resources.CompositeMaterial;
                }
                else
                {
                    _resources.CopyMaterial = CoreUtils.CreateEngineMaterial(defaultCompositeShader);
                    _resources.IsCopyMaterialOwned = true;
                }
            }

            var postSharpenShader = asset.PostSharpenShader != null ? asset.PostSharpenShader : Shader.Find("Hidden/LucentPath/PostSharpen");

            if (postSharpenShader != null)
            {
                _resources.PostSharpenMaterial = CoreUtils.CreateEngineMaterial(postSharpenShader);
            }

            var bloomShader = asset.BloomShader != null ? asset.BloomShader : Shader.Find("Hidden/LucentPath/Bloom");

            if (bloomShader != null)
            {
                _resources.BloomMaterial = CoreUtils.CreateEngineMaterial(bloomShader);
            }
        }

        private static RayTracingInstanceCullingConfig CreateRtasCullingConfig()
        {
            var config = new RayTracingInstanceCullingConfig
            {
                flags = RayTracingInstanceCullingFlags.ComputeMaterialsCRC
            };

            config.subMeshFlagsConfig.opaqueMaterials = RayTracingSubMeshFlags.Enabled | RayTracingSubMeshFlags.ClosestHitOnly;
            config.subMeshFlagsConfig.transparentMaterials = RayTracingSubMeshFlags.Enabled | RayTracingSubMeshFlags.UniqueAnyHitCalls;
            config.subMeshFlagsConfig.alphaTestedMaterials = RayTracingSubMeshFlags.Enabled;

            config.triangleCullingConfig.checkDoubleSidedGIMaterial = false;
            config.triangleCullingConfig.frontTriangleCounterClockwise = false;
            config.triangleCullingConfig.optionalDoubleSidedShaderKeywords = new string[0];

            config.alphaTestedMaterialConfig.renderQueueLowerBound = 2450;
            config.alphaTestedMaterialConfig.renderQueueUpperBound = 2499;
            config.alphaTestedMaterialConfig.optionalShaderKeywords = new string[0];

            config.transparentMaterialConfig.renderQueueLowerBound = 2501;
            config.transparentMaterialConfig.renderQueueUpperBound = 3500;
            config.transparentMaterialConfig.optionalShaderKeywords = new string[0];

            config.materialTest.requiredShaderTags = new RayTracingInstanceCullingShaderTagConfig[0];
            config.materialTest.deniedShaderPasses = new string[0];

            var test = new RayTracingInstanceCullingTest
            {
                allowOpaqueMaterials = true,
                allowAlphaTestedMaterials = true,
                allowTransparentMaterials = true,
                layerMask = -1,
                shadowCastingModeMask = (1 << (int)ShadowCastingMode.Off) | (1 << (int)ShadowCastingMode.On) | (1 << (int)ShadowCastingMode.TwoSided),
                instanceMask = 0xFF
            };

            config.instanceTests = new[] { test };

            return config;
        }

        private void EnsureMaterialCache()
        {
            var capacity = Mathf.Max(1, _resources.Asset.PathTracingSettings.MaxInstanceCount);

            if (_resources.MaterialCache != null && _resources.MaterialCache.count == capacity)
            {
                return;
            }

            _resources.MaterialCache?.Release();

            var stride = System.Runtime.InteropServices.Marshal.SizeOf<MaterialCacheEntry>();

            _resources.MaterialCache = new ComputeBuffer(capacity, stride, ComputeBufferType.Structured);
        }

        private const int EmissiveSurfelCapacity = 262144;
        private const int SurfelDedupResolution = 65536;
        private const float SurfelDedupCellSize = .5f;
        private static readonly uint[] _emissiveSurfelCounterReset = { 0u };

        private uint[] _emissiveSurfelOccupancyReset;

        private uint[] EnsureOccupancyResetPattern()
        {
            if (_emissiveSurfelOccupancyReset == null || _emissiveSurfelOccupancyReset.Length != SurfelDedupResolution)
            {
                _emissiveSurfelOccupancyReset = new uint[SurfelDedupResolution];

                for (int i = 0; i < SurfelDedupResolution; i++)
                {
                    _emissiveSurfelOccupancyReset[i] = 0xFFFFFFFFu;
                }
            }

            return _emissiveSurfelOccupancyReset;
        }

        private void EnsureEmissiveSurfels()
        {
            if (_resources.EmissiveSurfelBuffer == null || _resources.EmissiveSurfelBuffer.count != EmissiveSurfelCapacity)
            {
                _resources.EmissiveSurfelBuffer?.Release();

                var stride = System.Runtime.InteropServices.Marshal.SizeOf<GpuEmissiveSurfel>();

                _resources.EmissiveSurfelBuffer = new ComputeBuffer(EmissiveSurfelCapacity, stride, ComputeBufferType.Structured);
            }

            if (_resources.EmissiveSurfelCounter == null)
            {
                _resources.EmissiveSurfelCounter = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Structured);

                _resources.EmissiveSurfelCounter.SetData(_emissiveSurfelCounterReset);
            }

            if (_resources.EmissiveSurfelOccupancy == null || _resources.EmissiveSurfelOccupancy.count != SurfelDedupResolution)
            {
                _resources.EmissiveSurfelOccupancy?.Release();

                _resources.EmissiveSurfelOccupancy = new ComputeBuffer(SurfelDedupResolution, sizeof(uint), ComputeBufferType.Structured);

                _resources.EmissiveSurfelOccupancy.SetData(EnsureOccupancyResetPattern());
            }
        }

        private void ResetEmissiveSurfels(CommandBuffer cmd)
        {
            cmd.SetBufferData(_resources.EmissiveSurfelCounter, _emissiveSurfelCounterReset);
            cmd.SetBufferData(_resources.EmissiveSurfelOccupancy, EnsureOccupancyResetPattern());
        }

        public void Dispose()
        {
            _pathTracingRayGen.Dispose();
            _sceneUploader.Dispose();
            _cameraResources.Dispose();

            if (_resources.CompositeMaterial != null)
            {
                CoreUtils.Destroy(_resources.CompositeMaterial);
            }

            if (_resources.IsCopyMaterialOwned && _resources.CopyMaterial != null)
            {
                CoreUtils.Destroy(_resources.CopyMaterial);
            }

            if (_resources.BloomMaterial != null)
            {
                CoreUtils.Destroy(_resources.BloomMaterial);
            }

            if (_resources.PostSharpenMaterial != null)
            {
                CoreUtils.Destroy(_resources.PostSharpenMaterial);
            }

            if (_resources.SobolMatricesBuffer != null)
            {
                _resources.SobolMatricesBuffer.Release();

                _resources.SobolMatricesBuffer = null;
            }

            if (_resources.MaterialCache != null)
            {
                _resources.MaterialCache.Release();

                _resources.MaterialCache = null;
            }

            if (_resources.EmissiveSurfelBuffer != null)
            {
                _resources.EmissiveSurfelBuffer.Release();

                _resources.EmissiveSurfelBuffer = null;
            }

            if (_resources.EmissiveSurfelCounter != null)
            {
                _resources.EmissiveSurfelCounter.Release();

                _resources.EmissiveSurfelCounter = null;
            }

            if (_resources.EmissiveSurfelOccupancy != null)
            {
                _resources.EmissiveSurfelOccupancy.Release();

                _resources.EmissiveSurfelOccupancy = null;
            }

            _materialTable.Dispose();

            if (_rtas != null)
            {
                _rtas.Release();

                _rtas = null;
            }

            _skyboxBaker.Dispose();
            _envImportance.Dispose();
            _distantLightIndices.Dispose();
            _msLut.Dispose();
        }
    }
}
