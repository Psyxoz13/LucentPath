using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace LucentPath.Rendering
{
    internal class DXRCameraResourceCache : IDisposable
    {
        public const int RadianceCacheEntryStride = 66;

        private readonly DXRPipelineResources _context;

        private int _nextViewportId;

        private static readonly uint[] _zeroChunk = new uint[1 << 18];

        public DXRCameraResourceCache(DXRPipelineResources context)
        {
            _context = context;
        }

        public CameraState GetCameraState(Camera camera)
        {
            var id = camera.GetEntityId();

            if (_context.CameraStates.TryGetValue(id, out CameraState state))
            {
                return state;
            }

            state = new CameraState { ViewportId = _nextViewportId++ };

            _context.CameraStates.Add(id, state);

            return state;
        }

        public void EnsureTextures(CameraState state, int width, int height)
        {
            EnsureTextures(state, width, height, width, height, false);
        }

        public void EnsureTextures(
            CameraState state,
            int width,
            int height,
            int outputWidth,
            int outputHeight,
            bool allocateDlss)
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            outputWidth = Mathf.Max(1, outputWidth);
            outputHeight = Mathf.Max(1, outputHeight);

            var pixelCount = Mathf.Max(1, width * height);
            var pathStateStride = Marshal.SizeOf<GpuPathState>();
            var pathHitStride = Marshal.SizeOf<GpuPathHit>();
            var radianceCacheCount = ResolveRadianceCacheSlotCount() * RadianceCacheEntryStride;

            bool coreValid = AreCoreResourcesValid(state, width, height, pixelCount, pathStateStride, pathHitStride, radianceCacheCount);

            bool dlssValid = allocateDlss == false ||
                (IsRtHandleValid(state.PreparedDepth, width, height) &&
                IsRtHandleValid(state.PreparedMotionVectors, width, height) &&
                IsRtHandleValid(state.DlssTransparencyMask, width, height) &&
                IsRtHandleValid(state.DlssBiasColorMask, width, height) &&
                IsRtHandleValid(state.DlssOutput, outputWidth, outputHeight) &&
                state.OutputWidth == outputWidth &&
                state.OutputHeight == outputHeight);

            if (coreValid && dlssValid)
            {
                if (allocateDlss == false)
                {
                    ReleaseDlssTextures(state);
                }

                return;
            }

            ReleaseCoreResources(state);
            ReleaseDlssTextures(state);

            var lightingDesc = new RenderTextureDescriptor(width, height, GraphicsFormat.R16G16B16A16_SFloat, 0)
            {
                msaaSamples = 1,
                depthBufferBits = 0,
                enableRandomWrite = true,
                sRGB = false,
                useMipMap = false,
                autoGenerateMips = false
            };

            var momentsDesc = new RenderTextureDescriptor(width, height, GraphicsFormat.R16G16B16A16_SFloat, 0)
            {
                msaaSamples = 1,
                depthBufferBits = 0,
                enableRandomWrite = true,
                sRGB = false,
                useMipMap = false,
                autoGenerateMips = false
            };

            var guide0Desc = new RenderTextureDescriptor(width, height, GetGuide0Format(), 0)
            {
                msaaSamples = 1,
                depthBufferBits = 0,
                enableRandomWrite = true,
                sRGB = false,
                useMipMap = false,
                autoGenerateMips = false
            };

            var guide1Desc = new RenderTextureDescriptor(width, height, GetGuide0Format(), 0)
            {
                msaaSamples = 1,
                depthBufferBits = 0,
                enableRandomWrite = true,
                sRGB = false,
                useMipMap = false,
                autoGenerateMips = false
            };

            var guide2Desc = new RenderTextureDescriptor(width, height, GetGuide0Format(), 0)
            {
                msaaSamples = 1,
                depthBufferBits = 0,
                enableRandomWrite = true,
                sRGB = false,
                useMipMap = false,
                autoGenerateMips = false
            };

            var secondaryGuideEventDesc = new RenderTextureDescriptor(width, height, GetSecondaryGuideEventFormat(), 0)
            {
                msaaSamples = 1,
                depthBufferBits = 0,
                enableRandomWrite = true,
                sRGB = false,
                useMipMap = false,
                autoGenerateMips = false
            };

            var hitMaskDesc = new RenderTextureDescriptor(width, height, GetHitMaskFormat(), 0)
            {
                msaaSamples = 1,
                depthBufferBits = 0,
                enableRandomWrite = true,
                sRGB = false,
                useMipMap = false,
                autoGenerateMips = false
            };

            var hitDistanceDesc = new RenderTextureDescriptor(width, height, GetHitDistanceFormat(), 0)
            {
                msaaSamples = 1,
                depthBufferBits = 0,
                enableRandomWrite = true,
                sRGB = false,
                useMipMap = false,
                autoGenerateMips = false
            };

            var roughnessDesc = new RenderTextureDescriptor(width, height, GetRoughnessFormat(), 0)
            {
                msaaSamples = 1,
                depthBufferBits = 0,
                enableRandomWrite = true,
                sRGB = false,
                useMipMap = false,
                autoGenerateMips = false
            };

            state.Result = CreateCameraTexture(lightingDesc, $"DXR_Result_{width}x{height}", FilterMode.Bilinear);
            state.PrevResult = CreateCameraTexture(lightingDesc, $"DXR_ResultPrev_{width}x{height}", FilterMode.Bilinear);
            state.Moments = CreateCameraTexture(momentsDesc, $"DXR_Moments_{width}x{height}", FilterMode.Point);
            state.PrevMoments = CreateCameraTexture(momentsDesc, $"DXR_MomentsPrev_{width}x{height}", FilterMode.Point);
            state.SurfaceSignal.Moments = CreateCameraTexture(momentsDesc, $"DXR_SurfaceMoments_{width}x{height}", FilterMode.Point);
            state.SurfaceSignal.PrevMoments = CreateCameraTexture(momentsDesc, $"DXR_SurfaceMomentsPrev_{width}x{height}", FilterMode.Point);
            state.ReflectionSignal.Moments = CreateCameraTexture(momentsDesc, $"DXR_ReflectionMoments_{width}x{height}", FilterMode.Point);
            state.ReflectionSignal.PrevMoments = CreateCameraTexture(momentsDesc, $"DXR_ReflectionMomentsPrev_{width}x{height}", FilterMode.Point);
            state.TransmissionSignal.Moments = CreateCameraTexture(momentsDesc, $"DXR_TransmissionMoments_{width}x{height}", FilterMode.Point);

            state.TransmissionSignal.PrevMoments = CreateCameraTexture(
                momentsDesc,
                $"DXR_TransmissionMomentsPrev_{width}x{height}",
                FilterMode.Point);

            state.SurfaceSignal.Guide0 = CreateCameraTexture(guide0Desc, $"DXR_Guide0_{width}x{height}", FilterMode.Point);
            state.SurfaceSignal.PrevGuide0 = CreateCameraTexture(guide0Desc, $"DXR_Guide0Prev_{width}x{height}", FilterMode.Point);
            state.SurfaceSignal.Guide1 = CreateCameraTexture(guide1Desc, $"DXR_Guide1_{width}x{height}", FilterMode.Point);
            state.SurfaceSignal.PrevGuide1 = CreateCameraTexture(guide1Desc, $"DXR_Guide1Prev_{width}x{height}", FilterMode.Point);
            state.SurfaceSignal.Guide2 = CreateCameraTexture(guide2Desc, $"DXR_Guide2_{width}x{height}", FilterMode.Point);
            state.SurfaceSignal.PrevGuide2 = CreateCameraTexture(guide2Desc, $"DXR_Guide2Prev_{width}x{height}", FilterMode.Point);
            state.ReflectionSignal.Guide0 = CreateCameraTexture(guide0Desc, $"DXR_ReflectionGuide0_{width}x{height}", FilterMode.Point);
            state.ReflectionSignal.PrevGuide0 = CreateCameraTexture(guide0Desc, $"DXR_ReflectionGuide0Prev_{width}x{height}", FilterMode.Point);
            state.ReflectionSignal.Guide1 = CreateCameraTexture(guide1Desc, $"DXR_ReflectionGuide1_{width}x{height}", FilterMode.Point);
            state.ReflectionSignal.PrevGuide1 = CreateCameraTexture(guide1Desc, $"DXR_ReflectionGuide1Prev_{width}x{height}", FilterMode.Point);
            state.ReflectionSignal.Guide2 = CreateCameraTexture(guide2Desc, $"DXR_ReflectionGuide2_{width}x{height}", FilterMode.Point);
            state.ReflectionSignal.PrevGuide2 = CreateCameraTexture(guide2Desc, $"DXR_ReflectionGuide2Prev_{width}x{height}", FilterMode.Point);
            state.TransmissionSignal.Guide0 = CreateCameraTexture(guide0Desc, $"DXR_TransmissionGuide0_{width}x{height}", FilterMode.Point);
            state.TransmissionSignal.PrevGuide0 = CreateCameraTexture(guide0Desc, $"DXR_TransmissionGuide0Prev_{width}x{height}", FilterMode.Point);
            state.TransmissionSignal.Guide1 = CreateCameraTexture(guide1Desc, $"DXR_TransmissionGuide1_{width}x{height}", FilterMode.Point);
            state.TransmissionSignal.PrevGuide1 = CreateCameraTexture(guide1Desc, $"DXR_TransmissionGuide1Prev_{width}x{height}", FilterMode.Point);
            state.TransmissionSignal.Guide2 = CreateCameraTexture(guide2Desc, $"DXR_TransmissionGuide2_{width}x{height}", FilterMode.Point);
            state.TransmissionSignal.PrevGuide2 = CreateCameraTexture(guide2Desc, $"DXR_TransmissionGuide2Prev_{width}x{height}", FilterMode.Point);
            state.ShadowSignal.Guide0 = CreateCameraTexture(guide0Desc, $"DXR_ShadowGuide0_{width}x{height}", FilterMode.Point);
            state.ShadowSignal.PrevGuide0 = CreateCameraTexture(guide0Desc, $"DXR_ShadowGuide0Prev_{width}x{height}", FilterMode.Point);
            state.ShadowSignal.Guide1 = CreateCameraTexture(guide1Desc, $"DXR_ShadowGuide1_{width}x{height}", FilterMode.Point);
            state.ShadowSignal.PrevGuide1 = CreateCameraTexture(guide1Desc, $"DXR_ShadowGuide1Prev_{width}x{height}", FilterMode.Point);
            state.ShadowSignal.Guide2 = CreateCameraTexture(guide2Desc, $"DXR_ShadowGuide2_{width}x{height}", FilterMode.Point);
            state.ShadowSignal.PrevGuide2 = CreateCameraTexture(guide2Desc, $"DXR_ShadowGuide2Prev_{width}x{height}", FilterMode.Point);

            state.ReflectionGuideEvent.Allocate(secondaryGuideEventDesc, "DXR_ReflectionGuide", width, height);
            state.TransmissionGuideEvent.Allocate(secondaryGuideEventDesc, "DXR_TransmissionGuide", width, height);

            state.FilterPing = CreateCameraTexture(lightingDesc, $"DXR_FilterPing_{width}x{height}", FilterMode.Bilinear);
            state.FilterPong = CreateCameraTexture(lightingDesc, $"DXR_FilterPong_{width}x{height}", FilterMode.Bilinear);
            state.SurfaceSignal.Signal = CreateCameraTexture(lightingDesc, $"DXR_SurfaceSignal_{width}x{height}", FilterMode.Bilinear);
            state.SurfaceSignal.PrevSignal = CreateCameraTexture(lightingDesc, $"DXR_SurfaceSignalPrev_{width}x{height}", FilterMode.Bilinear);
            state.ReflectionSignal.Signal = CreateCameraTexture(lightingDesc, $"DXR_ReflectionSignal_{width}x{height}", FilterMode.Bilinear);
            state.ReflectionSignal.PrevSignal = CreateCameraTexture(lightingDesc, $"DXR_ReflectionSignalPrev_{width}x{height}", FilterMode.Bilinear);
            state.TransmissionSignal.Signal = CreateCameraTexture(lightingDesc, $"DXR_TransmissionSignal_{width}x{height}", FilterMode.Bilinear);

            state.TransmissionSignal.PrevSignal = CreateCameraTexture(
                lightingDesc,
                $"DXR_TransmissionSignalPrev_{width}x{height}",
                FilterMode.Bilinear);

            state.SurfaceSignal.DenoisedSignal = CreateCameraTexture(
                lightingDesc,
                $"DXR_DenoisedSurfaceSignal_{width}x{height}",
                FilterMode.Bilinear);

            state.ReflectionSignal.DenoisedSignal = CreateCameraTexture(
                lightingDesc,
                $"DXR_DenoisedReflectionSignal_{width}x{height}",
                FilterMode.Bilinear);

            state.TransmissionSignal.DenoisedSignal = CreateCameraTexture(
                lightingDesc,
                $"DXR_DenoisedTransmissionSignal_{width}x{height}",
                FilterMode.Bilinear);

            state.SurfaceSignal.HistoryFixedSignal = CreateCameraTexture(
                lightingDesc,
                $"DXR_HistoryFixedSurfaceSignal_{width}x{height}",
                FilterMode.Bilinear);

            state.ReflectionSignal.HistoryFixedSignal = CreateCameraTexture(
                lightingDesc,
                $"DXR_HistoryFixedReflectionSignal_{width}x{height}",
                FilterMode.Bilinear);

            state.TransmissionSignal.HistoryFixedSignal = CreateCameraTexture(
                lightingDesc,
                $"DXR_HistoryFixedTransmissionSignal_{width}x{height}",
                FilterMode.Bilinear);

            state.SurfaceSignal.YoungHistorySignal = CreateCameraTexture(
                lightingDesc,
                $"DXR_SurfaceYoungHistorySignal_{width}x{height}",
                FilterMode.Point);

            state.ReflectionSignal.YoungHistorySignal = CreateCameraTexture(
                lightingDesc,
                $"DXR_ReflectionYoungHistorySignal_{width}x{height}",
                FilterMode.Point);

            state.TransmissionSignal.YoungHistorySignal = CreateCameraTexture(
                lightingDesc,
                $"DXR_TransmissionYoungHistorySignal_{width}x{height}",
                FilterMode.Point);

            state.DirectLighting = CreateCameraTexture(lightingDesc, $"DXR_Direct_{width}x{height}", FilterMode.Bilinear);
            state.IndirectLighting = CreateCameraTexture(lightingDesc, $"DXR_Indirect_{width}x{height}", FilterMode.Bilinear);
            state.FrameSurfaceSignalSum = CreateCameraTexture(lightingDesc, $"DXR_FrameSurfaceSignal_{width}x{height}", FilterMode.Point);
            state.FrameReflectionSignalSum = CreateCameraTexture(lightingDesc, $"DXR_FrameReflectionSignal_{width}x{height}", FilterMode.Point);
            state.ReflectionPrePassSignal = CreateCameraTexture(lightingDesc, $"DXR_ReflectionPrePassSignal_{width}x{height}", FilterMode.Point);
            state.FrameTransmissionSignalSum = CreateCameraTexture(lightingDesc, $"DXR_FrameTransmissionSignal_{width}x{height}", FilterMode.Point);
            state.FrameEmissionSum = CreateCameraTexture(lightingDesc, $"DXR_FrameEmission_{width}x{height}", FilterMode.Point);
            state.PrimaryDiffuseAlbedo = CreateCameraTexture(lightingDesc, $"DXR_PrimaryDiffuseAlbedo_{width}x{height}", FilterMode.Point);
            state.PrimarySpecularF0 = CreateCameraTexture(lightingDesc, $"DXR_PrimarySpecularF0_{width}x{height}", FilterMode.Point);
            state.PrimaryTransmissionTint = CreateCameraTexture(lightingDesc, $"DXR_PrimaryTransmissionTint_{width}x{height}", FilterMode.Point);
            state.HitMask = CreateCameraTexture(hitMaskDesc, $"DXR_HitMask_{width}x{height}", FilterMode.Point);
            state.PrevHitMask = CreateCameraTexture(hitMaskDesc, $"DXR_HitMaskPrev_{width}x{height}", FilterMode.Point);
            state.ShadowHitMask = CreateCameraTexture(hitMaskDesc, $"DXR_ShadowHitMask_{width}x{height}", FilterMode.Point);
            state.PrevShadowHitMask = CreateCameraTexture(hitMaskDesc, $"DXR_ShadowHitMaskPrev_{width}x{height}", FilterMode.Point);
            state.FrameShadowSum = CreateCameraTexture(lightingDesc, $"DXR_FrameShadowSum_{width}x{height}", FilterMode.Point);
            state.ReflectionShadowHitMask = CreateCameraTexture(hitMaskDesc, $"DXR_ReflectionShadowHitMask_{width}x{height}", FilterMode.Point);

            state.PrevReflectionShadowHitMask = CreateCameraTexture(
                hitMaskDesc,
                $"DXR_ReflectionShadowHitMaskPrev_{width}x{height}",
                FilterMode.Point);

            state.TransmissionShadowHitMask = CreateCameraTexture(hitMaskDesc, $"DXR_TransmissionShadowHitMask_{width}x{height}", FilterMode.Point);

            state.PrevTransmissionShadowHitMask = CreateCameraTexture(
                hitMaskDesc,
                $"DXR_TransmissionShadowHitMaskPrev_{width}x{height}",
                FilterMode.Point);

            state.PrimaryHitDistance = CreateCameraTexture(hitDistanceDesc, $"DXR_PrimaryHitDist_{width}x{height}", FilterMode.Point);
            state.PrevPrimaryHitDistance = CreateCameraTexture(hitDistanceDesc, $"DXR_PrimaryHitDistPrev_{width}x{height}", FilterMode.Point);
            state.ReflectionHitDistance = CreateCameraTexture(hitDistanceDesc, $"DXR_ReflectionHitDist_{width}x{height}", FilterMode.Point);
            state.PrevReflectionHitDistance = CreateCameraTexture(hitDistanceDesc, $"DXR_ReflectionHitDistPrev_{width}x{height}", FilterMode.Point);
            state.TransmissionHitDistance = CreateCameraTexture(hitDistanceDesc, $"DXR_TransmissionHitDist_{width}x{height}", FilterMode.Point);

            state.PrevTransmissionHitDistance = CreateCameraTexture(
                hitDistanceDesc,
                $"DXR_TransmissionHitDistPrev_{width}x{height}",
                FilterMode.Point);

            state.PrimaryRoughness = CreateCameraTexture(roughnessDesc, $"DXR_PrimaryRoughness_{width}x{height}", FilterMode.Point);
            state.PrevPrimaryRoughness = CreateCameraTexture(roughnessDesc, $"DXR_PrimaryRoughnessPrev_{width}x{height}", FilterMode.Point);

            if (allocateDlss)
            {
                var preparedDepthDesc = new RenderTextureDescriptor(width, height, GetPreparedDepthFormat(), 0)
                {
                    msaaSamples = 1,
                    depthBufferBits = 0,
                    enableRandomWrite = true,
                    sRGB = false,
                    useMipMap = false,
                    autoGenerateMips = false
                };

                var preparedMotionDesc = new RenderTextureDescriptor(width, height, GetPreparedMotionVectorFormat(), 0)
                {
                    msaaSamples = 1,
                    depthBufferBits = 0,
                    enableRandomWrite = true,
                    sRGB = false,
                    useMipMap = false,
                    autoGenerateMips = false
                };

                var dlssMaskDesc = new RenderTextureDescriptor(width, height, GetHitMaskFormat(), 0)
                {
                    msaaSamples = 1,
                    depthBufferBits = 0,
                    enableRandomWrite = true,
                    sRGB = false,
                    useMipMap = false,
                    autoGenerateMips = false
                };

                var dlssOutputDesc = new RenderTextureDescriptor(outputWidth, outputHeight, GraphicsFormat.R16G16B16A16_SFloat, 0)
                {
                    msaaSamples = 1,
                    depthBufferBits = 0,
                    enableRandomWrite = true,
                    sRGB = false,
                    useMipMap = false,
                    autoGenerateMips = false
                };

                state.PreparedDepth = CreateCameraTexture(preparedDepthDesc, $"DXR_PreparedDepth_{width}x{height}", FilterMode.Point);

                state.PreparedMotionVectors = CreateCameraTexture(
                    preparedMotionDesc,
                    $"DXR_PreparedMotionVectors_{width}x{height}",
                    FilterMode.Point);

                state.DlssTransparencyMask = CreateCameraTexture(dlssMaskDesc, $"DXR_DLSSTransparencyMask_{width}x{height}", FilterMode.Point);
                state.DlssBiasColorMask = CreateCameraTexture(dlssMaskDesc, $"DXR_DLSSBiasColorMask_{width}x{height}", FilterMode.Point);
                state.DlssOutput = CreateCameraTexture(dlssOutputDesc, $"DXR_DLSSOutput_{outputWidth}x{outputHeight}", FilterMode.Point);
            }

            state.PathStateBuffer = new ComputeBuffer(pixelCount, pathStateStride, ComputeBufferType.Structured);
            state.PathHitBuffer = new ComputeBuffer(pixelCount, pathHitStride, ComputeBufferType.Structured);
            state.PathDistanceBuffer = new ComputeBuffer(pixelCount, sizeof(float), ComputeBufferType.Structured);
            state.HitGBuffer = new ComputeBuffer(pixelCount, Marshal.SizeOf<GpuStoredHit>(), ComputeBufferType.Structured);
            state.PathFlagsBuffer = new ComputeBuffer(pixelCount, sizeof(uint), ComputeBufferType.Structured);
            state.ActivePathIndicesBuffer = new ComputeBuffer(pixelCount, sizeof(uint), ComputeBufferType.Structured);
            state.SimplePathIndicesBuffer = new ComputeBuffer(pixelCount, sizeof(uint), ComputeBufferType.Structured);
            state.ComplexPathIndicesBuffer = new ComputeBuffer(pixelCount, sizeof(uint), ComputeBufferType.Structured);
            state.BootstrapActiveIndicesBuffer = new ComputeBuffer(pixelCount, sizeof(uint), ComputeBufferType.Structured);
            state.DeltaReflectionPathIndicesBuffer = new ComputeBuffer(pixelCount, sizeof(uint), ComputeBufferType.Structured);
            state.RadianceCacheBuffer = new ComputeBuffer(radianceCacheCount, sizeof(uint), ComputeBufferType.Structured);

            ClearBuffer(state.RadianceCacheBuffer, radianceCacheCount);

            state.DispatchArgsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Structured,
                16,
                sizeof(uint));

            state.DeltaReflectionArgsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Structured,
                3,
                sizeof(uint));

            state.Width = width;
            state.Height = height;
            state.OutputWidth = outputWidth;
            state.OutputHeight = outputHeight;
            state.SampleCount = 0;
            state.IsValid = false;
            state.LastViewProj = Matrix4x4.identity;
            state.LastViewMatrix = Matrix4x4.identity;
            state.LastCameraWorldPos = Vector3.zero;
            state.LastOrthographic = 0;
            state.IsLastDlssActive = allocateDlss;
            state.LastDlssQuality = _context.Asset.DlssSettings != null ? (int)_context.Asset.DlssSettings.Quality : 0;
            state.LastDlssSettingsHash = _context.Asset.DlssSettings != null ? _context.Asset.DlssSettings.GetRuntimeConfigurationHash() : 0;
            state.IsLastPresentationUsedDlss = false;
            state.LastRenderSettingsHash = 0;
