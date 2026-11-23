using UnityEngine.UIElements;

namespace LucentPath.EditorUi.RendererSettings
{
    internal class DlssMotionVectorsCard : RendererSettingsEditorCardBase
    {
        public DlssMotionVectorsCard(LucentPathRendererSettingsEditor editor)
            : base(editor, "DLSS Motion Vectors", "Defines how depth and motion vectors are prepared so DLSS can reconstruct stable detail.")
        { }

        public override bool ShouldDraw(RendererSettingsEditorContext context)
        {
            return IsDlssRuntimeActive(context.Asset?.DlssSettings);
        }

        public override void BuildUiToolkit(RendererSettingsEditorContext context, VisualElement parent)
        {
            var settings = Asset?.DlssSettings;

            if (settings == null)
            {
                return;
            }

            AddToggleField(parent, "HDR Input", () => settings.IsHdrInput, value => settings.IsHdrInput = value,
                "Treat the DLSS color input as HDR instead of LDR.");

            AddToggleField(parent, "Use System Reversed Z", () => settings.IsSystemReversedZUsed, value => settings.IsSystemReversedZUsed = value,
                "Automatically mirror Unity's reversed-Z setting into DLSS depth interpretation.");

            RegisterConditionalElement(
                AddToggleField(parent, "Depth Inverted", () => settings.IsDepthInverted, value => settings.IsDepthInverted = value,
                "Invert the depth buffer fed into DLSS when it does not match Unity's default interpretation."),
                () => settings.IsSystemReversedZUsed == false);

            AddEnumField(parent, "Motion Vector Space", () => settings.MotionVectorSpace, value => settings.MotionVectorSpace = value,
                "Coordinate space used by motion vectors before they are remapped for DLSS.");

            AddToggleField(
                parent,
                "Auto Motion Vector API Scale",
                () => settings.IsAutoMotionVectorApiScale,
                value => settings.IsAutoMotionVectorApiScale = value,
                "Automatically derives DLSS motion-vector scaling from the configured motion-vector space.");

            AddToggleField(
                parent,
                "Invert Motion Vector X",
                () => settings.InvertMotionVectorX != 0u,
                value => settings.InvertMotionVectorX = value ? 1u : 0u,
                "Flip the X component of motion vectors before DLSS consumes them.");

            AddToggleField(
                parent,
                "Invert Motion Vector Y",
                () => settings.InvertMotionVectorY != 0u,
                value => settings.InvertMotionVectorY = value ? 1u : 0u,
                "Flip the Y component of motion vectors before DLSS consumes them.");

            RegisterConditionalElement(
                AddFloatField(parent, "Motion Vector Scale X", () => settings.MotionVectorScaleX, value => settings.MotionVectorScaleX = value,
                "Manual multiplier for the X component of motion vectors when automatic scaling is not enough."),
                () => settings.IsAutoMotionVectorApiScale == false);

            RegisterConditionalElement(
                AddFloatField(parent, "Motion Vector Scale Y", () => settings.MotionVectorScaleY, value => settings.MotionVectorScaleY = value,
                "Manual multiplier for the Y component of motion vectors when automatic scaling is not enough."),
                () => settings.IsAutoMotionVectorApiScale == false);
        }
    }
}
