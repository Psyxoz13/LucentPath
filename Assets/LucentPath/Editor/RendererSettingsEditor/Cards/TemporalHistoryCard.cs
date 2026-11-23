using UnityEngine.UIElements;

namespace LucentPath.EditorUi.RendererSettings
{
    internal class TemporalHistoryCard : RendererSettingsEditorCardBase
    {
        public TemporalHistoryCard(LucentPathRendererSettingsEditor editor)
            : base(editor, "Temporal History", "Sets how many past frames can accumulate into the current pixel before old data is dropped.")
        { }

        public override void BuildUiToolkit(RendererSettingsEditorContext context, VisualElement parent)
        {
            var temporalHistorySettings = Asset.TemporalHistorySettings;

            if (temporalHistorySettings == null)
            {
                return;
            }

            AddIntSliderField(parent, "Max History Length",
                () => temporalHistorySettings.MaxHistoryLength, value => temporalHistorySettings.MaxHistoryLength = value, 1, 128,
                "Maximum number of frames that can accumulate in temporal history.");

            AddFloatSliderField(parent, "Low Confidence Spatial Blend",
                () => temporalHistorySettings.LowConfidenceSpatialBlend, value => temporalHistorySettings.LowConfidenceSpatialBlend = value, 0f, 1f,
                "How much spatial fallback is mixed in when history confidence is low.");

            AddMinFloatField(parent, "Guide Min Signal Weight",
                () => temporalHistorySettings.GuideMinSignalWeight, value => temporalHistorySettings.GuideMinSignalWeight = value, 0f,
                "Minimum lobe weight needed to create a dedicated mirror or glass reprojection guide.");

            AddFloatSliderField(parent, "Guide Planar Normal Threshold",
                () => temporalHistorySettings.GuidePlanarNormalThreshold, value => temporalHistorySettings.GuidePlanarNormalThreshold = value, .9f, 1f,
                "Minimum normal agreement required before a mirror/glass surface is treated as a stable flat reprojection plane.");

            AddMinFloatField(parent, "Guide Max Projection Error",
                () => temporalHistorySettings.GuideMaxVirtualProjectionErrorPixels, value => temporalHistorySettings.GuideMaxVirtualProjectionErrorPixels = value, 0f,
                "Maximum current-frame projection error in pixels for a generated virtual mirror/glass history point.");

            AddMinFloatField(parent, "Guide Max Plane Motion",
                () => temporalHistorySettings.GuideMaxPlaneMotionMeters, value => temporalHistorySettings.GuideMaxPlaneMotionMeters = value, 0f,
                "Maximum mirror/glass plane motion in world units allowed for static-plane reprojection.");

            AddFloatSliderField(parent, "Shadow Anti-Lag Sensitivity",
                () => temporalHistorySettings.ShadowAntiLagSensitivity, value => temporalHistorySettings.ShadowAntiLagSensitivity = value, 1f, 8f,
                "How hard shadow history is dropped when visibility changes (moving blocker). Higher reacts faster but adds edge noise.");

            AddFloatSliderField(parent, "Shadow Anti-Lag Warmup",
                () => temporalHistorySettings.ShadowAntiLagWarmup, value => temporalHistorySettings.ShadowAntiLagWarmup = value, 0f, 1f,
                "How quickly the shadow anti-lag engages with history age. Higher = engages with fewer accumulated frames.");

            AddIntSliderField(parent, "Shadow Anti-Lag Radius",
                () => temporalHistorySettings.ShadowAntiLagRadius, value => temporalHistorySettings.ShadowAntiLagRadius = value, 0, 2,
                "Neighborhood radius used to average current shadow visibility before change detection (suppresses 1-spp noise).");

            AddTemporalHistoryVisualizationElement(parent);
        }
    }
}
