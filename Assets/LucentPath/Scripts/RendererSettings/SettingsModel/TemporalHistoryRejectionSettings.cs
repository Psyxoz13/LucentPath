using System;

namespace LucentPath.Rendering.Settings
{
    [Serializable]
    public class TemporalHistoryRejectionSettings
    {
        public float ResolvedMotionRejectionStrength =>
            MotionRejectionStrength < 0f ? 0f : (MotionRejectionStrength > 1f ? 1f : MotionRejectionStrength);

        public float DepthRejectThreshold = .05f;
        public float NormalRejectThreshold = .85f;
        public float MotionRejectionStrength = .3f;
        public float HistoryColorClipSigma = 1f;
    }
}