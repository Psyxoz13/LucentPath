using UnityEngine.UIElements;

namespace LucentPath.EditorUi.RendererSettings
{
    internal class PipelineShadersCard : RendererSettingsEditorCardBase
    {
        public PipelineShadersCard(LucentPathRendererSettingsEditor editor)
            : base(
                editor,
                "Pipeline Shaders",
                "Assigns the DXR ray generation, compute post-processing, and presentation passes used by the active pipeline.")
        { }

        public override void BuildUiToolkit(RendererSettingsEditorContext context, VisualElement parent)
        {
            var asset = Asset;

            if (asset == null)
            {
                return;
            }

            AddObjectField(parent, "PathTracingRayGenShader", () => asset.PathTracingRayGenShader, value => asset.PathTracingRayGenShader = value,
                "DXR ray generation shader that traces, resolves, shades, and advances path samples.");

            AddObjectField(
                parent,
                "Resolve Secondary Guides Compute",
                () => asset.ResolveSecondaryGuidesCompute,
                value => asset.ResolveSecondaryGuidesCompute = value,
                "Builds reflection and transmission temporal guides from secondary path events.");

            AddObjectField(parent, "Accumulate Frame Compute", () => asset.AccumulateFrameCompute, value => asset.AccumulateFrameCompute = value,
                "Accumulates the current frame using temporal history.");

            AddObjectField(parent, "Prepare Buffers Compute", () => asset.PrepareBuffersCompute, value => asset.PrepareBuffersCompute = value,
                "Packs depth, motion vectors, and masks for Unity's NVIDIA DLSS Super Resolution path. Assign LucentPathPass_PrepareBuffers.compute.");

            AddObjectField(parent, "Atrous Filter Compute", () => asset.AtrousFilterCompute, value => asset.AtrousFilterCompute = value,
                "Runs the spatial atrous denoising filter.");

            AddObjectField(parent, "Shade Compute", () => asset.ShadeCompute, value => asset.ShadeCompute = value,
                "Runs the spatial atrous denoising filter.");

            AddObjectField(parent, "Compact Paths Compute", () => asset.CompactPathsCompute, value => asset.CompactPathsCompute = value,
                "NEED TOOLTIP.");

            AddObjectField(parent, "TAA Compute", () => asset.TaaCompute, value => asset.TaaCompute = value,
                "Final output TAA when DLSS is inactive. Assign LucentPath/Shaders/Passes/LucentPathPass_Taa.compute.");

            AddObjectField(parent, "Env Build CDF Compute", () => asset.EnvBuildCdfCompute, value => asset.EnvBuildCdfCompute = value,
                "Builds the environment-light luminance buffer + CDF for importance sampling. Assign LucentPath/Shaders/Lighting/EnvBuildCdf.compute.");

            AddObjectField(
                parent,
                "BSDF Multi-Scatter Bake Compute",
                () => asset.BsdfMultiScatterBakeCompute,
                value => asset.BsdfMultiScatterBakeCompute = value,
                "Bakes GGX directional + average albedo LUTs for Turquin 2019 multi-scatter compensation. Assign LucentPath/Shaders/Bsdf/BsdfMultiScatterBake.compute.");

            AddObjectField(parent, "Composite Shader", () => asset.CompositeShader, value => asset.CompositeShader = value,
                "Fullscreen shader that presents the final path-traced result.");

            AddObjectField(parent, "Bloom Shader", () => asset.BloomShader, value => asset.BloomShader = value,
                "Optional fullscreen shader that applies spectral PSF bloom after DLSS/native resolve and before tonemapping. Defaults to Hidden/LucentPath/Bloom.");

            AddObjectField(parent, "Post Sharpen Shader", () => asset.PostSharpenShader, value => asset.PostSharpenShader = value,
                "Optional fullscreen post-process shader used for sharpening after an upscaler has produced its output.");
        }
    }
}
