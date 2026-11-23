using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace LucentPath.EditorUi.Core
{
    public abstract class EditorSectionBase<TContext> where TContext : EditorUiContext
    {
        public string Title { get; }
        public string Hint { get; }
        public Func<bool> GetExpanded { get; }
        public Action<bool> SetExpanded { get; }
        public IReadOnlyList<EditorCardBase<TContext>> Cards { get; }

        protected EditorSectionBase(string title, string hint, Func<bool> getExpanded, Action<bool> setExpanded, IReadOnlyList<EditorCardBase<TContext>> cards)
        {
            Title = title ?? string.Empty;
            Hint = hint ?? string.Empty;
            GetExpanded = getExpanded ?? (() => true);
            SetExpanded = setExpanded ?? (_ => { });
            Cards = cards ?? Array.Empty<EditorCardBase<TContext>>();
        }

        public virtual bool ShouldDraw(TContext context)
        {
            return true;
        }

        public virtual void BuildUiToolkit(
            TContext context,
            VisualElement parent,
            Action<VisualElement, EditorCardBase<TContext>> cardBuilder)
        {
            var cards = Cards;

            for (var i = 0; i < cards.Count; i++)
            {
                var card = cards[i];

                if (card == null)
                {
                    continue;
                }

                cardBuilder?.Invoke(parent, card);
            }
        }

        public virtual void DrawIMGUI(
            TContext context,
            Action<EditorSectionBase<TContext>, Action> defaultSectionDrawer,
            Action<EditorCardBase<TContext>> defaultCardDrawer)
        {
            if (ShouldDraw(context) == false)
            {
                return;
            }

            defaultSectionDrawer?.Invoke(
                this,
                () =>
            {
                var cards = Cards;

                for (var i = 0; i < cards.Count; i++)
                {
                    var card = cards[i];

                    if (card == null || card.ShouldDraw(context) == false)
                    {
                        continue;
                    }

                    defaultCardDrawer?.Invoke(card);
                }
            });
        }
    }
}
