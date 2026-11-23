using UnityEngine;
using UnityEngine.UIElements;

namespace LucentPath.EditorUi.RendererSettings
{
    internal class RadianceCacheCard : RendererSettingsEditorCardBase
    {
        public RadianceCacheCard(LucentPathRendererSettingsEditor editor)
            : base(
                editor,
                "Path Guiding",
                "World-space online path guiding: learns the incident-radiance directional distribution per spatial-hash cell and guides indirect continuation directions.")
        { }

        public override void BuildUiToolkit(RendererSettingsEditorContext context, VisualElement parent)
        {
            var settings = Asset.RadianceCacheSettings;

            if (settings == null)
            {
                return;
            }

            var pathTracingSettings = Asset.PathTracingSettings;

            AddToggleField(parent, "Enabled",
                () => settings.IsEnabled, value => settings.IsEnabled = value,
                "Enables online path guiding (currently: deposits the incident-radiance directional field per cell).");

            var slotCountField = AddIntSliderField(parent, "Slot Count Exponent",
                () => settings.SlotCountExponent, value => settings.SlotCountExponent = value, 16, 23,
                "Log2 of the number of guiding cells. Higher = fewer collisions, more memory (2^exp x 264 bytes). Changing it reallocates the field.");

            var baseCellSizeField = AddMinFloatField(parent, "Base Cell Size",
                () => settings.BaseCellSize, value => settings.BaseCellSize = value, 1e-4f,
                "World-space size of the finest guiding cell near the camera.");

            var lodScaleField = AddMinFloatField(parent, "Lod Scale",
                () => settings.LodScale, value => settings.LodScale = value, 1e-4f,
                "Scales how quickly guiding cells grow with distance from the camera.");

            var minSamplesField = AddMinIntField(parent, "Min Samples",
                () => settings.MinSamples, value => settings.MinSamples = value, 1,
                "Samples a cell must accumulate before its distribution is trusted for guided sampling.");

            var minDepthField = AddIntSliderField(parent, "Min Depth",
                () => settings.MinDepth, value => settings.MinDepth = value,
                () => 1,
                () => pathTracingSettings != null ? Mathf.Max(1, pathTracingSettings.SurfaceBounces) : 16,
                "Earliest bounce depth at which a vertex deposits/guides. 1 = guide from the primary hit's continuation onward.");

            var guideFractionField = AddFloatSliderField(parent, "Guide Fraction",
                () => settings.CorrectionProbability, value => settings.CorrectionProbability = value, .05f, .95f,
                "Defensive mixture weight between guided and BSDF sampling for the indirect continuation (one-sample MIS, unbiased). 0.05 = mostly BSDF, 0.95 = mostly guided.");

            var temporalDecayField = AddFloatSliderField(parent, "Temporal Decay",
                () => settings.TemporalDecay, value => settings.TemporalDecay = value, 0f, .999f,
                "Per-frame multiplicative decay of the learned field (exponential moving average). Lower = adapts faster to camera/light motion but noisier; higher = more stable but staler. A cell whose decayed count reaches zero frees its hash slot for reuse.");

            RegisterConditionalElement(slotCountField, () => settings.IsEnabled);
            RegisterConditionalElement(baseCellSizeField, () => settings.IsEnabled);
            RegisterConditionalElement(lodScaleField, () => settings.IsEnabled);
            RegisterConditionalElement(minSamplesField, () => settings.IsEnabled);
            RegisterConditionalElement(minDepthField, () => settings.IsEnabled);
            RegisterConditionalElement(guideFractionField, () => settings.IsEnabled);
            RegisterConditionalElement(temporalDecayField, () => settings.IsEnabled);
        }
    }
}
