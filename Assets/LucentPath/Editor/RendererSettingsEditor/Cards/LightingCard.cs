using UnityEngine.UIElements;

namespace LucentPath.EditorUi.RendererSettings
{
    internal class LightingCard : RendererSettingsEditorCardBase
    {
        public LightingCard(LucentPathRendererSettingsEditor editor)
            : base(editor, "Lighting", "Sets the direct-light budget the tracer considers and scales its direct-light contribution.")
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
                "Max Lights",
                () => pathTracingSettings.MaxLights,
                value =>
            {
                pathTracingSettings.MaxLights = value;

                InvalidateLightingPreviewLights();
            },
                1,
                128,
                "Upper limit for lights uploaded to the GPU path tracing pass.");

            AddFloatField(parent, "Direct Intensity", () => pathTracingSettings.DirectIntensity, value => pathTracingSettings.DirectIntensity = value,
                "Multiplier applied to direct lighting contribution.");

            AddMinIntField(parent, "Max Instance Count", () => pathTracingSettings.MaxInstanceCount,
                value => pathTracingSettings.MaxInstanceCount = value, 1,
                "Capacity of the per-instance GPU material cache (used by ray tracing). Default 65536. Increase only if your scene has more ray tracing instances than this.");

            AddIntSliderField(parent, "Light NEE RIS Candidates",
                () => pathTracingSettings.LightNeeRisCandidates, value => pathTracingSettings.LightNeeRisCandidates = value, 1, 32,
                "Resampled Importance Sampling (Talbot 2005) for analytic-light next-event estimation. With value M>1, draws M candidate (light, point) samples from the light-selection PMF and the per-light area sampling, weights each by the full product target (BSDF x cos x light-radiance x MIS), and resamples one to trace a single shadow ray. This approximately importance-samples the BSDF x light product per shading point, which the point-independent light PMF alone does not, sharply cutting fireflies from many-light scenes and from large/close area lights. Unbiased and white-noise (no reservoir reuse, not ReSTIR); converges to the exact result. Cost is M candidate evaluations per vertex (no extra rays). 1 = off (single-sample light NEE). Try 8-16.");

            AddToggleField(
                parent,
                "Environment NEE",
                () => pathTracingSettings.IsEnvironmentNee,
                value =>
            {
                pathTracingSettings.IsEnvironmentNee = value;
            },
                "Toggle for environment (sky) next-event-estimation on primary-ray surfaces only. When ON, directly-seen surfaces sample the environment via a dedicated shadow ray (MIS-combined with BSDF-sampled sky hits). When OFF, the environment is lit purely by BSDF-sampled bounces hitting the sky (MIS weight 1, no energy loss). Secondary bounces never sample the environment via NEE regardless of this toggle. On = default.");

            AddToggleField(
                parent,
                "Emissive Surfel NEE",
                () => pathTracingSettings.IsEmissiveSurfelNee,
                value =>
            {
                pathTracingSettings.IsEmissiveSurfelNee = value;
            },
                "Next-event-estimation toward emissive geometry via discovered surfels. Emissive surface hits are progressively accumulated (deduplicated to one representative per world cell) into a compact global emitter-proxy list. Each shading point power-importance-samples one proxy from the whole list, cone-samples a direction toward it, traces one visibility ray, and reads the emitter radiance on instance match. MIS-combined with BSDF-sampled emitter hits via a shared exact mixture PDF on both the compute-shade NEE side and the ray-gen emission terminal, so it is unbiased and white-noise and converges to the exact result. Cuts noise from emissive-mesh light sources that the analytic-light path does not cover. Off = default.");

            AddIntSliderField(parent, "Emissive Surfel NEE Candidates",
                () => pathTracingSettings.EmissiveSurfelNeeMaxCandidates, value => pathTracingSettings.EmissiveSurfelNeeMaxCandidates = value, 1, 128,
                "Maximum number of discovered emitter proxies the surfel NEE considers per shading point (the global mixture is evaluated over this many). Higher covers more distinct emissive surfaces at higher per-vertex cost; lower is cheaper but only the first-discovered proxies are sampled directly (the rest stay covered by BSDF-sampled hits via MIS, still unbiased). Default 32.");

            AddToggleField(
                parent,
                "Sky Object Lighting",
                () => pathTracingSettings.IsSkyObjectLighting,
                value =>
            {
                pathTracingSettings.IsSkyObjectLighting = value;
            },
                "When off, the sky/environment no longer adds diffuse ambient light to objects (env-NEE and indirect sky on diffuse bounces are skipped). The sky stays visible as background and in specular reflections; analytic lights and their shadows are unaffected. On = default.");

            AddLightingVisualizationElement(parent);
        }
    }
}
