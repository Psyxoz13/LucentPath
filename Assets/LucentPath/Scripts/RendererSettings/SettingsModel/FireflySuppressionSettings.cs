using System;
using UnityEngine;

namespace LucentPath.Rendering.Settings
{
    [Serializable]
    public class FireflySuppressionSettings
    {
        public float IndirectClampMultiplierSurface = 1f;
        public float IndirectClampMultiplierReflection = 1f;
        public float IndirectClampMultiplierTransmission = 1f;
        public Texture2D IndirectClampHistoryLutSurface;
        public Texture2D IndirectClampHistoryLutReflection;
        public Texture2D IndirectClampHistoryLutTransmission;
    }
}
