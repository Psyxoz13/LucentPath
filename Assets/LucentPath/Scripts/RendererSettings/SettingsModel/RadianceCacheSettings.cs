using System;

namespace LucentPath.Rendering.Settings
{
    [Serializable]
    public class RadianceCacheSettings
    {
        public bool IsEnabled = true;
        public int SlotCountExponent = 20;
        public float BaseCellSize = .05f;
        public float LodScale = 1f;
        public int MinSamples = 16;
        public int MinDepth = 1;
        public float CorrectionProbability = .5f;
        public float TemporalDecay = .95f;
        public bool IsDebugView = false;
    }
}
