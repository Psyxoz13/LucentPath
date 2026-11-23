using UnityEngine.UIElements;

namespace LucentPath.EditorUi.RendererSettings
{
    internal class HistoryRejectionCard : RendererSettingsEditorCardBase
    {
        public HistoryRejectionCard(LucentPathRendererSettingsEditor editor)
            : base(editor, "History Rejection", "Sets how different history may be before it is discarded to avoid ghosting and stale data.")
        { }

        public override void BuildUiToolkit(RendererSettingsEditorContext context, VisualElement parent)
        {
            var temporalHistoryRejectionSettings = Asset.TemporalHistoryRejectionSettings;

            if (temporalHistoryRejectionSettings == null)
            {
                return;
            }

            AddFloatSliderField(parent, "Temporal Depth Reject Threshold",
                () => temporalHistoryRejectionSettings.DepthRejectThreshold, value => temporalHistoryRejectionSettings.DepthRejectThreshold = value, .001f, 1f,
                "Depth difference threshold for rejecting previous-frame history.");

            AddFloatSliderField(parent, "Temporal Normal Reject Threshold",
                () => temporalHistoryRejectionSettings.NormalRejectThreshold, value => temporalHistoryRejectionSettings.NormalRejectThreshold = value, 0f, 1f,
                "Normal similarity threshold for accepting previous-frame history.");

            AddFloatSliderField(parent, "Motion Rejection Strength",
                () => temporalHistoryRejectionSettings.MotionRejectionStrength, value => temporalHistoryRejectionSettings.MotionRejectionStrength = value, 0f, 1f,
                "Resolution-independent history rejection by screen motion (linear mapping). Reject threshold = lerp(maxScreenSide, 1px, value). Low (~0.01) = threshold ~one screen side = history kept through motion = cheapest, no motion-time spatial reconstruction; threshold stays large across most of the range. Toward 1 = threshold drops to ~1 pixel = strong rejection = crisp motion but full-screen reconstruction cost. Depth/normal still catch true disocclusions at any value. Keep low for FPS; raise only if fast objects with imperfect motion vectors ghost.");

            AddMinFloatField(parent, "History Color Clip Sigma Scale",
                () => temporalHistoryRejectionSettings.HistoryColorClipSigma, value => temporalHistoryRejectionSettings.HistoryColorClipSigma = value, 0f,
                "NEED TOOLTIP");
        }
    }
}
