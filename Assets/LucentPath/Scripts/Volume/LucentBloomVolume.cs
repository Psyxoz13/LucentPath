using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LucentPath.Rendering
{
    [Serializable, VolumeComponentMenu("Post-processing/Lucent Bloom")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public class LucentBloomVolume : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("Overall energy scale applied to the spectral PSF bloom contribution.")]
        public MinFloatParameter Intensity = new(0f, 0f);

        [Tooltip("Scales the effective PSF radius without changing the baked profile texture.")]
        public MinFloatParameter RadiusScale = new(1f, 0f);

        [Tooltip("Blends from luminance-equalized bloom weights to the full RGB PSF profile.")]
        public ClampedFloatParameter ChromaticSpreadScale = new(.25f, 0f, 1f);

        [Tooltip("Linear 8x1 RGB texture containing baked spectral PSF bloom band weights from primary-source ocular straylight data.")]
        public TextureParameter RgbPsfProfile = new(null);

        public bool IsActive() => active && Intensity.value > 0f;

        [Obsolete("Unused. #from(2023.1)")]
        public bool IsTileCompatible() => false;
    }
}
