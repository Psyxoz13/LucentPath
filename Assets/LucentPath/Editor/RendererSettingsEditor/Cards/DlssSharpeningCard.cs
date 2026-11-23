using UnityEngine.UIElements;

namespace LucentPath.EditorUi.RendererSettings
{
    internal class DlssSharpeningCard : RendererSettingsEditorCardBase
    {
        public DlssSharpeningCard(LucentPathRendererSettingsEditor editor)
            : base(editor, "DLSS Sharpening", "Applies an optional sharpening override after DLSS to recover crispness.")
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

            AddToggleField(parent, "Enable Sharpening", () => settings.IsSharpeningEnabled, value => settings.IsSharpeningEnabled = value,
                "Applies a post-DLSS sharpening pass after upscaling.");

            RegisterConditionalElement(
                AddToggleField(
                    parent,
                    "Use NVIDIA Optimal Settings",
                    () => settings.IsNvidiaOptimalSharpnessPreferred,
                    value => settings.IsNvidiaOptimalSharpnessPreferred = value,
                    "Use NVIDIA's recommended sharpness value for the current DLSS quality mode."),
                () => settings.IsSharpeningEnabled && settings.IsNvidiaOptimalSharpnessAvailable);

            RegisterConditionalElement(
                AddFloatSliderField(parent, "Sharpness", () => settings.SharpnessOverride, value => settings.SharpnessOverride = value, .01f, 1f,
                "Manual post-DLSS sharpness used when NVIDIA optimal sharpness is off or unavailable."),
                () => settings.IsManualSharpnessUsed);
        }
    }
}
