using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LucentPath.Rendering
{
    public class LucentPathRendererFeature : ScriptableRendererFeature
    {
        public LucentPathRendererSettings Settings;

        private LucentPathRenderer _renderer;
        private LucentPathRendererSettings _subscribedSettings;
        private LucentPathRenderPass _renderPass;
        private bool _isUnsupportedLogged;

        public override void Create()
        {
            DisposeRenderer();
            SubscribeToSettings(Settings);

            _renderPass ??= new LucentPathRenderPass();
            _renderPass.RestorePendingSkyboxes();

            RenderPipelineManager.beginContextRendering -= OnRenderPipelineManagerBeginContextRendering;
            RenderPipelineManager.endContextRendering -= OnRenderPipelineManagerEndContextRendering;
            RenderPipelineManager.endCameraRendering -= OnRenderPipelineManagerEndCameraRendering;
            RenderPipelineManager.beginContextRendering += OnRenderPipelineManagerBeginContextRendering;
            RenderPipelineManager.endContextRendering += OnRenderPipelineManagerEndContextRendering;
            RenderPipelineManager.endCameraRendering += OnRenderPipelineManagerEndCameraRendering;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var cameraType = renderingData.cameraData.cameraType;

            if (cameraType != CameraType.Game && cameraType != CameraType.SceneView)
            {
                return;
            }

            if (TryGetRenderer(out LucentPathRenderer lucentPathRenderer) == false)
            {
                return;
            }

            _renderPass.SetRenderer(lucentPathRenderer);
            renderer.EnqueuePass(_renderPass);

            renderingData.cameraData.requiresDepthTexture = false;
            renderingData.cameraData.requiresOpaqueTexture = false;
            if (renderingData.cameraData.renderType == CameraRenderType.Base)
            {
                _renderPass.SuppressCameraSkybox(renderingData.cameraData.camera);
            }
        }

        protected override void Dispose(bool disposing)
        {
            RenderPipelineManager.beginContextRendering -= OnRenderPipelineManagerBeginContextRendering;
            RenderPipelineManager.endContextRendering -= OnRenderPipelineManagerEndContextRendering;
            RenderPipelineManager.endCameraRendering -= OnRenderPipelineManagerEndCameraRendering;

            _renderPass?.RestorePendingSkyboxes();
            SubscribeToSettings(null);
            DisposeRenderer();
        }

        private bool TryGetRenderer(out LucentPathRenderer renderer)
        {
            renderer = _renderer;

            if (renderer != null)
            {
                return true;
            }

            if (Settings == null)
            {
                return false;
            }

            if (LucentPathRenderer.IsSupported == false)
            {
                LogUnsupportedOnce();

                return false;
            }

            _renderer = new LucentPathRenderer(Settings);
            renderer = _renderer;

            return true;
        }

        private void LogUnsupportedOnce()
        {
            if (_isUnsupportedLogged)
            {
                return;
            }

            _isUnsupportedLogged = true;

            Debug.LogError("LucentPath requires compute shaders, hardware ray tracing and inline ray tracing support.", this);
        }

        private void SubscribeToSettings(LucentPathRendererSettings settings)
        {
            if (_subscribedSettings != null)
            {
                _subscribedSettings.OnValidated -= OnSettingsValidated;
            }

            _subscribedSettings = settings;

            if (_subscribedSettings != null)
            {
                _subscribedSettings.OnValidated += OnSettingsValidated;
            }
        }

        private void DisposeRenderer()
        {
            if (_renderer == null)
            {
                return;
            }

            _renderer.Dispose();

            _renderer = null;
        }

        private void OnSettingsValidated()
        {
            DisposeRenderer();
        }

        private void OnRenderPipelineManagerBeginContextRendering(ScriptableRenderContext context, List<Camera> cameras)
        {
            _renderer?.BeginFrame();
        }

        private void OnRenderPipelineManagerEndContextRendering(ScriptableRenderContext context, List<Camera> cameras)
        {
            _renderer?.EndFrame();
        }

        private void OnRenderPipelineManagerEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            _renderPass?.RestoreCameraSkybox(camera);
        }
    }
}
