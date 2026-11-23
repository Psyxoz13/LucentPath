using UnityEngine;
using UnityEngine.UIElements;

namespace LucentPath.EditorUi.RendererSettings
{
    internal class SamplingCard : RendererSettingsEditorCardBase
    {
        public SamplingCard(LucentPathRendererSettingsEditor editor)
            : base(
                editor,
                "Sampling",
                "Controls how many independent paths each pixel traces per frame and how the Sobol/STBN sampler distributes Monte Carlo error.")
        { }

        public override void BuildUiToolkit(RendererSettingsEditorContext context, VisualElement parent)
        {
            var pathTracingSettings = Asset.PathTracingSettings;
            var samplerSettings = Asset.SamplerSettings;

            if (pathTracingSettings == null || samplerSettings == null)
            {
                return;
            }

            AddIntSliderField(
                parent,
                "Samples Per Pixel",
                () => pathTracingSettings.SamplesPerPixel,
                value =>
            {
                pathTracingSettings.SamplesPerPixel = value;

                InvalidateSampleVisualization();
            },
                1,
                8,
                "Number of independent path samples traced per pixel each frame.");

            AddSamplesPerPixelVisualizationElement(parent);

            AddObjectField<Texture2DArray>(parent, "Blue Noise Texture",
                () => samplerSettings.BlueNoiseTexture, value => samplerSettings.BlueNoiseTexture = value,
                "Texture2DArray of R8 STBN scalars. When assigned, all pixels share one Owen-scrambled Sobol sequence shifted per pixel by the blue noise (Cranley-Patterson rotation), so Monte Carlo error is distributed as blue noise over screen and time. When None, pure Sobol+hash. Bake via Tools -> LucentPath -> Bake -> Blue Noise Texture Array.");
        }
    }
}
