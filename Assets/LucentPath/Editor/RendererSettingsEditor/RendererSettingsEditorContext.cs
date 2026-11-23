using LucentPath.EditorUi.Core;
using LucentPath.Rendering;

namespace LucentPath.EditorUi.RendererSettings
{
    internal class RendererSettingsEditorContext : EditorUiContext
    {
        public LucentPathRendererSettingsEditor Editor { get; }
        public LucentPathRendererSettings Asset => Editor.Asset;

        public RendererSettingsEditorContext(LucentPathRendererSettingsEditor editor)
        {
            Editor = editor;
        }

        public override void Repaint()
        {
            Editor.Repaint();
        }
    }
}
