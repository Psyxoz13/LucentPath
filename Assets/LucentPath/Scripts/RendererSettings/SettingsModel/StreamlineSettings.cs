using System;

namespace LucentPath.Rendering.Settings
{
    public enum StreamlineDlssRayReconstructionPreset
    {
        NvidiaDefault = 0,
        PresetD = 4,
        PresetE = 5
    }

    [Serializable]
    public class StreamlineSettings
    {
        public bool IsStreamlineActive => IsStreamlineEnabled;
        public bool IsRuntimeActive => IsStreamlineEnabled && IsDlssRayReconstructionEnabled;

        public bool IsStreamlineEnabled;
        public bool IsDlssRayReconstructionEnabled;
        public bool IsSceneViewAllowed;
        public bool IsFallbackToUnityDlssOrNative = true;
        public bool IsWarnWhenUnavailable = true;

        public string RuntimeDirectory = "Assets/LucentPath/Plugins/x86_64";
        public string LogDirectory = "Logs/Streamline";
        public string ApplicationId = string.Empty;
        public string ProjectId = string.Empty;

        public bool IsManualHookingUsed = true;
        public bool IsFrameBasedResourceTaggingUsed = true;
        public bool IsLoadDownloadedPlugins = true;
        public bool IsOverTheAirUpdatesAllowed = true;

        public bool IsColorBuffersHDR = true;
        public bool IsPackedNormalRoughness = true;
        public bool IsAlphaUpscalingEnabled;
        public float PreExposure = 1.0f;
        public float ExposureScale = 1.0f;

        public StreamlineDlssRayReconstructionPreset DlaaPreset = StreamlineDlssRayReconstructionPreset.PresetD;
        public StreamlineDlssRayReconstructionPreset QualityPreset = StreamlineDlssRayReconstructionPreset.PresetD;
        public StreamlineDlssRayReconstructionPreset BalancedPreset = StreamlineDlssRayReconstructionPreset.PresetD;
        public StreamlineDlssRayReconstructionPreset PerformancePreset = StreamlineDlssRayReconstructionPreset.PresetD;
        public StreamlineDlssRayReconstructionPreset UltraPerformancePreset = StreamlineDlssRayReconstructionPreset.PresetD;

        public void Validate()
        {
            RuntimeDirectory = RuntimeDirectory ?? string.Empty;
            LogDirectory = LogDirectory ?? string.Empty;
            ApplicationId = ApplicationId != null ? ApplicationId.Trim() : string.Empty;
            ProjectId = ProjectId != null ? ProjectId.Trim() : string.Empty;
            PreExposure = Math.Max(.0001f, PreExposure);
            ExposureScale = Math.Max(.0001f, ExposureScale);
            IsManualHookingUsed = true;
            IsFrameBasedResourceTaggingUsed = true;
            IsColorBuffersHDR = true;
            IsPackedNormalRoughness = true;
        }
    }
}
