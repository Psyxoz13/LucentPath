using LucentPath.EditorUi.Core;

namespace LucentPath.EditorUi.RendererSettings
{
    internal class DlssSuperResolutionSection : EditorSectionBase<RendererSettingsEditorContext>
    {
        public DlssSuperResolutionSection(LucentPathRendererSettingsEditor editor)
            : base(
                "DLSS / Streamline",
                "Unity NVIDIA DLSS Super Resolution plus the native Streamline bridge used for DLSS Ray Reconstruction work.",
                () => editor.IsDlssSectionExpanded,
                editor.SetDlssSectionVisibility,
                new EditorCardBase<RendererSettingsEditorContext>[]
                {
                    new StreamlineLibraryCard(editor),
                    new StreamlineRuntimeCard(editor),
                    new DlssRuntimeCard(editor),
                    new DlssResolutionCard(editor),
                    new DlssMotionVectorsCard(editor),
                    new DlssSharpeningCard(editor),
                })
        { }
    }
}
