using UnityEngine;
using UnityEngine.UIElements;

namespace LucentPath.EditorUi.RendererSettings
{
    internal class FireflySuppressionCard : RendererSettingsEditorCardBase
    {
        public FireflySuppressionCard(LucentPathRendererSettingsEditor editor)
            : base(
                editor,
                "Firefly Suppression",
                "Hard luminance clamp on indirect (bounce >= 1) lighting contributions, per signal, applied before accumulation and denoising.")
        { }

        public override void BuildUiToolkit(RendererSettingsEditorContext context, VisualElement parent)
        {
            var firefly = Asset.FireflySuppressionSettings;

            if (firefly == null)
            {
                return;
            }

            AddMinFloatField(parent, "Surface Indirect Clamp Multiplier",
                () => firefly.IndirectClampMultiplierSurface, value => firefly.IndirectClampMultiplierSurface = value, 0f,
                "Base luminance ceiling for indirect (bounce >= 1) diffuse/surface-signal contributions, clamped per deposit before accumulation/denoising. Direct lighting (bounce 0) is never touched. 0 = off. Scaled per-pixel by the Surface History Trust LUT (if assigned).");

            AddMinFloatField(parent, "Reflection Indirect Clamp Multiplier",
                () => firefly.IndirectClampMultiplierReflection, value => firefly.IndirectClampMultiplierReflection = value, 0f,
                "Indirect clamp ceiling for the reflection signal (glossy/specular paths). 0 = off.");

            AddMinFloatField(parent, "Transmission Indirect Clamp Multiplier",
                () => firefly.IndirectClampMultiplierTransmission, value => firefly.IndirectClampMultiplierTransmission = value, 0f,
                "Indirect clamp ceiling for the transmission signal (glass/refraction paths). 0 = off.");

            AddObjectField<Texture2D>(parent, "Surface History Trust LUT",
                () => firefly.IndirectClampHistoryLutSurface, value => firefly.IndirectClampHistoryLutSurface = value,
                "Optional 1D lookup scaling the surface clamp ceiling by per-pixel history trust (this signal's accumulated history length / Max History Length, 0..1). X = trust, Y = multiplier. Empty = fixed clamp. Bake with Tools/LucentPath/Bake/Indirect Clamp History LUT.");

            AddObjectField<Texture2D>(parent, "Reflection History Trust LUT",
                () => firefly.IndirectClampHistoryLutReflection, value => firefly.IndirectClampHistoryLutReflection = value,
                "Optional 1D history-trust lookup for the reflection clamp ceiling. Empty = fixed clamp.");

            AddObjectField<Texture2D>(parent, "Transmission History Trust LUT",
                () => firefly.IndirectClampHistoryLutTransmission, value => firefly.IndirectClampHistoryLutTransmission = value,
                "Optional 1D history-trust lookup for the transmission clamp ceiling. Empty = fixed clamp.");
        }
    }
}
