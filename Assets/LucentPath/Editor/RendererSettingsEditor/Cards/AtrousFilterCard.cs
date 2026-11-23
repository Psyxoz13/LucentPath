using UnityEngine.UIElements;

namespace LucentPath.EditorUi.RendererSettings
{
    internal class AtrousFilterCard : RendererSettingsEditorCardBase
    {
        public AtrousFilterCard(LucentPathRendererSettingsEditor editor)
            : base(editor, "A-Trous Filter", "Runs edge-aware spatial cleanup after accumulation; more passes clean more noise but blur more detail.")
        { }

        public override void BuildUiToolkit(RendererSettingsEditorContext context, VisualElement parent)
        {
            var aTrousSettings = Asset.ATrousSettings;

            if (aTrousSettings == null)
            {
                return;
            }

            AddIntSliderField(
                parent,
                "Surface Iterations",
                () => aTrousSettings.SurfaceAtrousIterations,
                value => aTrousSettings.SurfaceAtrousIterations = value,
                1,
                5,
                "Number of A-Trous denoiser passes for the surface (diffuse) signal.");

            AddIntSliderField(
                parent,
                "Reflection Iterations",
                () => aTrousSettings.ReflectionAtrousIterations,
                value => aTrousSettings.ReflectionAtrousIterations = value,
                1,
                5,
                "Number of A-Trous denoiser passes for the reflection signal. Glossy reflections often need fewer.");

            AddIntSliderField(
                parent,
                "Transmission Iterations",
                () => aTrousSettings.TransmissionAtrousIterations,
                value => aTrousSettings.TransmissionAtrousIterations = value,
                1,
                5,
                "Number of A-Trous denoiser passes for the transmission (glass) signal.");

            AddFloatSliderField(
                parent,
                "Reflection Pre-Pass Radius",
                () => aTrousSettings.ReflectionPrePassRadius,
                value => aTrousSettings.ReflectionPrePassRadius = value,
                0f,
                100f,
                "Max radius in pixels of the in-lobe reflection blur applied before temporal accumulation (NRD REBLUR pre-pass). 0 disables it.");

            AddLiveMinFloatField(
                parent,
                "A-Trous Depth Phi",
                () => aTrousSettings.AtrousDepthPhi,
                value =>
            {
                aTrousSettings.AtrousDepthPhi = value;

                RepaintAtrousVisualization();
            },
                .1f,
                "Depth sensitivity for the A-Trous filter.");

            AddLiveMinFloatField(
                parent,
                "A-Trous Normal Phi",
                () => aTrousSettings.AtrousNormalPhi,
                value =>
            {
                aTrousSettings.AtrousNormalPhi = value;

                RepaintAtrousVisualization();
            },
                1f,
                "Normal sensitivity for the A-Trous filter.");

            AddAtrousVisualizationElement(parent);
        }
    }
}
