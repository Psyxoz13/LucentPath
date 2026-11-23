using UnityEngine.UIElements;

namespace LucentPath.EditorUi.RendererSettings
{
    internal class StreamlineRuntimeCard : RendererSettingsEditorCardBase
    {
        public StreamlineRuntimeCard(LucentPathRendererSettingsEditor editor)
            : base(
                editor,
                "Ray Reconstruction",
                "Configures NVIDIA Streamline DLSS Ray Reconstruction. DLSS-RR shares the DLSS quality mode and replaces the denoiser/upscaler when all required inputs are available.")
        { }

        public override bool ShouldDraw(RendererSettingsEditorContext context)
        {
            return Asset?.StreamlineSettings != null && Asset.StreamlineSettings.IsStreamlineActive;
        }

        public override void BuildUiToolkit(RendererSettingsEditorContext context, VisualElement parent)
        {
            var settings = Asset?.StreamlineSettings;

            if (settings == null)
            {
                return;
            }

            AddToggleField(
                parent,
                "Enable DLSS Ray Reconstruction",
                () => settings.IsDlssRayReconstructionEnabled,
                value => settings.IsDlssRayReconstructionEnabled = value,
                "Loads Streamline kFeatureDLSS_RR and enables the native render-thread evaluate path when required resources are present.");

            RegisterConditionalElement(
                AddToggleField(parent, "Allow Scene View", () => settings.IsSceneViewAllowed, value => settings.IsSceneViewAllowed = value,
                "Allows Streamline evaluation for Scene View cameras. Keep disabled while validating stability."),
                () => settings.IsRuntimeActive);

            RegisterConditionalElement(
                AddToggleField(
                    parent,
                    "Fallback To DLSS Or Native",
                    () => settings.IsFallbackToUnityDlssOrNative,
                    value => settings.IsFallbackToUnityDlssOrNative = value,
                    "Falls back to Unity DLSS Super Resolution or native presentation when DLSS-RR is unavailable."),
                () => settings.IsRuntimeActive);

            RegisterConditionalElement(
                AddToggleField(parent, "Warn When Unavailable", () => settings.IsWarnWhenUnavailable, value => settings.IsWarnWhenUnavailable = value,
                "Logs Streamline initialization/support-check failures instead of silently falling back."),
                () => settings.IsRuntimeActive);

            RegisterConditionalElement(
                AddToggleField(parent, "Color Buffers HDR", () => settings.IsColorBuffersHDR, value => settings.IsColorBuffersHDR = value,
                "DLSS-RR requires HDR inputs in the NVIDIA programming guide; this is forced on during validation."),
                () => settings.IsRuntimeActive);

            RegisterConditionalElement(
                AddToggleField(
                    parent,
                    "Packed Normal Roughness",
                    () => settings.IsPackedNormalRoughness,
                    value => settings.IsPackedNormalRoughness = value,
                    "Uses kBufferTypeNormalRoughness with linear roughness in alpha, matching the Streamline DLSS-RR guide."),
                () => settings.IsRuntimeActive);

            RegisterConditionalElement(
                AddToggleField(parent, "Alpha Upscaling", () => settings.IsAlphaUpscalingEnabled, value => settings.IsAlphaUpscalingEnabled = value,
                "Enables alpha upscaling in DLSSDOptions. NVIDIA notes this can cost performance and should stay off unless alpha output is needed."),
                () => settings.IsRuntimeActive);

            RegisterConditionalElement(
                AddMinFloatField(parent, "Pre Exposure", () => settings.PreExposure, value => settings.PreExposure = value, .0001f,
                "DLSSDOptions.preExposure."),
                () => settings.IsRuntimeActive);

            RegisterConditionalElement(
                AddMinFloatField(parent, "Exposure Scale", () => settings.ExposureScale, value => settings.ExposureScale = value, .0001f,
                "DLSSDOptions.exposureScale."),
                () => settings.IsRuntimeActive);

            RegisterConditionalElement(AddEnumField(parent, "Quality Preset", () => settings.QualityPreset, value => settings.QualityPreset = value,
                "DLSS-RR model preset for Quality mode. Streamline 2.11 exposes Default, D, and E."),
                () => settings.IsRuntimeActive);

            RegisterConditionalElement(
                AddEnumField(parent, "Balanced Preset", () => settings.BalancedPreset, value => settings.BalancedPreset = value,
                "DLSS-RR model preset for Balanced mode."),
                () => settings.IsRuntimeActive);

            RegisterConditionalElement(
                AddEnumField(parent, "Performance Preset", () => settings.PerformancePreset, value => settings.PerformancePreset = value,
                "DLSS-RR model preset for Performance mode."),
                () => settings.IsRuntimeActive);

            RegisterConditionalElement(
                AddEnumField(
                    parent,
                    "Ultra Performance Preset",
                    () => settings.UltraPerformancePreset,
                    value => settings.UltraPerformancePreset = value,
                    "DLSS-RR model preset for Ultra Performance mode."),
                () => settings.IsRuntimeActive);

            RegisterConditionalElement(AddEnumField(parent, "DLAA Preset", () => settings.DlaaPreset, value => settings.DlaaPreset = value,
                "DLSS-RR model preset for DLAA mode."),
                () => settings.IsRuntimeActive);
        }
    }
}
