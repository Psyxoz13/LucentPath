using LucentPath.Upscale.DLSS;
using UnityEngine;

namespace LucentPath.Rendering
{
    internal readonly struct DlssRenderPlan
    {
        public readonly bool IsIntegration;
        public readonly int InputWidth;
        public readonly int InputHeight;
        public readonly int OutputWidth;
        public readonly int OutputHeight;
        public readonly DlssUpscaleMode Quality;
        public readonly int SettingsHash;

        public DlssRenderPlan(bool integration, int inputWidth, int inputHeight, int outputWidth, int outputHeight, DlssUpscaleMode quality, int settingsHash)
        {
            IsIntegration = integration;
            InputWidth = Mathf.Max(1, inputWidth);
            InputHeight = Mathf.Max(1, inputHeight);
            OutputWidth = Mathf.Max(1, outputWidth);
            OutputHeight = Mathf.Max(1, outputHeight);
            Quality = quality;
            SettingsHash = settingsHash;
        }

        public static DlssRenderPlan Native(int width, int height)
        {
            return new DlssRenderPlan(false, width, height, width, height, DlssUpscaleMode.Off, 0);
        }
    }
}
