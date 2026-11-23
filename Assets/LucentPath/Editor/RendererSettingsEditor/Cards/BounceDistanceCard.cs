using UnityEngine.UIElements;

namespace LucentPath.EditorUi.RendererSettings
{
    internal class BounceDistanceCard : RendererSettingsEditorCardBase
    {
        public BounceDistanceCard(LucentPathRendererSettingsEditor editor)
            : base(editor, "Bounce Distance", "Caps how far a bounced ray may travel before it is discarded; limits reach, leaks, and cost.")
        { }

        public override void BuildUiToolkit(RendererSettingsEditorContext context, VisualElement parent)
        {
            var pathTracingSettings = Asset.PathTracingSettings;

            if (pathTracingSettings == null)
            {
                return;
            }

            RegisterUiToolkitRefresh(ClampBounceDistanceToCameraLimit);

            AddClampedFloatField(
                parent,
                "Max Bounce Distance",
                () => pathTracingSettings.MaxBounceDistance,
                value => pathTracingSettings.MaxBounceDistance = value,
                .001f,
                () => GetGameCameraFarDistance(float.PositiveInfinity),
                "Maximum travel distance allowed for secondary bounce rays.");

            AddBounceDistanceVisualizationElement(parent);
        }
    }
}
