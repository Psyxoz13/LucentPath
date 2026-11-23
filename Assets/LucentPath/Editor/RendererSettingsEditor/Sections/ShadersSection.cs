using LucentPath.EditorUi.Core;

namespace LucentPath.EditorUi.RendererSettings
{
    internal class ShadersSection : EditorSectionBase<RendererSettingsEditorContext>
    {
        public ShadersSection(LucentPathRendererSettingsEditor editor)
            : base(
                "Shaders",
                "Required compute and composite shader references. If one is missing, the pipeline cannot render correctly.",
                () => editor.IsShadersSectionExpanded,
                editor.SetShadersSectionVisibility,
                new EditorCardBase<RendererSettingsEditorContext>[]
                {
                    new PipelineShadersCard(editor)
                })
        { }
    }
}
