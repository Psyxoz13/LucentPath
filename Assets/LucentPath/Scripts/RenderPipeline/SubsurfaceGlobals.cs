using LucentPath.Rendering.Settings;
using UnityEngine;
using UnityEngine.Rendering;

namespace LucentPath.Rendering
{
    internal static class SubsurfaceGlobals
    {
        private static readonly int _enabledProperty = Shader.PropertyToID("_SubsurfaceEnabled");
        private static readonly int _intensityProperty = Shader.PropertyToID("_SubsurfaceIntensity");

        public static void PushGlobals(CommandBuffer cmd, SubsurfaceSettings settings)
        {
            if (cmd == null)
            {
                return;
            }

            bool enabled = settings != null && settings.IsEnabled;

            cmd.SetGlobalInt(_enabledProperty, enabled ? 1 : 0);
            cmd.SetGlobalFloat(_intensityProperty, settings != null ? Mathf.Max(0f, settings.IndirectIntensity) : 1f);
        }
    }
}
