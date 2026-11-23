using UnityEngine.UIElements;

namespace LucentPath.EditorUi.RendererSettings
{
    internal class SurfaceBiasCard : RendererSettingsEditorCardBase
    {
        public SurfaceBiasCard(LucentPathRendererSettingsEditor editor)
            : base(
                editor,
                "Surface Bias",
                "Offsets new rays off the surface to prevent self-hits; raise it only when acne or self-intersections appear.")
        { }

        public override void BuildUiToolkit(RendererSettingsEditorContext context, VisualElement parent)
        {
            var pathTracingSettings = Asset.PathTracingSettings;

            if (pathTracingSettings == null)
            {
                return;
            }

            AddLiveMinFloatField(
                parent,
                "Surface Bias",
                () => pathTracingSettings.SurfaceBias,
                value => pathTracingSettings.SurfaceBias = value,
                .001f,
                "Offset applied to new rays to avoid self-intersections.");

            AddVisualizationElement(parent, () => DrawBiasVisualization(pathTracingSettings.SurfaceBias));
        }
    }
}
