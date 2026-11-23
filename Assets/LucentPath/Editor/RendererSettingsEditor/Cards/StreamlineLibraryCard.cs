using UnityEngine.UIElements;

namespace LucentPath.EditorUi.RendererSettings
{
    internal class StreamlineLibraryCard : RendererSettingsEditorCardBase
    {
        public StreamlineLibraryCard(LucentPathRendererSettingsEditor editor)
            : base(
                editor,
                "Streamline Runtime Library",
                "Controls Streamline plugin discovery, log output, and official initialization flags used by the native bridge.")
        { }

        public override void BuildUiToolkit(RendererSettingsEditorContext context, VisualElement parent)
        {
            var settings = Asset?.StreamlineSettings;

            if (settings == null)
            {
                return;
            }

            AddToggleField(parent, "Enable Streamline", () => settings.IsStreamlineEnabled, value => settings.IsStreamlineEnabled = value,
                "Loads the native NVIDIA Streamline bridge. Unity DLSS Super Resolution does not require this; Streamline is used here for DLSS Ray Reconstruction.");

            RegisterConditionalElement(
                AddTextField(parent, "Runtime Directory", () => settings.RuntimeDirectory, value => settings.RuntimeDirectory = value,
                "Directory containing sl.interposer.dll, sl.common.dll, sl.dlss_d.dll, and nvngx_dlssd.dll. Relative paths are resolved from the Unity project root by the managed bridge."),
                () => settings.IsStreamlineActive);

            RegisterConditionalElement(AddTextField(parent, "Log Directory", () => settings.LogDirectory, value => settings.LogDirectory = value,
                "Directory where Streamline writes sl.log and NGX diagnostic files."),
                () => settings.IsStreamlineActive);

            RegisterConditionalElement(
                AddTextField(parent, "NGX Application ID", () => settings.ApplicationId, value => settings.ApplicationId = value,
                "NVIDIA-provided numeric application id passed to slInit. Required by Streamline for NGX features such as DLSS Ray Reconstruction."),
                () => settings.IsStreamlineActive);

            RegisterConditionalElement(AddTextField(parent, "NGX Project ID", () => settings.ProjectId, value => settings.ProjectId = value,
                "Optional NVIDIA NGX project id. Leave empty unless NVIDIA provides one for the Streamline integration."),
                () => settings.IsStreamlineActive);

            RegisterConditionalElement(
                AddToggleField(parent, "Manual Hooking", () => settings.IsManualHookingUsed, value => settings.IsManualHookingUsed = value,
                "Required for Unity native-plugin integration: the bridge binds Unity's real D3D12 device and command list."),
                () => settings.IsStreamlineActive);

            RegisterConditionalElement(
                AddToggleField(
                    parent,
                    "Frame-Based Resource Tagging",
                    () => settings.IsFrameBasedResourceTaggingUsed,
                    value => settings.IsFrameBasedResourceTaggingUsed = value,
                    "Required by Streamline 2.11 slSetTagForFrame."),
                () => settings.IsStreamlineActive);

            RegisterConditionalElement(
                AddToggleField(
                    parent,
                    "Load Downloaded Plugins",
                    () => settings.IsLoadDownloadedPlugins,
                    value => settings.IsLoadDownloadedPlugins = value,
                    "Allows Streamline to load downloaded NVIDIA plugin updates when available."),
                () => settings.IsStreamlineActive);

            RegisterConditionalElement(
                AddToggleField(
                    parent,
                    "Allow OTA Updates",
                    () => settings.IsOverTheAirUpdatesAllowed,
                    value => settings.IsOverTheAirUpdatesAllowed = value,
                    "Allows Streamline OTA plugin/model updates according to NVIDIA guidance."),
                () => settings.IsStreamlineActive);
        }
    }
}
