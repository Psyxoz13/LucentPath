using LucentPath.EditorUi.Core;
using System;
using UnityEditor;

namespace LucentPath.EditorUi.Materials
{
    internal class MaterialEditorContext : EditorUiContext
    {
        public LucentPathDefaultShaderGUI Editor { get; }
        public MaterialEditor MaterialEditor { get; private set; }
        public MaterialProperty[] Properties { get; private set; }
        public MaterialModelKind Model { get; private set; }

        public MaterialEditorContext(LucentPathDefaultShaderGUI editor)
        {
            Editor = editor;
            Properties = Array.Empty<MaterialProperty>();
        }

        public void Update(MaterialEditor materialEditor, MaterialProperty[] properties, MaterialModelKind model)
        {
            MaterialEditor = materialEditor;
            Properties = properties ?? Array.Empty<MaterialProperty>();
            Model = model;
        }

        public override void Repaint()
        {
            MaterialEditor?.Repaint();
        }
    }
}
