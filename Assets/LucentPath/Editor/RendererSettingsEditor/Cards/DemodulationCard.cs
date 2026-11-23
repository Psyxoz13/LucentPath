using UnityEngine.UIElements;

namespace LucentPath.EditorUi.RendererSettings
{
    internal class DebugVisualizationCard : RendererSettingsEditorCardBase
    {
        public DebugVisualizationCard(LucentPathRendererSettingsEditor editor)
            : base(
                editor,
                "Debug Visualization",
                "Displays isolated lighting signals, demodulation factors, and primary material inputs in the final output.")
        { }

        public override void BuildUiToolkit(RendererSettingsEditorContext context, VisualElement parent)
        {
            var pathTracingSettings = Asset.PathTracingSettings;

            if (pathTracingSettings == null)
            {
                return;
            }

            AddEnumField(parent, "Debug Visualization",
                () => pathTracingSettings.DebugVisualizationMode,
                value => pathTracingSettings.DebugVisualizationMode = value,
                "Displays isolated signals, demodulation factors, primary material inputs, or per-signal relative-variance / firefly heatmaps directly in the final output. Variance modes (blue=clean, red=high relative variance) localize which signal and which pixels carry firefly noise. RawSurfaceFrame shows the current-frame surface lighting before temporal accumulation and denoising (re-modulated to radiance) so per-frame firefly spikes are visible directly. DLSS is automatically bypassed while debug visualization is active.");
        }
    }
}
