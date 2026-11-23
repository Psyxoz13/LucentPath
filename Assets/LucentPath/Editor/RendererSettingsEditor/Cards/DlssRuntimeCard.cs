using LucentPath.Upscale.DLSS;
using UnityEngine.UIElements;

namespace LucentPath.EditorUi.RendererSettings
{
    internal class DlssRuntimeCard : RendererSettingsEditorCardBase
    {
        public DlssRuntimeCard(LucentPathRendererSettingsEditor editor)
            : base(
                editor,
                "DLSS Runtime",
                "Enables DLSS, selects the quality preset, and defines how the pipeline falls back when DLSS is unavailable.")
        { }

        public override void BuildUiToolkit(RendererSettingsEditorContext context, VisualElement parent)
        {
            var settings = Asset?.DlssSettings;

            if (settings == null)
            {
                return;
            }

            AddEnumField(parent, "Quality", () => settings.Quality, value => settings.Quality = value,
                "DLSS quality mode used when the runtime is active. Off skips the DLSS render pass but keeps the integration enabled.");

            RegisterConditionalElement(
                AddToggleField(parent, "Allow Scene View", () => settings.IsSceneViewAllowed, value => settings.IsSceneViewAllowed = value,
                "Allows DLSS to run for Scene View cameras as well as Game View cameras."),
                () => IsDlssRuntimeActive(settings));

            RegisterConditionalElement(
                AddToggleField(
                    parent,
                    "Fallback To Native When Unavailable",
                    () => settings.IsFallbackToNativeWhenUnavailable,
                    value => settings.IsFallbackToNativeWhenUnavailable = value,
                    "Renders at native output resolution when DLSS is unavailable or initialization fails."),
                () => IsDlssRuntimeActive(settings));

            RegisterConditionalElement(AddEnumField(parent, "Model Preset Mode", () => settings.PresetMode, value => settings.PresetMode = value,
                "Selects which DLSS neural model preset is requested from Unity's NVIDIA module. Unsupported presets safely fall back to NVIDIA defaults."),
                () => IsDlssRuntimeActive(settings));

            RegisterConditionalElement(AddEnumField(parent, "Model Preset", () => settings.ModelPreset, value => settings.ModelPreset = value,
                "One DLSS model preset for all quality modes. Preset K is the current transformer quality default where supported."),
                () => IsDlssRuntimeActive(settings) && settings.PresetMode == LucentPathDlssPresetMode.SinglePreset);

            RegisterConditionalElement(AddEnumField(parent, "Quality Preset", () => settings.QualityPreset, value => settings.QualityPreset = value,
                "DLSS model preset requested for Quality mode. Standard DLSS 4 recommendation: Preset K."),
                () => IsDlssRuntimeActive(settings) && settings.PresetMode == LucentPathDlssPresetMode.PerQualityPreset);

            RegisterConditionalElement(
                AddEnumField(parent, "Balanced Preset", () => settings.BalancedPreset, value => settings.BalancedPreset = value,
                "DLSS model preset requested for Balanced mode. Standard DLSS 4 recommendation: Preset K."),
                () => IsDlssRuntimeActive(settings) && settings.PresetMode == LucentPathDlssPresetMode.PerQualityPreset);

            RegisterConditionalElement(
                AddEnumField(parent, "Performance Preset", () => settings.PerformancePreset, value => settings.PerformancePreset = value,
                "DLSS model preset requested for Performance mode. Standard DLSS 4.5 recommendation: Preset M."),
                () => IsDlssRuntimeActive(settings) && settings.PresetMode == LucentPathDlssPresetMode.PerQualityPreset);

            RegisterConditionalElement(
                AddEnumField(
                    parent,
                    "Ultra Performance Preset",
                    () => settings.UltraPerformancePreset,
                    value => settings.UltraPerformancePreset = value,
                    "DLSS model preset requested for Ultra Performance mode. Standard DLSS 4.5 recommendation: Preset L."),
                () => IsDlssRuntimeActive(settings) && settings.PresetMode == LucentPathDlssPresetMode.PerQualityPreset);

            RegisterConditionalElement(AddEnumField(parent, "DLAA Preset", () => settings.DlaaPreset, value => settings.DlaaPreset = value,
                "DLSS model preset requested for DLAA contexts. Standard DLSS 4 recommendation: Preset K."),
                () => IsDlssRuntimeActive(settings) && settings.PresetMode == LucentPathDlssPresetMode.PerQualityPreset);

            RegisterConditionalElement(
                AddToggleField(
                    parent,
                    "Warn When Preset Is Unsupported",
                    () => settings.IsWarnWhenPresetIsUnsupported,
                    value => settings.IsWarnWhenPresetIsUnsupported = value,
                    "Logs one warning when the installed Unity NVIDIA module cannot expose the requested DLSS preset API."),
                () => IsDlssRuntimeActive(settings) && settings.PresetMode != LucentPathDlssPresetMode.NvidiaDefault);
        }
    }
}
