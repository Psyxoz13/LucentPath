using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using static LucentPath.Rendering.PipelineShaderProperties;

namespace LucentPath.Rendering
{
    internal static class GpuLightBuilder
    {
        private static readonly List<Light> _orderedLights = new(128);
        private static readonly List<Light> _visibleLights = new(128);
        private static readonly HashSet<EntityId> _acceptedLightIds = new();

        private const float DefaultPointSpotRadius = .05f;

        public static void BuildFromCullingResults(CullingResults cullingResults, int maxLights, out GpuLight[] lights, out int analyticAreaLightCount)
        {
            _visibleLights.Clear();
            _acceptedLightIds.Clear();

            for (var i = 0; i < cullingResults.visibleLights.Length; i++)
            {
                var light = cullingResults.visibleLights[i].light;

                if (light == null || _acceptedLightIds.Add(light.GetEntityId()) == false)
                {
                    continue;
                }

                if (IsSupportedLight(light))
                {
                    _visibleLights.Add(light);
                }
            }

            Build(_visibleLights, maxLights, out lights, out analyticAreaLightCount);
        }

        private static void Build(IList<Light> sourceLights, int maxLights, out GpuLight[] lights, out int analyticAreaLightCount)
        {
            maxLights = Mathf.Max(1, maxLights);

            _orderedLights.Clear();

            for (var i = 0; i < sourceLights.Count; i++)
            {
                var light = sourceLights[i];

                if (IsSupportedLight(light))
                {
                    _orderedLights.Add(light);
                }
            }

            _orderedLights.Sort((a, b) => GetLightWeight(b).CompareTo(GetLightWeight(a)));

            var count = Mathf.Min(maxLights, _orderedLights.Count);

            if (count == 0)
            {
                lights = Array.Empty<GpuLight>();
                analyticAreaLightCount = 0;

                return;
            }

            var areaCount = 0;

            lights = new GpuLight[count];

            for (var i = 0; i < count; i++)
            {
                var light = _orderedLights[i];

                if (IsAnalyticAreaLight(light) && TryCreate(light, out GpuLight gpuLight))
                {
                    lights[areaCount++] = gpuLight;
                }
            }

            analyticAreaLightCount = areaCount;

            var writeIndex = areaCount;

            for (var i = 0; i < count; i++)
            {
                var light = _orderedLights[i];

                if (IsAnalyticAreaLight(light))
                {
                    continue;
                }

                if (TryCreate(light, out GpuLight gpuLight))
                {
                    lights[writeIndex++] = gpuLight;
                }
            }

            if (writeIndex != lights.Length)
            {
                Array.Resize(ref lights, writeIndex);
            }
        }

        private static bool IsSupportedLight(Light light)
        {
            return light != null &&
                light.isActiveAndEnabled &&
                light.intensity > 0f &&
                IsSupportedLightType(light.type);
        }

        private static bool IsSupportedLightType(LightType lightType)
        {
            return lightType == LightType.Directional ||
                lightType == LightType.Point ||
                lightType == LightType.Spot ||
                lightType == LightType.Pyramid ||
                lightType == LightType.Box ||
                lightType == LightType.Rectangle ||
                lightType == LightType.Disc;
        }

        private static bool IsAnalyticAreaLight(Light light)
        {
            var lightType = light.type;

            if (lightType == LightType.Rectangle || lightType == LightType.Disc)
            {
                return true;
            }

            return GetLightRadius(light) > 0f &&
                            (lightType == LightType.Point ||
                             lightType == LightType.Spot ||
                             lightType == LightType.Pyramid ||
                             lightType == LightType.Box);
        }

        private static float GetLightRadius(Light light)
        {
            if (light.TryGetComponent<LucentPathAdditionalData>(out LucentPathAdditionalData data))
            {
                return data.Radius;
            }

            return DefaultPointSpotRadius;
        }

        private static float GetLightWeight(Light light)
        {
            var color = GetLightColor(light) * light.intensity;
            var luminance = Mathf.Max(color.r, Mathf.Max(color.g, color.b));

            return luminance * Mathf.Max(light.range, 1f);
        }

