using System;

namespace LucentPath.Rendering.Settings
{
    [Serializable]
    public class TemporalHistorySettings
    {
        public int MaxHistoryLength = 42;
        public float LowConfidenceSpatialBlend = .35f;
        public float GuideMinSignalWeight = .001f;
        public float GuidePlanarNormalThreshold = .999f;
        public float GuideMaxVirtualProjectionErrorPixels = 1f;
        public float GuideMaxPlaneMotionMeters = .001f;
        public float ShadowAntiLagSensitivity = 2f;
        public float ShadowAntiLagWarmup = .125f;
        public int ShadowAntiLagRadius = 1;
    }
}