using UnityEngine.UIElements;

namespace LucentPath.EditorUi.RendererSettings
{
    internal class HistoryConfidenceCard : RendererSettingsEditorCardBase
    {
        public HistoryConfidenceCard(LucentPathRendererSettingsEditor editor)
            : base(editor, "History Confidence", "Weights history by depth, normals, and warmup so unstable pixels rely less on old frames.")
        { }

        public override void BuildUiToolkit(RendererSettingsEditorContext context, VisualElement parent)
        {
            var temporalHistoryConfidenceSettings = Asset.TemporalHistoryConfidenceSettings;

            if (temporalHistoryConfidenceSettings == null)
            {
                return;
            }

            AddMinFloatField(parent, "Depth Confidence Scale",
                () => temporalHistoryConfidenceSettings.DepthConfidenceScale, value => temporalHistoryConfidenceSettings.DepthConfidenceScale = value, .25f,
                "Scale applied to depth agreement when estimating history confidence.");

            AddMinFloatField(parent, "Normal Confidence Power",
                () => temporalHistoryConfidenceSettings.NormalConfidencePower, value => temporalHistoryConfidenceSettings.NormalConfidencePower = value, .25f,
                "Exponent applied to normal agreement when estimating history confidence.");

            AddMinFloatField(parent, "History Confidence Power",
                () => temporalHistoryConfidenceSettings.ConfidencePower, value => temporalHistoryConfidenceSettings.ConfidencePower = value, .01f,
                "Power curve applied to the combined confidence before it caps the effective history length.");

            AddFloatSliderField(parent, "History Confidence Dither",
                () => temporalHistoryConfidenceSettings.ConfidenceDitherStrength, value => temporalHistoryConfidenceSettings.ConfidenceDitherStrength = value, 0f, 1f,
                "Small NRD-style dither used to reduce visible quantization bands in effective history length.");
        }
    }
}
