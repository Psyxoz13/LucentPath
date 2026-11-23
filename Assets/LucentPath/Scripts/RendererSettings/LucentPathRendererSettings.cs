using LucentPath.Rendering.Settings;
using LucentPath.Upscale.DLSS;
using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace LucentPath.Rendering
{
    [CreateAssetMenu(menuName = "Rendering/LucentPath Renderer Settings")]
    public class LucentPathRendererSettings : ScriptableObject
    {
        public Action OnValidated;

        [Header("Shaders")]
        public RayTracingShader PathTracingRayGenShader;

        public ComputeShader ResolveSecondaryGuidesCompute;
        public ComputeShader AccumulateFrameCompute;
        public ComputeShader PrepareBuffersCompute;
        public ComputeShader AtrousFilterCompute;
        public ComputeShader ShadeCompute;
        public ComputeShader CompactPathsCompute;
        public ComputeShader TaaCompute;
        public ComputeShader EnvBuildCdfCompute;
        public ComputeShader BsdfMultiScatterBakeCompute;
        public Shader CompositeShader;
        public Shader BloomShader;
        public Shader PostSharpenShader;

        public DlssSettings DlssSettings = new();
        public StreamlineSettings StreamlineSettings = new();
        public PathTracingSettings PathTracingSettings = new();
        public TemporalHistorySettings TemporalHistorySettings = new();
        public TemporalHistoryRejectionSettings TemporalHistoryRejectionSettings = new();
        public TemporalHistoryConfidenceSettings TemporalHistoryConfidenceSettings = new();
        public DisocclusionRecoverySettings DisocclusionRecoverySettings = new();
        public ATrousSettings ATrousSettings = new();
        public FireflySuppressionSettings FireflySuppressionSettings = new();
        public RadianceCacheSettings RadianceCacheSettings = new();
        public CheckerboardSettings CheckerboardSettings = new();
        public TaaSettings TaaSettings = new();
        public SamplerSettings SamplerSettings = new();
        public SubsurfaceSettings SubsurfaceSettings = new();

#if UNITY_EDITOR
        public void ValidateForEditor()
        {
            DlssSettings.Validate();
            StreamlineSettings.Validate();
            OnValidate();
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        }

#endif

        private void OnValidate()
        {
            OnValidated?.Invoke();
        }
    }
}
