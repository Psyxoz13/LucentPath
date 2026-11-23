using System;

namespace LucentPath.Rendering.Settings
{
    public enum LucentPathDebugVisualizationMode
    {
        None = 0,
        SurfaceSignal = 1,
        ReflectionSignal = 2,
        TransmissionSignal = 3,
        ShadowSignal = 4,
        DemodulatedDiffuseFactor = 5,
        DemodulatedSpecularFactor = 6,
        DemodulatedTransmissionFactor = 7,
        PrimaryDiffuseAlbedo = 8,
        PrimarySpecularF0 = 9,
        PrimaryTransmissionTint = 10,
        SurfaceVariance = 11,
        ReflectionVariance = 12,
        TransmissionVariance = 13,
        FireflyHeatmap = 14,
        RawSurfaceFrame = 15
    }

    [Serializable]
    public class PathTracingSettings
    {
        public bool IsDebugVisualizationAvailable =>
            DebugVisualizationMode != LucentPathDebugVisualizationMode.None;

        public int SamplesPerPixel = 1;
        public int SurfaceBounces = 2;
        public int ReflectionBounces = 3;
        public int TransmissionBounces = 4;
        public int RussianRouletteStart = 3;
        public bool IsEnvironmentNee = true;
        public int LightNeeRisCandidates = 1;
        public bool IsEmissiveSurfelNee = false;
        public int EmissiveSurfelNeeMaxCandidates = 32;
        public bool IsSkyObjectLighting = true;
        public int MaxLights = 32;
        public int MaxInstanceCount = 65536;
        public float MaxBounceDistance = 100f;
        public float SurfaceBias = .001f;
        public float DirectIntensity = 1f;

        public LucentPathDebugVisualizationMode DebugVisualizationMode = LucentPathDebugVisualizationMode.None;
    }
}
