using LucentPath.EditorUi.Core;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace LucentPath.EditorUi.RendererSettings
{
    internal class PipelineEditorSection : EditorSectionBase<RendererSettingsEditorContext>
    {
        public PipelineEditorSection(
            string title,
            string hint,
            Func<bool> getExpanded,
            Action<bool> setExpanded,
            IReadOnlyList<EditorCardBase<RendererSettingsEditorContext>> cards)
            : base(title, hint, getExpanded, setExpanded, cards)
        { }
    }

    internal class PipelineEditorCard : EditorCardBase<RendererSettingsEditorContext>
    {
        public override RectOffset ImguiPadding => _pipelineCardPadding;

        private readonly Action<VisualElement, string> _buildUiToolkit;
        private readonly Func<RendererSettingsEditorContext, bool> _shouldDraw;

        private static readonly RectOffset _pipelineCardPadding = new RectOffset(10, 10, 4, 10);

        public PipelineEditorCard(
            string title,
            string hint,
            Action<VisualElement, string> buildUiToolkit,
            Func<RendererSettingsEditorContext, bool> shouldDraw = null)
            : base(title, hint)
        {
            _buildUiToolkit = buildUiToolkit;
            _shouldDraw = shouldDraw;
        }

        public override bool ShouldDraw(RendererSettingsEditorContext context)
        {
            return _shouldDraw?.Invoke(context) ?? true;
        }

        public override void BuildUiToolkit(RendererSettingsEditorContext context, VisualElement parent)
        {
            _buildUiToolkit?.Invoke(parent, Title);
        }

        public override void DrawIMGUI(RendererSettingsEditorContext context)
        { }
    }
}
