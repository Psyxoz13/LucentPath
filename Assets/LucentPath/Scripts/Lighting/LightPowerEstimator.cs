using LucentPath.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using static LucentPath.Rendering.PipelineShaderProperties;

namespace LucentPath.Lighting
{
    internal static class LightPowerEstimator
    {
        private static readonly int _analyticLightsTotalPowerProperty = Shader.PropertyToID("_AnalyticLightsTotalPower");

        public static float ComputeAnalyticTotalPower(GpuLight[] lights)
        {
            if (lights == null || lights.Length == 0)
            {
                return 0f;
            }

            float total = 0f;

            for (int i = 0; i < lights.Length; i++)
            {
                total += ComputeSingleLightPower(lights[i]);
            }

            return total;
        }

        public static void PushAnalyticTotalPower(CommandBuffer cmd, float totalPower)
        {
            if (cmd == null)
            {
                return;
            }

            cmd.SetGlobalFloat(_analyticLightsTotalPowerProperty, Mathf.Max(totalPower, 0f));
        }

        internal static float ComputeSingleLightPower(GpuLight light)
        {
            float luminance = Mathf.Max(light.ColorIntensity.x, Mathf.Max(light.ColorIntensity.y, light.ColorIntensity.z));
            float intensity = Mathf.Max(light.ColorIntensity.w, 0f);

            if (luminance <= 0f || intensity <= 0f)
            {
                return 0f;
            }

            float radiance = luminance * intensity;
            float type = light.DirectionType.w;

            if (Mathf.Approximately(type, LightTypeDirectional))
            {
                return radiance;
            }

            if (Mathf.Approximately(type, LightTypePoint))
            {
                return radiance * 4f * Mathf.PI;
            }

            if (Mathf.Approximately(type, LightTypeSpot))
            {
                float cosOuter = Mathf.Clamp(light.SpotAngles.x, -1f, 1f);
                float solidAngle = 2f * Mathf.PI * Mathf.Max(1f - cosOuter, 0f);

                return radiance * solidAngle;
            }

            if (Mathf.Approximately(type, LightTypeRectangle))
            {
                float halfWidth = Mathf.Max(light.AreaData.x, 0f);
                float halfHeight = Mathf.Max(light.AreaData.y, 0f);
                float area = 4f * halfWidth * halfHeight;

                return radiance * area * Mathf.PI;
            }

            if (Mathf.Approximately(type, LightTypeDisc))
            {
                float radius = Mathf.Max(light.AreaData.z, 0f);
                float area = Mathf.PI * radius * radius;

                return radiance * area * Mathf.PI;
            }

            return radiance;
        }
    }
}
