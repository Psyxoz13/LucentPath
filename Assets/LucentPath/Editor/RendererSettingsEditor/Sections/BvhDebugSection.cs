using LucentPath.EditorUi.Core;

namespace LucentPath.EditorUi.RendererSettings
{
    internal class DebugSection : EditorSectionBase<RendererSettingsEditorContext>
    {
        public DebugSection(LucentPathRendererSettingsEditor editor)
            : base(
                "Debug",
                "Holds output-level debug visualizations that intentionally bypass the normal presentation path.",
                () => editor.IsDebugSectionExpanded,
                editor.SetDebugSectionVisibility,
                new EditorCardBase<RendererSettingsEditorContext>[]
                {
                    new DebugVisualizationCard(editor)
                })
        { }
    }
}
