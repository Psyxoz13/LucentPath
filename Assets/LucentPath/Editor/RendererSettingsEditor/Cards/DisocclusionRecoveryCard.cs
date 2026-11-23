using UnityEngine.UIElements;

namespace LucentPath.EditorUi.RendererSettings
{
    internal class DisocclusionRecoveryCard : RendererSettingsEditorCardBase
    {
        public DisocclusionRecoveryCard(LucentPathRendererSettingsEditor editor)
            : base(
                editor,
                "Disocclusion / History-Fix",
                "Controls spatial repair, uncertainty, and young-history filtering for newly revealed or low-confidence regions.")
        { }

        public override void BuildUiToolkit(RendererSettingsEditorContext context, VisualElement parent)
        {
            var disocclusionRecoverySettings = Asset.DisocclusionRecoverySettings;

            if (disocclusionRecoverySettings == null)
            {
                return;
            }

            AddIntSliderField(parent, "Base Bootstrap Radius",
                () => disocclusionRecoverySettings.SpatialBootstrapRadius, value => disocclusionRecoverySettings.SpatialBootstrapRadius = value, 0, 3,
                "Base spatial reconstruction radius for surface pixels after history miss. Runtime clamp: 0..3. History-Fix / Young Radius can temporarily raise the effective radius.");

            AddFloatSliderField(parent, "Bootstrap Depth Reject Threshold",
                () => disocclusionRecoverySettings.SpatialBootstrapDepthRejectThreshold, value => disocclusionRecoverySettings.SpatialBootstrapDepthRejectThreshold = value, .001f, 1f,
                "Maximum relative depth difference accepted for bootstrap neighbors. Lower values preserve shadow/contact edges more strongly.");

            AddFloatSliderField(parent, "Bootstrap Normal Reject Threshold",
                () => disocclusionRecoverySettings.SpatialBootstrapNormalThreshold, value => disocclusionRecoverySettings.SpatialBootstrapNormalThreshold = value, 0f, 1f,
                "Minimum normal agreement accepted for bootstrap neighbors. Higher values make repair more edge-aware.");

            AddMinFloatField(parent, "Bootstrap Depth Phi",
                () => disocclusionRecoverySettings.SpatialBootstrapDepthPhi, value => disocclusionRecoverySettings.SpatialBootstrapDepthPhi = value, .1f,
                "Depth falloff for accepted bootstrap neighbors. Higher values reduce cross-depth bleeding.");

            AddMinFloatField(parent, "Bootstrap Normal Phi",
                () => disocclusionRecoverySettings.SpatialBootstrapNormalPhi, value => disocclusionRecoverySettings.SpatialBootstrapNormalPhi = value, .1f,
                "Normal falloff for accepted bootstrap neighbors. Higher values reduce cross-normal bleeding.");

            AddFloatSliderField(parent, "History Miss Spatial Blend",
                () => disocclusionRecoverySettings.HistoryMissSpatialBlend, value => disocclusionRecoverySettings.HistoryMissSpatialBlend = value, 0f, 1f,
                "Base blend toward spatially reconstructed color when temporal history is missing or low confidence.");

            AddFloatSliderField(parent, "Bootstrap Compaction Threshold",
                () => disocclusionRecoverySettings.BootstrapCompactionThreshold, value => disocclusionRecoverySettings.BootstrapCompactionThreshold = value, 0f, 1f,
                "Performance/quality cutoff for the compacted surface-bootstrap pass. Pixels are sent to bootstrap only on disocclusion, young history, or when the estimated spatial blend exceeds this value. Higher = fewer pixels bootstrapped = faster, but converged pixels with small residual uncertainty skip spatial repair. 0 = bootstrap nearly everything (no compaction benefit).");

            AddMinFloatField(parent, "Bootstrap Luminance Phi",
                () => disocclusionRecoverySettings.SpatialBootstrapLuminancePhi, value => disocclusionRecoverySettings.SpatialBootstrapLuminancePhi = value, 0f,
                "Penalty exponent applied to neighbors whose luminance differs from the center pixel. Higher = more shadow-aware (rejects bright neighbors of dark pixels).");

            AddMinFloatField(parent, "Bootstrap Luminance Floor",
                () => disocclusionRecoverySettings.SpatialBootstrapLuminanceFloor, value => disocclusionRecoverySettings.SpatialBootstrapLuminanceFloor = value, 0f,
                "Minimum luminance used in the ratio. Prevents division blow-up in very dark regions.");

            AddMinFloatField(parent, "Bootstrap Variance Dampening Cutoff",
                () => disocclusionRecoverySettings.SpatialBootstrapVarianceCutoff, value => disocclusionRecoverySettings.SpatialBootstrapVarianceCutoff = value, 1e-4f,
                "Relative-variance scale used to dampen bootstrap blend. Lower values suppress repair more aggressively in noisy or shadow-boundary regions.");

            AddFloatSliderField(parent, "Bootstrap Min Variance Blend",
                () => disocclusionRecoverySettings.SpatialBootstrapVarianceMinBlend, value => disocclusionRecoverySettings.SpatialBootstrapVarianceMinBlend = value, 0f, 1f,
                "Lower clamp on the variance dampening factor. 0 = bootstrap can be fully suppressed in noisy regions.");

            AddIntSliderField(parent, "History-Fix / Young Frames",
                () => disocclusionRecoverySettings.YoungHistoryFrames, value => disocclusionRecoverySettings.YoungHistoryFrames = value, 0, 16,
                "Number of frames after reset/disocclusion where History-Fix and young-history boost are active. Runtime clamp: 0..16.");

            AddIntSliderField(parent, "History-Fix / Young Radius",
                () => disocclusionRecoverySettings.YoungHistoryRadius, value => disocclusionRecoverySettings.YoungHistoryRadius = value, 0, 4,
                "Maximum radius used by History-Fix and boosted bootstrap on young or low-confidence history. Runtime clamp: 0..4.");

            AddFloatSliderField(parent, "Young Bootstrap Blend",
                () => disocclusionRecoverySettings.YoungHistoryBlend, value => disocclusionRecoverySettings.YoungHistoryBlend = value, 0f, 1f,
                "Bootstrap blend used on the youngest history frame, fading back to History Miss Spatial Blend.");

            AddMinFloatField(parent, "Repair Variance Boost",
                () => disocclusionRecoverySettings.YoungHistoryVarianceBoost, value => disocclusionRecoverySettings.YoungHistoryVarianceBoost = value, 1f,
                "Multiplier applied to uncertainty from young-history, low-confidence, and spatial repair so A-Trous treats repaired pixels as temporary estimates.");
        }
    }
}
