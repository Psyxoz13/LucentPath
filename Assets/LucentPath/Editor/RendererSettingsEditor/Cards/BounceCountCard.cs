using UnityEngine;
using UnityEngine.UIElements;

namespace LucentPath.EditorUi.RendererSettings
{
    internal class BounceCountCard : RendererSettingsEditorCardBase
    {
        public BounceCountCard(LucentPathRendererSettingsEditor editor)
            : base(editor, "Bounces", "Per-channel bounce budgets and path termination.")
        { }

        public override void BuildUiToolkit(RendererSettingsEditorContext context, VisualElement parent)
        {
            var pathTracingSettings = Asset.PathTracingSettings;

            if (pathTracingSettings == null)
            {
                return;
            }

            AddIntSliderField(
                parent,
                "Surface Bounces",
                () => pathTracingSettings.SurfaceBounces,
                value =>
            {
                pathTracingSettings.SurfaceBounces = value;
            },
                1,
                16,
                "Bounce budget for paths whose primary surface is diffuse/glossy.");

            AddIntSliderField(
                parent,
                "Reflection Bounces",
                () => pathTracingSettings.ReflectionBounces,
                value =>
            {
                pathTracingSettings.ReflectionBounces = value;
            },
                1,
                16,
                "Bounce budget for paths whose primary surface is a reflection-dominant material (mirror/conductor). Also used as the PSR reflection-guide chain length.");

            AddIntSliderField(
                parent,
                "Transmission Bounces",
                () => pathTracingSettings.TransmissionBounces,
                value =>
            {
                pathTracingSettings.TransmissionBounces = value;
            },
                1,
                16,
                "Bounce budget for paths whose primary surface is transmissive (glass).");

            AddIntSliderField(
                parent,
                "Russian Roulette Start",
                () => pathTracingSettings.RussianRouletteStart,
                value => pathTracingSettings.RussianRouletteStart = value,
                () => 1,
                () => Mathf.Min(
                    pathTracingSettings.SurfaceBounces,
                    Mathf.Min(pathTracingSettings.ReflectionBounces, pathTracingSettings.TransmissionBounces)) + 1,
                "Bounce index at which probabilistic path termination begins.");

            AddBounceCountVisualizationElement(parent);
        }
    }
}
