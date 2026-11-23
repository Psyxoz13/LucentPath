using UnityEngine.UIElements;

namespace LucentPath.EditorUi.RendererSettings
{
    internal class TaaCard : RendererSettingsEditorCardBase
    {
        public TaaCard(LucentPathRendererSettingsEditor editor)
            : base(
                editor,
                "Temporal Anti-Aliasing",
                "Final output TAA applied when DLSS is inactive. Reduces sub-pixel jitter flicker on object edges via motion-vector reprojection with neighborhood clamping.")
        { }

        public override void BuildUiToolkit(RendererSettingsEditorContext context, VisualElement parent)
        {
            var taaSettings = Asset.TaaSettings;

            if (taaSettings == null)
            {
                return;
            }

            AddToggleField(parent, "Enabled",
                () => taaSettings.IsEnabled, value => taaSettings.IsEnabled = value,
                "Enable final output TAA when DLSS is not active.");

            AddFloatSliderField(parent, "History Feedback",
                () => taaSettings.Feedback, value => taaSettings.Feedback = value, 0f, .99f,
                "Blend weight applied to reprojected history. Higher = more stable, more lag. Typical 0.85-0.95.");
        }
    }
}
