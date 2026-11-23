using LucentPath.Upscale.DLSS;
using UnityEngine;

namespace LucentPath.Rendering
{
    internal class DXRDlssPass
    {
        private readonly DXRPipelineResources _context;

        public DXRDlssPass(DXRPipelineResources context)
        {
            _context = context;
        }

        public DlssRenderPlan ResolveRenderPlan(Camera camera, int outputWidth, int outputHeight)
        {
            var settings = _context.Asset.DlssSettings;

            if (settings == null || settings.IsRuntimeActive == false)
            {
                return DlssRenderPlan.Native(outputWidth, outputHeight);
            }

            settings.Validate();

            int settingsHash = settings.GetRuntimeConfigurationHash();

            if (_context.LastDlssRuntimeConfigurationHash != settingsHash)
            {
                _context.LastDlssRuntimeConfigurationHash = settingsHash;
                _context.IsDlssRuntimeFailed = false;
                _context.LastDlssWarning = null;
            }

            if (_context.IsDlssRuntimeFailed && settings.IsFallbackToNativeWhenUnavailable)
            {
                return DlssRenderPlan.Native(outputWidth, outputHeight);
            }

            if (settings.IsDisableWhenDebugViewIsEnabled && _context.Asset.PathTracingSettings.IsDebugVisualizationAvailable)
            {
                return DlssRenderPlan.Native(outputWidth, outputHeight);
            }

            if (camera.cameraType != CameraType.Game && settings.IsSceneViewAllowed == false)
            {
                return DlssRenderPlan.Native(outputWidth, outputHeight);
            }

            if (outputWidth < settings.MinimumOutputWidth || outputHeight < settings.MinimumOutputHeight)
            {
                return DlssRenderPlan.Native(outputWidth, outputHeight);
            }

            if (DlssBackend.TryInitialize(settings, out string initError) == false)
            {
                WarnOnce(initError);

                if (settings.IsFallbackToNativeWhenUnavailable)
                {
                    _context.IsDlssRuntimeFailed = true;
                }

                return DlssRenderPlan.Native(outputWidth, outputHeight);
            }

            int inputWidth = 0;
            int inputHeight = 0;

            if (settings.IsDlaaMode)
            {
                inputWidth = outputWidth;
                inputHeight = outputHeight;
            }
            else if (settings.IsNvidiaOptimalSettingsUsed && DlssBackend.TryGetOptimalSettings(
                outputWidth,
                outputHeight,
                settings.Quality,
                out DlssOptimalSettings optimal) && optimal.IsValid)
            {
                inputWidth = optimal.RenderWidth;
                inputHeight = optimal.RenderHeight;
            }
            else
            {
                inputWidth = Mathf.CeilToInt(outputWidth * settings.FallbackRenderScale);
                inputHeight = Mathf.CeilToInt(outputHeight * settings.FallbackRenderScale);
            }

            inputWidth = Mathf.Clamp(inputWidth, 1, outputWidth);
            inputHeight = Mathf.Clamp(inputHeight, 1, outputHeight);

            return new DlssRenderPlan(true, inputWidth, inputHeight, outputWidth, outputHeight, settings.Quality, settingsHash);
        }

        public void WarnOnce(string message)
        {
            if (string.IsNullOrEmpty(message) || string.Equals(_context.LastDlssWarning, message, System.StringComparison.Ordinal))
            {
                return;
            }

            _context.LastDlssWarning = message;

            Debug.LogWarning($"LucentPath DLSS: {message}");
        }
    }
}
