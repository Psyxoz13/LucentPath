using LucentPath.EditorUi.Core;
using System;
using System.Collections.Generic;

namespace LucentPath.EditorUi.Materials
{
    internal class MaterialEditorSection : EditorSectionBase<MaterialEditorContext>
    {
        private readonly Func<MaterialEditorContext, bool> _shouldDraw;

        public MaterialEditorSection(
            string title,
            string hint,
            Func<bool> getExpanded,
            Action<bool> setExpanded,
            IReadOnlyList<EditorCardBase<MaterialEditorContext>> cards,
            Func<MaterialEditorContext, bool> shouldDraw = null)
            : base(title, hint, getExpanded, setExpanded, cards)
        {
            _shouldDraw = shouldDraw;
        }

        public override bool ShouldDraw(MaterialEditorContext context)
        {
            return _shouldDraw?.Invoke(context) ?? true;
        }
    }
}
