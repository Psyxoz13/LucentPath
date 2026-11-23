using LucentPath.EditorUi.Core;
using UnityEngine.UIElements;

namespace LucentPath.EditorUi.RendererSettings
{
    internal class PathTracingSection : EditorSectionBase<RendererSettingsEditorContext>
    {
        public PathTracingSection(LucentPathRendererSettingsEditor editor)
            : base(
                "Path Tracing",
                "Controls how paths are spawned, how far they travel, how much lighting is sampled, and when tracing stops.",
                () => editor.IsPathTracingSectionExpanded,
                editor.OnPathTracingSectionVisibilityChanged,
                new EditorCardBase<RendererSettingsEditorContext>[]
                {
                    new SamplingCard(editor),
                    new BounceDistanceCard(editor),
                    new BounceCountCard(editor),
                    new LightingCard(editor),
                    new SubsurfaceCard(editor),
                    new SurfaceBiasCard(editor),
                    new RadianceCacheCard(editor)
                })
        { }
    }

    internal class SubsurfaceCard : RendererSettingsEditorCardBase
    {
        public SubsurfaceCard(LucentPathRendererSettingsEditor editor)
            : base(
                editor,
                "Subsurface Scattering",
                "Christensen-Burley normalized-diffusion BSSRDF (2015) - analytic diffusion profile with a single probe ray per scatter, for high-albedo media (skin, marble, wax, jade). Per-material color/radius/anisotropy authored on the material.")
        { }

        public override void BuildUiToolkit(RendererSettingsEditorContext context, VisualElement parent)
        {
            var settings = Asset.SubsurfaceSettings;

            if (settings == null)
            {
                return;
            }

            AddToggleField(parent, "Enabled",
                () => settings.IsEnabled, value => settings.IsEnabled = value,
                "Master toggle for subsurface scattering. When on, surfaces with subsurface_weight > 0 use a Christensen-Burley diffusion profile (single probe ray) instead of standard BSDF continuation at the scatter event.");

            RegisterConditionalElement(AddMinFloatField(parent, "Indirect Intensity",
                () => settings.IndirectIntensity, value => settings.IndirectIntensity = value, 0f,
                "Scalar multiplier on SSS contribution. 1.0 = physically correct."), () => settings.IsEnabled);
        }
    }
}
