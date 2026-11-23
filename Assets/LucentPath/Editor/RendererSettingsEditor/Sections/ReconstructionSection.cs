using LucentPath.EditorUi.Core;
using UnityEngine.UIElements;

namespace LucentPath.EditorUi.RendererSettings
{
    internal class ReconstructionSection : EditorSectionBase<RendererSettingsEditorContext>
    {
        public ReconstructionSection(LucentPathRendererSettingsEditor editor)
            : base(
                "Reconstruction",
                "Reduces shading cost by shading a fraction of pixels each frame and reconstructing the rest before denoising.",
                () => editor.IsReconstructionSectionExpanded,
                editor.SetReconstructionSectionVisibility,
                new EditorCardBase<RendererSettingsEditorContext>[]
                {
                    new CheckerboardCard(editor)
                })
        { }
    }

    internal class CheckerboardCard : RendererSettingsEditorCardBase
    {
        public CheckerboardCard(LucentPathRendererSettingsEditor editor)
            : base(
                editor,
                "Checkerboard Shading",
                "Shades a per-frame checkerboard half of the screen and reconstructs the off-half from current-frame neighbours before the denoiser. Halves primary-NEE and bounce-ray cost; primary geometry and guides stay full-res.")
        { }

        public override void BuildUiToolkit(RendererSettingsEditorContext context, VisualElement parent)
        {
            var settings = Asset.CheckerboardSettings;

            if (settings == null)
            {
                return;
            }

            AddToggleField(parent, "Enabled", () => settings.IsEnabled, value => settings.IsEnabled = value,
                "Shade only a per-frame checkerboard half of the screen; the off-half skips primary NEE and bounce rays and is reconstructed spatially from current-frame neighbours before temporal accumulation. Off = default.");

            RegisterConditionalElement(AddIntSliderField(parent, "Full-Rate History Frames",
                () => settings.FullRateHistoryFrames, value => settings.FullRateHistoryFrames = value, 0, 64,
                "Pixels whose accumulated history is shorter than this many samples are shaded every frame instead of every other frame, so disocclusions and camera cuts converge at full rate while converged areas keep the checkerboard saving. 0 = plain checkerboard."), () => settings.IsEnabled);

            RegisterConditionalElement(AddIntSliderField(parent, "Reconstruction Radius",
                () => settings.ReconstructionRadius, value => settings.ReconstructionRadius = value, 1, 3,
                "Neighbourhood radius (in pixels) the resolve pass gathers shaded on-half neighbours from when filling an off-half pixel. Larger = more candidate neighbours (more stable fill) at the cost of a wider gather."), () => settings.IsEnabled);

            RegisterConditionalElement(AddFloatSliderField(parent, "Neighbor Normal Threshold",
                () => settings.NeighborNormalThreshold, value => settings.NeighborNormalThreshold = value, 0f, 1f,
                "Minimum normal agreement a shaded neighbour must have with the reconstructed pixel to contribute. Higher = more edge-aware fill (rejects neighbours across normal discontinuities)."), () => settings.IsEnabled);

            RegisterConditionalElement(AddMinFloatField(parent, "Neighbor Depth Threshold",
                () => settings.NeighborDepthThreshold, value => settings.NeighborDepthThreshold = value, 1e-4f,
                "Maximum relative depth difference a shaded neighbour may have to contribute. Lower = more edge-aware fill (rejects neighbours across depth discontinuities)."), () => settings.IsEnabled);
        }
    }
}
