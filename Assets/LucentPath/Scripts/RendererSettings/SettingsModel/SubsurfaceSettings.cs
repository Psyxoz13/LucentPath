using System;

namespace LucentPath.Rendering.Settings
{
    [Serializable]
    public class SubsurfaceSettings
    {
        public bool IsEnabled = false;
        public float IndirectIntensity = 1f;
    }
}
