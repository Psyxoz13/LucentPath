using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace LucentPath.EditorUi.Core
{
    public class DelegateEditorCard<TContext> : EditorCardBase<TContext> where TContext : EditorUiContext
    {
        public override RectOffset ImguiPadding => _imguiPadding ?? base.ImguiPadding;

        private readonly Action<TContext> _drawImGui;
        private readonly Action<TContext, VisualElement> _buildUiToolkit;
        private readonly Func<TContext, bool> _shouldDraw;
        private readonly RectOffset _imguiPadding;

        public DelegateEditorCard(
            string title,
            string hint,
            Action<TContext> drawImGui,
            Action<TContext, VisualElement> buildUiToolkit = null,
            Func<TContext, bool> shouldDraw = null,
            RectOffset imguiPadding = null)
            : base(title, hint)
        {
            _drawImGui = drawImGui;
            _buildUiToolkit = buildUiToolkit;
            _shouldDraw = shouldDraw;
            _imguiPadding = imguiPadding;
        }

        public override bool ShouldDraw(TContext context)
        {
            return _shouldDraw?.Invoke(context) ?? base.ShouldDraw(context);
        }

        public override void BuildUiToolkit(TContext context, VisualElement parent)
        {
            _buildUiToolkit?.Invoke(context, parent);
        }

        public override void DrawIMGUI(TContext context)
        {
            _drawImGui?.Invoke(context);
        }
    }
}
