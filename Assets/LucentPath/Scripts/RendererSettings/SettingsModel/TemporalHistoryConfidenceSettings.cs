using System;

namespace LucentPath.Rendering.Settings
{
    [Serializable]
    public class TemporalHistoryConfidenceSettings
    {
        public float DepthConfidenceScale = 1f;
        public float NormalConfidencePower = 1f;
        public float ConfidencePower = 1f;
        public float ConfidenceDitherStrength = 1f;
    }
}