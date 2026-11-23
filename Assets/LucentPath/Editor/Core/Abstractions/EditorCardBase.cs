using UnityEngine;
using UnityEngine.UIElements;

namespace LucentPath.EditorUi.Core
{
    public abstract class EditorCardBase<TContext> where TContext : EditorUiContext
    {
        public string Title { get; }
        public string Hint { get; }
        public virtual RectOffset ImguiPadding => null;

        protected EditorCardBase(string title, string hint)
        {
            Title = title ?? string.Empty;
            Hint = hint ?? string.Empty;
        }

        public virtual bool ShouldDraw(TContext context)
        {
            return true;
        }

        public virtual void BuildUiToolkit(TContext context, VisualElement parent)
        { }

        public abstract void DrawIMGUI(TContext context);
    }
}
