using System;
using UnityEngine;

namespace LucentPath.Rendering.Settings
{
    [Serializable]
    public class DisocclusionRecoverySettings
    {
        public int SpatialBootstrapRadius = 1;
        public float SpatialBootstrapDepthRejectThreshold = .06f;
        public float SpatialBootstrapNormalThreshold = .88f;
        public float SpatialBootstrapDepthPhi = 8f;
        public float SpatialBootstrapNormalPhi = 16f;
        [Range(0f, 1f)] public float HistoryMissSpatialBlend = .45f;
        [Range(0f, 1f)] public float BootstrapCompactionThreshold = .1f;

        [Header("Shadow-aware bootstrap")]
        [Min(0f)] public float SpatialBootstrapLuminancePhi = 10f;

        [Min(0f)] public float SpatialBootstrapLuminanceFloor = .05f;
        [Min(0f)] public float SpatialBootstrapVarianceCutoff = .3f;
        [Range(0f, 1f)] public float SpatialBootstrapVarianceMinBlend = .05f;

        [Header("Young history boost")]
        [Min(0)] public int YoungHistoryFrames = 3;

        [Min(0)] public int YoungHistoryRadius = 2;
        [Range(0f, 1f)] public float YoungHistoryBlend = .6f;
        [Min(1f)] public float YoungHistoryVarianceBoost = 3f;
    }
}

