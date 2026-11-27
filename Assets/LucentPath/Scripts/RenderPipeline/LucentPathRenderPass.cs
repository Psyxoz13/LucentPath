using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace LucentPath.Rendering
{
    public class LucentPathRenderPass : ScriptableRenderPass
    {
        private readonly Dictionary<EntityId, Camera> _suppressedSkyboxes = new();
        private LucentPathRenderer _renderer;

        private class PassData
        {
            public LucentPathRenderer Renderer;
            public UniversalCameraData CameraData;
            public CullingResults CullingResults;
            public TextureHandle Target;
        }

        public LucentPathRenderPass()
        {
            profilingSampler = new ProfilingSampler("LucentPath");
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            requiresIntermediateTexture = true;
        }

        public void SetRenderer(LucentPathRenderer renderer)
        {
            _renderer = renderer;
        }

        internal void SuppressCameraSkybox(Camera camera)
        {
            if (camera.clearFlags == CameraClearFlags.Skybox)
            {
                _suppressedSkyboxes[camera.GetEntityId()] = camera;
                camera.clearFlags = CameraClearFlags.SolidColor;
            }
        }

        internal void RestoreCameraSkybox(Camera camera)
        {
            if (_suppressedSkyboxes.Count != 0 && camera != null && _suppressedSkyboxes.Remove(camera.GetEntityId()))
            {
                RestoreSkyboxClearFlags(camera);
            }
        }

        internal void RestorePendingSkyboxes()
        {
            foreach (Camera camera in _suppressedSkyboxes.Values)
            {
                RestoreSkyboxClearFlags(camera);
            }
            _suppressedSkyboxes.Clear();
        }

        private static void RestoreSkyboxClearFlags(Camera camera)
        {
            if (camera != null && camera.clearFlags == CameraClearFlags.SolidColor)
            {
                camera.clearFlags = CameraClearFlags.Skybox;
            }
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            var renderingData = frameData.Get<UniversalRenderingData>();
            RestoreCameraSkybox(cameraData.camera);

            using (var builder = renderGraph.AddUnsafePass<PassData>(passName, out PassData passData, profilingSampler))
            {
                passData.Renderer = _renderer;
                passData.CameraData = cameraData;
                passData.CullingResults = renderingData.cullResults;
                passData.Target = resourceData.activeColorTexture;

                builder.UseTexture(passData.Target, AccessFlags.WriteAll);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) => ExecutePass(data, context));
            }
        }

        private static void ExecutePass(PassData data, UnsafeGraphContext context)
        {
            RTHandle target = data.Target;
            var descriptor = data.CameraData.cameraTargetDescriptor;

            data.Renderer.RenderCamera(
                CommandBufferHelpers.GetNativeCommandBuffer(context.cmd),
                data.CameraData.camera,
                data.CullingResults,
                target,
                data.CameraData.IsHandleYFlipped(target),
                descriptor.width,
                descriptor.height);
        }
    }
}
