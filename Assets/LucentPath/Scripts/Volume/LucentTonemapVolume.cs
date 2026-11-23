using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LucentPath.Rendering
{
    public enum LucentTonemapMode
    {
        None = 0,
        AgX = 1,
        [InspectorName("ACES")]
        Aces = 2,
        Neutral = 3,
        [InspectorName("Khronos PBR Neutral")]
        KhronosPbrNeutral = 4
    }

    public enum LucentTonemapLook
    {
        Base = 0,
        Golden = 1,
        Punchy = 2
    }

    [Serializable]
    public class LucentTonemapModeParameter : VolumeParameter<LucentTonemapMode>
    {
        public LucentTonemapModeParameter(LucentTonemapMode value, bool overrideState = false) : base(value, overrideState)
        { }
    }

    [Serializable]
    public class LucentTonemapLookParameter : VolumeParameter<LucentTonemapLook>
    {
        public LucentTonemapLookParameter(LucentTonemapLook value, bool overrideState = false) : base(value, overrideState)
        { }
    }

    [Serializable]
    public class AgxSpi1dLutParameter : VolumeParameter<AgxSpi1dLut>
    {
        public AgxSpi1dLutParameter(AgxSpi1dLut value, bool overrideState = false) : base(value, overrideState)
        { }
    }

    [Serializable, VolumeComponentMenu("Post-processing/Lucent Tonemap")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public class LucentTonemapVolume : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("Selects the documented display transform applied during the final LucentPath presentation pass.")]
        public LucentTonemapModeParameter Mode = new(LucentTonemapMode.None);

        [Tooltip("Selects the documented AgX OCIO look. Punchy is the game-ready default.")]
        public LucentTonemapLookParameter Look = new(LucentTonemapLook.Punchy);

        [Tooltip("Imported official AgX_Default_Contrast.spi1d asset used by the AgX tonemap.")]
        public AgxSpi1dLutParameter DefaultContrastLut = new(null);

        [Tooltip("Applies the tonemap in Scene View cameras.")]
        public BoolParameter ApplyInSceneView = new(false);

        public bool IsActive() => active && Mode.value != LucentTonemapMode.None;

        [Obsolete("Unused. #from(2023.1)")]
        public bool IsTileCompatible() => false;
    }
}
