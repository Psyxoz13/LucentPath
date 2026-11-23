using LucentPath.Tools;
using UnityEngine;
using UnityEngine.Rendering;
using static LucentPath.Rendering.PipelineShaderProperties;

namespace LucentPath.Rendering
{
    internal readonly struct CameraLensState
    {
        public float ApertureRadius { get; }
        public float FocusDistance { get; }
        public int BladeCount { get; }
        public float ApertureRotation { get; }
        public float Anamorphism { get; }

        private const float MillimetersToWorldMeters = .001f;

        public CameraLensState(float apertureRadius, float focusDistance, int bladeCount, float apertureRotation, float anamorphism)
        {
            ApertureRadius = apertureRadius;
            FocusDistance = focusDistance;
            BladeCount = bladeCount;
            ApertureRotation = apertureRotation;
            Anamorphism = anamorphism;
        }

        public static CameraLensState Resolve(Camera camera)
        {
            if (camera == null || camera.orthographic || camera.usePhysicalProperties == false)
            {
                return default;
            }

            float fNumber = camera.aperture;
            float focalLengthMm = camera.focalLength;
            float focusDistance = camera.focusDistance;

            if (fNumber <= 0f || focalLengthMm <= 0f || focusDistance <= 0f)
            {
                return default;
            }

            float apertureDiameter = (focalLengthMm / fNumber) * MillimetersToWorldMeters;
            float apertureRadius = Mathf.Max(0f, apertureDiameter * .5f);
            int bladeCount = Mathf.Clamp(camera.bladeCount, 0, Camera.kMaxBladeCount);
            float anamorphism = Mathf.Clamp(camera.anamorphism, -.99f, .99f);

            return new CameraLensState(apertureRadius, focusDistance, bladeCount, 0f, anamorphism);
        }

        public void BindCompute(CommandBuffer cmd, ComputeShader compute)
        {
            cmd.SetComputeFloatParam(compute, CameraApertureRadiusProperty, ApertureRadius);
            cmd.SetComputeFloatParam(compute, CameraFocusDistanceProperty, FocusDistance);
            cmd.SetComputeIntParam(compute, CameraBladeCountProperty, BladeCount);
            cmd.SetComputeFloatParam(compute, CameraApertureRotationProperty, ApertureRotation);
            cmd.SetComputeFloatParam(compute, CameraAnamorphismProperty, Anamorphism);
        }

        public void BindRayTracing(CommandBuffer cmd, RayTracingShader shader)
        {
            cmd.SetRayTracingFloatParam(shader, CameraApertureRadiusProperty, ApertureRadius);
            cmd.SetRayTracingFloatParam(shader, CameraFocusDistanceProperty, FocusDistance);
            cmd.SetRayTracingIntParam(shader, CameraBladeCountProperty, BladeCount);
            cmd.SetRayTracingFloatParam(shader, CameraApertureRotationProperty, ApertureRotation);
            cmd.SetRayTracingFloatParam(shader, CameraAnamorphismProperty, Anamorphism);
        }

        public int AppendHash(int hash)
        {
            hash = HashHelper.AppendHash(hash, ApertureRadius);
            hash = HashHelper.AppendHash(hash, FocusDistance);
            hash = HashHelper.AppendHash(hash, BladeCount);
            hash = HashHelper.AppendHash(hash, ApertureRotation);

            return HashHelper.AppendHash(hash, Anamorphism);
        }
    }
}