#if UNITY_EDITOR
            state.LastEditorCameraHash = 0;
            state.LastEditorInvalidationVersion = 0;
#endif
        }

        public void SwapHistoryBuffers(CameraState state)
        {
            Swap(ref state.Moments, ref state.PrevMoments);
            Swap(ref state.SurfaceSignal.Moments, ref state.SurfaceSignal.PrevMoments);
            Swap(ref state.ReflectionSignal.Moments, ref state.ReflectionSignal.PrevMoments);
            Swap(ref state.TransmissionSignal.Moments, ref state.TransmissionSignal.PrevMoments);
            Swap(ref state.SurfaceSignal.Guide0, ref state.SurfaceSignal.PrevGuide0);
            Swap(ref state.SurfaceSignal.Guide1, ref state.SurfaceSignal.PrevGuide1);
            Swap(ref state.SurfaceSignal.Guide2, ref state.SurfaceSignal.PrevGuide2);
            Swap(ref state.ReflectionSignal.Guide0, ref state.ReflectionSignal.PrevGuide0);
            Swap(ref state.ReflectionSignal.Guide1, ref state.ReflectionSignal.PrevGuide1);
            Swap(ref state.ReflectionSignal.Guide2, ref state.ReflectionSignal.PrevGuide2);
            Swap(ref state.TransmissionSignal.Guide0, ref state.TransmissionSignal.PrevGuide0);
            Swap(ref state.TransmissionSignal.Guide1, ref state.TransmissionSignal.PrevGuide1);
            Swap(ref state.TransmissionSignal.Guide2, ref state.TransmissionSignal.PrevGuide2);
            Swap(ref state.ShadowSignal.Guide0, ref state.ShadowSignal.PrevGuide0);
            Swap(ref state.ShadowSignal.Guide1, ref state.ShadowSignal.PrevGuide1);
            Swap(ref state.ShadowSignal.Guide2, ref state.ShadowSignal.PrevGuide2);
            Swap(ref state.SurfaceSignal.Signal, ref state.SurfaceSignal.PrevSignal);
            Swap(ref state.ReflectionSignal.Signal, ref state.ReflectionSignal.PrevSignal);
            Swap(ref state.TransmissionSignal.Signal, ref state.TransmissionSignal.PrevSignal);
            Swap(ref state.HitMask, ref state.PrevHitMask);
            Swap(ref state.ShadowHitMask, ref state.PrevShadowHitMask);
            Swap(ref state.ReflectionShadowHitMask, ref state.PrevReflectionShadowHitMask);
            Swap(ref state.TransmissionShadowHitMask, ref state.PrevTransmissionShadowHitMask);
            Swap(ref state.PrimaryHitDistance, ref state.PrevPrimaryHitDistance);
            Swap(ref state.ReflectionHitDistance, ref state.PrevReflectionHitDistance);
            Swap(ref state.TransmissionHitDistance, ref state.PrevTransmissionHitDistance);
            Swap(ref state.PrimaryRoughness, ref state.PrevPrimaryRoughness);
            state.ReflectionGuideEvent.SwapHistory();
            state.TransmissionGuideEvent.SwapHistory();
        }

        public void SwapResultWithHistory(CameraState state)
        {
            Swap(ref state.Result, ref state.PrevResult);
        }

        public static void ReleaseRtHandle(ref RTHandle handle)
        {
            if (handle == null)
            {
                return;
            }

            handle.Release();

            handle = null;
        }

        public void ReleaseDlssTextures(CameraState state)
        {
            ReleaseRtHandle(ref state.PreparedDepth);
            ReleaseRtHandle(ref state.PreparedMotionVectors);
            ReleaseRtHandle(ref state.DlssTransparencyMask);
            ReleaseRtHandle(ref state.DlssBiasColorMask);
            ReleaseRtHandle(ref state.DlssOutput);
        }

        public void Dispose()
        {
            foreach (var cameraStatePair in _context.CameraStates)
            {
                ReleaseCoreResources(cameraStatePair.Value);

                ReleaseRtHandle(ref cameraStatePair.Value.PreparedDepth);
                ReleaseRtHandle(ref cameraStatePair.Value.PreparedMotionVectors);
                ReleaseDlssTextures(cameraStatePair.Value);
                cameraStatePair.Value.DlssUpscaler.Dispose();
                ReleaseBuffer(ref cameraStatePair.Value.PathStateBuffer);
                ReleaseBuffer(ref cameraStatePair.Value.PathHitBuffer);
                ReleaseBuffer(ref cameraStatePair.Value.PathDistanceBuffer);
                ReleaseBuffer(ref cameraStatePair.Value.HitGBuffer);
                ReleaseBuffer(ref cameraStatePair.Value.PathFlagsBuffer);
                ReleaseBuffer(ref cameraStatePair.Value.ActivePathIndicesBuffer);
                ReleaseBuffer(ref cameraStatePair.Value.SimplePathIndicesBuffer);
                ReleaseBuffer(ref cameraStatePair.Value.ComplexPathIndicesBuffer);
                ReleaseBuffer(ref cameraStatePair.Value.BootstrapActiveIndicesBuffer);
                ReleaseBuffer(ref cameraStatePair.Value.DeltaReflectionPathIndicesBuffer);
                ReleaseBuffer(ref cameraStatePair.Value.RadianceCacheBuffer);

                if (cameraStatePair.Value.DispatchArgsBuffer != null)
                {
                    cameraStatePair.Value.DispatchArgsBuffer.Release();

                    cameraStatePair.Value.DispatchArgsBuffer = null;
                }

                if (cameraStatePair.Value.DeltaReflectionArgsBuffer != null)
                {
                    cameraStatePair.Value.DeltaReflectionArgsBuffer.Release();

                    cameraStatePair.Value.DeltaReflectionArgsBuffer = null;
                }
            }

            _context.CameraStates.Clear();
        }

        private int ResolveRadianceCacheSlotCount()
        {
            var settings = _context.Asset != null ? _context.Asset.RadianceCacheSettings : null;

            if (settings == null || settings.IsEnabled == false)
            {
                return 1;
            }

            return 1 << Mathf.Clamp(settings.SlotCountExponent, 16, 23);
        }

        private static void ClearBuffer(ComputeBuffer buffer, int count)
        {
            for (var offset = 0; offset < count; offset += _zeroChunk.Length)
            {
                buffer.SetData(_zeroChunk, 0, offset, Mathf.Min(_zeroChunk.Length, count - offset));
            }
        }

        private static void Swap(ref RTHandle a, ref RTHandle b)
        {
            (a, b) = (b, a);
        }

        private static GraphicsFormat GetPreparedDepthFormat()
        {
            if (SystemInfo.IsFormatSupported(GraphicsFormat.R32_SFloat, GraphicsFormatUsage.LoadStore))
            {
                return GraphicsFormat.R32_SFloat;
            }

            if (SystemInfo.IsFormatSupported(GraphicsFormat.R16_SFloat, GraphicsFormatUsage.LoadStore))
            {
                return GraphicsFormat.R16_SFloat;
            }

            return GraphicsFormat.R32_SFloat;
        }

        private static GraphicsFormat GetGuide0Format()
        {
            if (SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat, GraphicsFormatUsage.LoadStore))
            {
                return GraphicsFormat.R16G16B16A16_SFloat;
            }

            return GraphicsFormat.R32G32B32A32_SFloat;
        }

        private static GraphicsFormat GetSecondaryGuideEventFormat()
        {
            return GraphicsFormat.R32G32B32A32_UInt;
        }

        private static GraphicsFormat GetPreparedMotionVectorFormat()
        {
            if (SystemInfo.IsFormatSupported(GraphicsFormat.R16G16_SFloat, GraphicsFormatUsage.LoadStore))
            {
                return GraphicsFormat.R16G16_SFloat;
            }

            if (SystemInfo.IsFormatSupported(GraphicsFormat.R32G32_SFloat, GraphicsFormatUsage.LoadStore))
            {
                return GraphicsFormat.R32G32_SFloat;
            }

            return GraphicsFormat.R16G16_SFloat;
        }

        private static GraphicsFormat GetHitDistanceFormat()
        {
            if (SystemInfo.IsFormatSupported(GraphicsFormat.R16_SFloat, GraphicsFormatUsage.LoadStore))
            {
                return GraphicsFormat.R16_SFloat;
            }

            if (SystemInfo.IsFormatSupported(GraphicsFormat.R32_SFloat, GraphicsFormatUsage.LoadStore))
            {
                return GraphicsFormat.R32_SFloat;
            }

            return GraphicsFormat.R16_SFloat;
        }

        private static GraphicsFormat GetRoughnessFormat()
        {
            if (SystemInfo.IsFormatSupported(GraphicsFormat.R8_UNorm, GraphicsFormatUsage.LoadStore))
            {
                return GraphicsFormat.R8_UNorm;
            }

            if (SystemInfo.IsFormatSupported(GraphicsFormat.R16_SFloat, GraphicsFormatUsage.LoadStore))
            {
                return GraphicsFormat.R16_SFloat;
            }

            return GraphicsFormat.R8_UNorm;
        }

        private static RTHandle CreateCameraTexture(RenderTextureDescriptor desc, string name, FilterMode filterMode)
        {
            return RTHandles.Alloc(desc, filterMode, TextureWrapMode.Clamp, name: name);
        }

        private static bool IsRtHandleValid(RTHandle handle, int width, int height, GraphicsFormat format)
        {
            return IsRtHandleValid(handle, width, height) &&
                handle.rt.graphicsFormat == format;
        }

        private static bool IsRtHandleValid(RTHandle handle, int width, int height)
        {
            return handle != null &&
                   handle.rt != null &&
                   handle.rt.width == width &&
                   handle.rt.height == height;
        }

        private static void ReleaseBuffer(ref ComputeBuffer buffer)
        {
            if (buffer == null)
            {
                return;
            }

            buffer.Release();

            buffer = null;
        }

        private static bool AreCoreResourcesValid(CameraState state, int width, int height, int pixelCount, int pathStateStride, int pathHitStride, int radianceCacheCount)
        {
            var guide0Format = GetGuide0Format();

            return state != null &&
                   IsRtHandleValid(state.Result, width, height) &&
                   IsRtHandleValid(state.PrevResult, width, height) &&
                   IsRtHandleValid(state.Moments, width, height) &&
                   IsRtHandleValid(state.PrevMoments, width, height) &&
                   IsRtHandleValid(state.SurfaceSignal.Moments, width, height) &&
                   IsRtHandleValid(state.SurfaceSignal.PrevMoments, width, height) &&
                   IsRtHandleValid(state.ReflectionSignal.Moments, width, height) &&
                   IsRtHandleValid(state.ReflectionSignal.PrevMoments, width, height) &&
                   IsRtHandleValid(state.TransmissionSignal.Moments, width, height) &&
                   IsRtHandleValid(state.TransmissionSignal.PrevMoments, width, height) &&
                   IsRtHandleValid(state.SurfaceSignal.Guide0, width, height, guide0Format) &&
                   IsRtHandleValid(state.SurfaceSignal.PrevGuide0, width, height, guide0Format) &&
                   IsRtHandleValid(state.ReflectionSignal.Guide0, width, height, guide0Format) &&
                   IsRtHandleValid(state.ReflectionSignal.PrevGuide0, width, height, guide0Format) &&
                   IsRtHandleValid(state.TransmissionSignal.Guide0, width, height, guide0Format) &&
                   IsRtHandleValid(state.TransmissionSignal.PrevGuide0, width, height, guide0Format) &&
                   IsRtHandleValid(state.SurfaceSignal.Guide1, width, height) &&
                   IsRtHandleValid(state.SurfaceSignal.PrevGuide1, width, height) &&
                   IsRtHandleValid(state.SurfaceSignal.Guide2, width, height) &&
                   IsRtHandleValid(state.SurfaceSignal.PrevGuide2, width, height) &&
                   IsRtHandleValid(state.ReflectionSignal.Guide1, width, height) &&
                   IsRtHandleValid(state.ReflectionSignal.PrevGuide1, width, height) &&
                   IsRtHandleValid(state.ReflectionSignal.Guide2, width, height) &&
                   IsRtHandleValid(state.ReflectionSignal.PrevGuide2, width, height) &&
                   IsRtHandleValid(state.TransmissionSignal.Guide1, width, height) &&
                   IsRtHandleValid(state.TransmissionSignal.PrevGuide1, width, height) &&
                   IsRtHandleValid(state.TransmissionSignal.Guide2, width, height) &&
                   IsRtHandleValid(state.TransmissionSignal.PrevGuide2, width, height) &&
                   IsRtHandleValid(state.ShadowSignal.Guide0, width, height, guide0Format) &&
                   IsRtHandleValid(state.ShadowSignal.PrevGuide0, width, height, guide0Format) &&
                   IsRtHandleValid(state.ShadowSignal.Guide1, width, height) &&
                   IsRtHandleValid(state.ShadowSignal.PrevGuide1, width, height) &&
                   IsRtHandleValid(state.ShadowSignal.Guide2, width, height) &&
                   IsRtHandleValid(state.ShadowSignal.PrevGuide2, width, height) &&
                   state.ReflectionGuideEvent.IsValid(width, height, GetSecondaryGuideEventFormat()) &&
                   state.TransmissionGuideEvent.IsValid(width, height, GetSecondaryGuideEventFormat()) &&
                   IsRtHandleValid(state.FilterPing, width, height) &&
                   IsRtHandleValid(state.FilterPong, width, height) &&
                   IsRtHandleValid(state.SurfaceSignal.Signal, width, height) &&
                   IsRtHandleValid(state.SurfaceSignal.PrevSignal, width, height) &&
                   IsRtHandleValid(state.ReflectionSignal.Signal, width, height) &&
                   IsRtHandleValid(state.ReflectionSignal.PrevSignal, width, height) &&
                   IsRtHandleValid(state.TransmissionSignal.Signal, width, height) &&
                   IsRtHandleValid(state.TransmissionSignal.PrevSignal, width, height) &&
                   IsRtHandleValid(state.DirectLighting, width, height) &&
                   IsRtHandleValid(state.IndirectLighting, width, height) &&
                   IsRtHandleValid(state.FrameSurfaceSignalSum, width, height) &&
                   IsRtHandleValid(state.FrameReflectionSignalSum, width, height) &&
                   IsRtHandleValid(state.ReflectionPrePassSignal, width, height) &&
                   IsRtHandleValid(state.FrameTransmissionSignalSum, width, height) &&
                   IsRtHandleValid(state.FrameEmissionSum, width, height) &&
                   IsRtHandleValid(state.PrimaryDiffuseAlbedo, width, height) &&
                   IsRtHandleValid(state.PrimarySpecularF0, width, height) &&
                   IsRtHandleValid(state.PrimaryTransmissionTint, width, height) &&
                   IsRtHandleValid(state.HitMask, width, height) &&
                   IsRtHandleValid(state.PrevHitMask, width, height) &&
                   IsRtHandleValid(state.ShadowHitMask, width, height) &&
                   IsRtHandleValid(state.PrevShadowHitMask, width, height) &&
                   IsRtHandleValid(state.FrameShadowSum, width, height) &&
                   IsRtHandleValid(state.ReflectionShadowHitMask, width, height) &&
                   IsRtHandleValid(state.PrevReflectionShadowHitMask, width, height) &&
                   IsRtHandleValid(state.TransmissionShadowHitMask, width, height) &&
                   IsRtHandleValid(state.PrevTransmissionShadowHitMask, width, height) &&
                   IsRtHandleValid(state.SurfaceSignal.DenoisedSignal, width, height) &&
                   IsRtHandleValid(state.ReflectionSignal.DenoisedSignal, width, height) &&
                   IsRtHandleValid(state.TransmissionSignal.DenoisedSignal, width, height) &&
                   IsRtHandleValid(state.SurfaceSignal.HistoryFixedSignal, width, height) &&
                   IsRtHandleValid(state.ReflectionSignal.HistoryFixedSignal, width, height) &&
                   IsRtHandleValid(state.TransmissionSignal.HistoryFixedSignal, width, height) &&
                   IsRtHandleValid(state.SurfaceSignal.YoungHistorySignal, width, height) &&
                   IsRtHandleValid(state.ReflectionSignal.YoungHistorySignal, width, height) &&
                   IsRtHandleValid(state.TransmissionSignal.YoungHistorySignal, width, height) &&
                   IsRtHandleValid(state.PrimaryHitDistance, width, height) &&
                   IsRtHandleValid(state.PrevPrimaryHitDistance, width, height) &&
                   IsRtHandleValid(state.ReflectionHitDistance, width, height) &&
                   IsRtHandleValid(state.PrevReflectionHitDistance, width, height) &&
                   IsRtHandleValid(state.TransmissionHitDistance, width, height) &&
                   IsRtHandleValid(state.PrevTransmissionHitDistance, width, height) &&
                   IsRtHandleValid(state.PrimaryRoughness, width, height) &&
                   IsRtHandleValid(state.PrevPrimaryRoughness, width, height) &&
                   state.PathStateBuffer != null &&
                   state.PathStateBuffer.count == pixelCount &&
                   state.PathStateBuffer.stride == pathStateStride &&
                   state.PathHitBuffer != null &&
                   state.PathHitBuffer.count == pixelCount &&
                   state.PathHitBuffer.stride == pathHitStride &&
                   state.PathDistanceBuffer != null &&
                   state.PathDistanceBuffer.count == pixelCount &&
                   state.HitGBuffer != null &&
                   state.HitGBuffer.count == pixelCount &&
                   state.PathFlagsBuffer != null &&
                   state.PathFlagsBuffer.count == pixelCount &&
                   state.ActivePathIndicesBuffer != null &&
                   state.ActivePathIndicesBuffer.count == pixelCount &&
                   state.SimplePathIndicesBuffer != null &&
                   state.SimplePathIndicesBuffer.count == pixelCount &&
                   state.ComplexPathIndicesBuffer != null &&
                   state.ComplexPathIndicesBuffer.count == pixelCount &&
                   state.BootstrapActiveIndicesBuffer != null &&
                   state.BootstrapActiveIndicesBuffer.count == pixelCount &&
                   state.DeltaReflectionPathIndicesBuffer != null &&
                   state.DeltaReflectionPathIndicesBuffer.count == pixelCount &&
                   state.DeltaReflectionArgsBuffer != null &&
                   state.RadianceCacheBuffer != null &&
                   state.RadianceCacheBuffer.count == radianceCacheCount &&
                   state.DispatchArgsBuffer != null &&
                   state.Width == width &&
                   state.Height == height;
        }

        private static void ReleaseCoreResources(CameraState state)
        {
            if (state == null)
            {
                return;
            }

            ReleaseRtHandle(ref state.Result);
            ReleaseRtHandle(ref state.PrevResult);
            ReleaseRtHandle(ref state.Moments);
            ReleaseRtHandle(ref state.PrevMoments);
            ReleaseRtHandle(ref state.SurfaceSignal.Moments);
            ReleaseRtHandle(ref state.SurfaceSignal.PrevMoments);
            ReleaseRtHandle(ref state.ReflectionSignal.Moments);
            ReleaseRtHandle(ref state.ReflectionSignal.PrevMoments);
            ReleaseRtHandle(ref state.TransmissionSignal.Moments);
            ReleaseRtHandle(ref state.TransmissionSignal.PrevMoments);
            ReleaseRtHandle(ref state.SurfaceSignal.Guide0);
            ReleaseRtHandle(ref state.SurfaceSignal.PrevGuide0);
            ReleaseRtHandle(ref state.SurfaceSignal.Guide1);
            ReleaseRtHandle(ref state.SurfaceSignal.PrevGuide1);
            ReleaseRtHandle(ref state.SurfaceSignal.Guide2);
            ReleaseRtHandle(ref state.SurfaceSignal.PrevGuide2);
            ReleaseRtHandle(ref state.ReflectionSignal.Guide0);
            ReleaseRtHandle(ref state.ReflectionSignal.PrevGuide0);
            ReleaseRtHandle(ref state.ReflectionSignal.Guide1);
            ReleaseRtHandle(ref state.ReflectionSignal.PrevGuide1);
            ReleaseRtHandle(ref state.ReflectionSignal.Guide2);
            ReleaseRtHandle(ref state.ReflectionSignal.PrevGuide2);
            ReleaseRtHandle(ref state.TransmissionSignal.Guide0);
            ReleaseRtHandle(ref state.TransmissionSignal.PrevGuide0);
            ReleaseRtHandle(ref state.TransmissionSignal.Guide1);
            ReleaseRtHandle(ref state.TransmissionSignal.PrevGuide1);
            ReleaseRtHandle(ref state.TransmissionSignal.Guide2);
            ReleaseRtHandle(ref state.TransmissionSignal.PrevGuide2);
            ReleaseRtHandle(ref state.ShadowSignal.Guide0);
            ReleaseRtHandle(ref state.ShadowSignal.PrevGuide0);
            ReleaseRtHandle(ref state.ShadowSignal.Guide1);
            ReleaseRtHandle(ref state.ShadowSignal.PrevGuide1);
            ReleaseRtHandle(ref state.ShadowSignal.Guide2);
            ReleaseRtHandle(ref state.ShadowSignal.PrevGuide2);
            state.ReflectionGuideEvent.Release();
            state.TransmissionGuideEvent.Release();
            ReleaseRtHandle(ref state.FilterPing);
            ReleaseRtHandle(ref state.FilterPong);
            ReleaseRtHandle(ref state.SurfaceSignal.Signal);
            ReleaseRtHandle(ref state.SurfaceSignal.PrevSignal);
            ReleaseRtHandle(ref state.ReflectionSignal.Signal);
            ReleaseRtHandle(ref state.ReflectionSignal.PrevSignal);
            ReleaseRtHandle(ref state.TransmissionSignal.Signal);
            ReleaseRtHandle(ref state.TransmissionSignal.PrevSignal);
            ReleaseRtHandle(ref state.DirectLighting);
            ReleaseRtHandle(ref state.IndirectLighting);
            ReleaseRtHandle(ref state.FrameSurfaceSignalSum);
            ReleaseRtHandle(ref state.FrameReflectionSignalSum);
            ReleaseRtHandle(ref state.ReflectionPrePassSignal);
            ReleaseRtHandle(ref state.FrameTransmissionSignalSum);
            ReleaseRtHandle(ref state.FrameEmissionSum);
            ReleaseRtHandle(ref state.PrimaryDiffuseAlbedo);
            ReleaseRtHandle(ref state.PrimarySpecularF0);
            ReleaseRtHandle(ref state.PrimaryTransmissionTint);
            ReleaseRtHandle(ref state.HitMask);
            ReleaseRtHandle(ref state.PrevHitMask);
            ReleaseRtHandle(ref state.ShadowHitMask);
            ReleaseRtHandle(ref state.PrevShadowHitMask);
            ReleaseRtHandle(ref state.FrameShadowSum);
            ReleaseRtHandle(ref state.ReflectionShadowHitMask);
            ReleaseRtHandle(ref state.PrevReflectionShadowHitMask);
            ReleaseRtHandle(ref state.TransmissionShadowHitMask);
            ReleaseRtHandle(ref state.PrevTransmissionShadowHitMask);
            ReleaseRtHandle(ref state.SurfaceSignal.DenoisedSignal);
            ReleaseRtHandle(ref state.ReflectionSignal.DenoisedSignal);
            ReleaseRtHandle(ref state.TransmissionSignal.DenoisedSignal);
            ReleaseRtHandle(ref state.SurfaceSignal.HistoryFixedSignal);
            ReleaseRtHandle(ref state.ReflectionSignal.HistoryFixedSignal);
            ReleaseRtHandle(ref state.TransmissionSignal.HistoryFixedSignal);
            ReleaseRtHandle(ref state.SurfaceSignal.YoungHistorySignal);
            ReleaseRtHandle(ref state.ReflectionSignal.YoungHistorySignal);
            ReleaseRtHandle(ref state.TransmissionSignal.YoungHistorySignal);
            ReleaseRtHandle(ref state.PrimaryHitDistance);
            ReleaseRtHandle(ref state.PrevPrimaryHitDistance);
            ReleaseRtHandle(ref state.ReflectionHitDistance);
            ReleaseRtHandle(ref state.PrevReflectionHitDistance);
            ReleaseRtHandle(ref state.TransmissionHitDistance);
            ReleaseRtHandle(ref state.PrevTransmissionHitDistance);
            ReleaseRtHandle(ref state.PrimaryRoughness);
            ReleaseRtHandle(ref state.PrevPrimaryRoughness);
            ReleaseBuffer(ref state.PathStateBuffer);
            ReleaseBuffer(ref state.PathHitBuffer);
            ReleaseBuffer(ref state.PathDistanceBuffer);
            ReleaseBuffer(ref state.HitGBuffer);
            ReleaseBuffer(ref state.PathFlagsBuffer);
            ReleaseBuffer(ref state.ActivePathIndicesBuffer);
            ReleaseBuffer(ref state.SimplePathIndicesBuffer);
            ReleaseBuffer(ref state.ComplexPathIndicesBuffer);
            ReleaseBuffer(ref state.BootstrapActiveIndicesBuffer);
            ReleaseBuffer(ref state.DeltaReflectionPathIndicesBuffer);
            ReleaseBuffer(ref state.RadianceCacheBuffer);

            if (state.DispatchArgsBuffer != null)
            {
                state.DispatchArgsBuffer.Release();

                state.DispatchArgsBuffer = null;
            }

            if (state.DeltaReflectionArgsBuffer != null)
            {
                state.DeltaReflectionArgsBuffer.Release();

                state.DeltaReflectionArgsBuffer = null;
            }
        }

        private static GraphicsFormat GetHitMaskFormat()
        {
            return SystemInfo.IsFormatSupported(GraphicsFormat.R8_UNorm, GraphicsFormatUsage.LoadStore)
                ? GraphicsFormat.R8_UNorm
                : GraphicsFormat.R16_SFloat;
        }
    }
}
