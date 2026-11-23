using System;
using UnityEngine;

namespace LucentPath.Rendering.Settings
{
    [Serializable]
    public class SamplerSettings
    {
        [Header("Spatiotemporal Blue Noise (Wolfe et al. 2022)")]
        [Tooltip("Texture2DArray of R8 STBN scalars applied as a per-pixel Cranley-Patterson rotation of a shared Owen-scrambled Sobol sequence (Georgiev & Fajardo 2016). Layer advances with the sample index; dimensions are decorrelated by R2 lookup shifts. When unset, falls back to pure Sobol with per-pixel hash scramble. 128x128 with 32-64 layers is a good default. Bake via Tools → LucentPath → Bake → Blue Noise Texture Array.")]
        public Texture2DArray BlueNoiseTexture;

        public int GetRuntimeConfigurationHash()
        {
            unchecked
            {
                int h = 17;

                h = h * 31 + (BlueNoiseTexture != null ? BlueNoiseTexture.GetEntityId().GetHashCode() : 0);

                return h;
            }
        }
    }
}