        private static Color GetLightColor(Light light)
        {
            var color = light.color;

            if (light.useColorTemperature)
            {
                color *= Mathf.CorrelatedColorTemperatureToRGB(light.colorTemperature);
            }

            return color;
        }

        private static bool TryCreate(Light light, out GpuLight gpuLight)
        {
            gpuLight = default;

            var color = GetLightColor(light);
            var intensity = light.intensity;
            var position = light.transform.position;
            var forward = light.transform.forward.normalized;
            var right = light.transform.right.normalized;
            var up = light.transform.up.normalized;
            var range = Mathf.Max(light.range, .001f);
            var colorIntensity = new Vector4(color.r, color.g, color.b, intensity);

            if (light.type == LightType.Directional)
            {
                gpuLight = new GpuLight
                {
                    PositionRange = Vector4.zero,
                    DirectionType = new Vector4(-forward.x, -forward.y, -forward.z, LightTypeDirectional),
                    ColorIntensity = colorIntensity,
                    SpotAngles = Vector4.zero,
                    AreaData = Vector4.zero,
                    Right = Vector4.zero,
                    Up = Vector4.zero
                };

                return true;
            }

            if (light.type == LightType.Point)
            {
                gpuLight = new GpuLight
                {
                    PositionRange = new Vector4(position.x, position.y, position.z, range),
                    DirectionType = new Vector4(0f, 0f, 0f, LightTypePoint),
                    ColorIntensity = colorIntensity,
                    SpotAngles = Vector4.zero,
                    AreaData = new Vector4(GetLightRadius(light), 0f, 0f, 0f),
                    Right = Vector4.zero,
                    Up = Vector4.zero
                };

                return true;
            }

            if (light.type == LightType.Spot || light.type == LightType.Pyramid || light.type == LightType.Box)
            {
                var outerCos = Mathf.Cos(light.spotAngle * .5f * Mathf.Deg2Rad);
                var innerAngle = Mathf.Min(light.innerSpotAngle, light.spotAngle);
                var innerCos = Mathf.Cos(innerAngle * .5f * Mathf.Deg2Rad);

                gpuLight = new GpuLight
                {
                    PositionRange = new Vector4(position.x, position.y, position.z, range),
                    DirectionType = new Vector4(forward.x, forward.y, forward.z, LightTypeSpot),
                    ColorIntensity = colorIntensity,
                    SpotAngles = new Vector4(outerCos, innerCos, 0f, 0f),
                    AreaData = new Vector4(GetLightRadius(light), 0f, 0f, 0f),
                    Right = Vector4.zero,
                    Up = Vector4.zero
                };

                return true;
            }

            if (light.type == LightType.Rectangle)
            {
                var halfWidth = Mathf.Max(light.areaSize.x * .5f, .001f);
                var halfHeight = Mathf.Max(light.areaSize.y * .5f, .001f);

                gpuLight = new GpuLight
                {
                    PositionRange = new Vector4(position.x, position.y, position.z, range),
                    DirectionType = new Vector4(forward.x, forward.y, forward.z, LightTypeRectangle),
                    ColorIntensity = colorIntensity,
                    SpotAngles = Vector4.zero,
                    AreaData = new Vector4(halfWidth, halfHeight, 0f, 0f),
                    Right = new Vector4(right.x, right.y, right.z, 0f),
                    Up = new Vector4(up.x, up.y, up.z, 0f)
                };

                return true;
            }

            if (light.type == LightType.Disc)
            {
                var radius = Mathf.Max(light.areaSize.x, .001f);

                gpuLight = new GpuLight
                {
                    PositionRange = new Vector4(position.x, position.y, position.z, range),
                    DirectionType = new Vector4(forward.x, forward.y, forward.z, LightTypeDisc),
                    ColorIntensity = colorIntensity,
                    SpotAngles = Vector4.zero,
                    AreaData = new Vector4(0f, 0f, radius, 0f),
                    Right = new Vector4(right.x, right.y, right.z, 0f),
                    Up = new Vector4(up.x, up.y, up.z, 0f)
                };

                return true;
            }

            return false;
        }
    }
}
