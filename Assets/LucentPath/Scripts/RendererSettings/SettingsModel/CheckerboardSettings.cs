using System;

namespace LucentPath.Rendering.Settings
{
    [Serializable]
    public class CheckerboardSettings
    {
        public bool IsEnabled = false;
        public int ReconstructionRadius = 2;
        public float NeighborNormalThreshold = .9f;
        public float NeighborDepthThreshold = .1f;
        public int FullRateHistoryFrames = 16;
    }
}
