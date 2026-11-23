using LucentPath.EditorUi.Core;
using System;
using UnityEngine;

namespace LucentPath.EditorUi.Materials
{
    internal class MaterialEditorCard : DelegateEditorCard<MaterialEditorContext>
    {
        private static readonly RectOffset _shaderCardPadding = new RectOffset(-14, 44, 0, 10);

        public MaterialEditorCard(string title, string hint, Action<MaterialEditorContext> drawImGui)
            : base(title, hint, drawImGui, imguiPadding: _shaderCardPadding)
        { }
    }
}
