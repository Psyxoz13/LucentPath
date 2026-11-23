using UnityEngine.UIElements;

namespace LucentPath.EditorUi.RendererSettings
{
    internal class DlssResolutionCard : RendererSettingsEditorCardBase
    {
        public DlssResolutionCard(LucentPathRendererSettingsEditor editor)
            : base(editor, "DLSS Resolution", "Chooses the internal render size fed into DLSS, or the fallback scale when DLSS is off.")
        { }

        public override bool ShouldDraw(RendererSettingsEditorContext context)
        {
            return IsDlssRuntimeActive(context.Asset?.DlssSettings);
        }

        public override void BuildUiToolkit(RendererSettingsEditorContext context, VisualElement parent)
        {
            var settings = Asset?.DlssSettings;

            if (settings == null)
            {
                return;
            }

            RegisterConditionalElement(
                AddToggleField(
                    parent,
                    "Use NVIDIA Optimal Settings",
                    () => settings.IsNvidiaOptimalSettingsPreferred,
                    value => settings.IsNvidiaOptimalSettingsPreferred = value,
                    "Use NVIDIA-recommended internal resolution for the selected DLSS quality mode."),
                () => settings.IsDlaaMode == false);

            RegisterConditionalElement(
                AddFloatSliderField(
                    parent,
                    "Fallback Render Scale",
                    () => settings.FallbackRenderScale,
                    value => settings.FallbackRenderScale = value,
                    .3333333f,
                    1f,
                    "Manual internal render scale used when NVIDIA optimal settings are off."),
                () => settings.IsFallbackRenderScaleUsed);

            AddMinIntField(parent, "Minimum Output Width", () => settings.MinimumOutputWidth, value => settings.MinimumOutputWidth = value, 1,
                "Smallest output width allowed before DLSS falls back to native rendering.");

            AddMinIntField(parent, "Minimum Output Height", () => settings.MinimumOutputHeight, value => settings.MinimumOutputHeight = value, 1,
                "Smallest output height allowed before DLSS falls back to native rendering.");

            AddToggleField(parent, "Enable DLSS Jitter", () => settings.IsDlssJitterEnabled, value => settings.IsDlssJitterEnabled = value,
                "Applies the Halton camera jitter expected by temporal DLSS reconstruction.");
        }
    }
}
