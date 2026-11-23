using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LucentPath.EditorUi.Core
{
    internal class EditorUiToolkitView
    {
        private const float DefaultMinFieldLabelWidth = 92f;
        private const float DefaultNumericFieldWidth = 50f;
        private const float FieldRowBottomSpacing = 1f;
        private const float FieldLabelVerticalOffset = -3f;
        private const float NumericInputVerticalOffset = -1f;
        private const float SliderInputVerticalOffset = 1f;
        private const float ToggleControlVerticalOffset = -1f;
        private const float ToggleFieldLabelVerticalOffset = FieldLabelVerticalOffset + 1f;
        private const float SectionHeaderHintVerticalOffset = 2f;
        private const float CardTitleBrightnessMultiplier = .95f;
        private const float FieldLabelBrightnessMultiplier = .9f;
        private const string SliderHoverCallbacksClass = "path-tracing-slider-hover-callbacks";
        private const string SliderHoverActiveClass = "path-tracing-slider-hover-active";
        private const string SliderHoverDotName = "path-tracing-slider-hover-dot";
        private static readonly Color _proInputBackgroundColor = new Color32(65, 69, 78, 255);
        private static readonly Color _proInputBorderTopColor = new Color32(48, 48, 48, 255);
        private static readonly Color _proInputBorderBottomColor = new Color32(36, 36, 36, 255);
        private static readonly Color _proSliderTrackColor = new Color32(72, 76, 81, 255);
        private static readonly Color _proSliderThumbColor = new Color32(134, 143, 152, 255);
        private static readonly Color _proSliderRestingDotColor = new Color32(114, 122, 139, 255);
        private static readonly Color _proSliderHoverDotColor = new Color32(174, 185, 216, 255);

        private readonly Action _prepare;
        private readonly Action _repaint;
        private readonly Func<UnityEngine.Object> _undoTarget;
        private readonly List<Action> _refreshActions = new List<Action>();
        private readonly List<IMGUIContainer> _visualizationContainers = new List<IMGUIContainer>();
        private readonly Dictionary<string, List<Label>> _fieldLabelsByCard = new Dictionary<string, List<Label>>();
        private readonly Dictionary<string, float> _fieldCardLabelWidths = new Dictionary<string, float>();

        private int _sliderUndoGroup = -1;

        public EditorUiToolkitView(EditorChrome chrome, Action prepare, Action repaint, Func<UnityEngine.Object> undoTarget = null)
        {
            Chrome = chrome ?? throw new ArgumentNullException(nameof(chrome));
            _prepare = prepare;
            _repaint = repaint;
            _undoTarget = undoTarget;
        }

        public EditorChrome Chrome { get; }
        public bool IsRefreshing { get; private set; }
        public float MinFieldLabelWidth { get; set; } = DefaultMinFieldLabelWidth;
        public float NumericFieldWidth { get; set; } = DefaultNumericFieldWidth;

        public void Reset()
        {
            _refreshActions.Clear();
            _visualizationContainers.Clear();
            _fieldLabelsByCard.Clear();
            _fieldCardLabelWidths.Clear();
            EndSliderUndoGroup();
        }

        public VisualElement CreateRoot()
        {
            var root = new VisualElement();

            root.style.flexGrow = 1f;
            root.style.marginLeft = 0f;
            root.style.marginTop = 0f;
            root.style.marginRight = 0f;
            root.style.marginBottom = 0f;
            root.style.paddingLeft = 0f;
            root.style.paddingTop = 0f;
            root.style.paddingRight = 0f;
            root.style.paddingBottom = 0f;
            root.style.backgroundColor = Chrome.Palette.BackgroundColor;

            return root;
        }

        public VisualElement CreateHeader(string title, string subtitle, Action<VisualElement> buildTrailing = null)
        {
            return CreateHeader(title, subtitle, out _, out _, buildTrailing);
        }

        public VisualElement CreateHeader(
            string title,
            string subtitle,
            out Label titleLabel,
            out Label subtitleLabel,
            Action<VisualElement> buildTrailing = null)
        {
            var header = new VisualElement();

            header.style.minHeight = Chrome.Metrics.HeaderHeight;
            header.style.marginBottom = Chrome.Metrics.HeaderBottomSpacing;
            header.style.paddingLeft = 16f;
            header.style.paddingRight = 16f;
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.backgroundColor = Chrome.Palette.PanelColor;

            var textColumn = new VisualElement();

            textColumn.style.flexGrow = 1f;
            textColumn.style.flexShrink = 1f;
            textColumn.style.minWidth = 0f;
            textColumn.style.marginRight = 16f;

            titleLabel = new Label(title ?? string.Empty);
            titleLabel.style.fontSize = 18;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = Chrome.Palette.TextColor;
            titleLabel.style.marginBottom = 4f;

            subtitleLabel = new Label(subtitle ?? string.Empty);
            subtitleLabel.style.fontSize = 11;
            subtitleLabel.style.whiteSpace = WhiteSpace.Normal;
            subtitleLabel.style.color = Chrome.Palette.MutedTextColor;

            textColumn.Add(titleLabel);
            textColumn.Add(subtitleLabel);
            header.Add(textColumn);

            if (buildTrailing != null)
            {
                var trailing = new VisualElement();

                trailing.style.flexShrink = 0f;

                buildTrailing(trailing);
                header.Add(trailing);
            }

            return header;
        }

        public VisualElement CreateSectionElement<TContext>(
            EditorSectionBase<TContext> section,
            TContext context,
            Action<VisualElement, EditorCardBase<TContext>> buildCard)
            where TContext : EditorUiContext
        {
            if (section == null)
            {
                return null;
            }

            var sectionElement = new VisualElement();

            sectionElement.style.marginLeft = 0f;
            sectionElement.style.marginTop = 0f;
            sectionElement.style.marginRight = 0f;
            sectionElement.style.marginBottom = Chrome.Metrics.SectionBottomSpacing;
            sectionElement.style.paddingLeft = 0f;
            sectionElement.style.paddingTop = 0f;
            sectionElement.style.paddingRight = 0f;
            sectionElement.style.paddingBottom = section.GetExpanded() ? 2f : 0f;
            sectionElement.style.backgroundColor = Chrome.Palette.PanelColor;

            var content = new VisualElement();

            content.style.marginLeft = 0f;
            content.style.marginTop = 0f;
            content.style.marginRight = 0f;
            content.style.marginBottom = 0f;
            content.style.paddingLeft = Chrome.Metrics.SectionContentPaddingLeft;
            content.style.paddingTop = Chrome.Metrics.SectionBottomSpacing;
            content.style.paddingRight = Chrome.Metrics.SectionContentPaddingRight;
            content.style.paddingBottom = 0f;
            content.style.display = section.GetExpanded() ? DisplayStyle.Flex : DisplayStyle.None;

            var header = CreateSectionHeaderElement(section, sectionElement, content);

            sectionElement.Add(header);
            section.BuildUiToolkit(context, content, buildCard);
            sectionElement.Add(content);
            RegisterConditionalElement(sectionElement, () => section.ShouldDraw(context));

            return sectionElement;
        }

        public VisualElement CreateCardElement<TContext>(
            EditorCardBase<TContext> card,
            TContext context,
            Action<VisualElement, EditorCardBase<TContext>> buildContent = null)
            where TContext : EditorUiContext
        {
            if (card == null)
            {
                return null;
            }

            var cardElement = new VisualElement();

            cardElement.style.marginLeft = 0f;
            cardElement.style.marginTop = 0f;
            cardElement.style.marginRight = 0f;
            cardElement.style.marginBottom = Chrome.Metrics.CardBottomSpacing;
            cardElement.style.paddingLeft = 0f;
            cardElement.style.paddingTop = 0f;
            cardElement.style.paddingRight = 0f;
            cardElement.style.paddingBottom = 0f;
            cardElement.style.backgroundColor = Chrome.Palette.CardColor;
            cardElement.style.borderTopLeftRadius = Chrome.Metrics.CardCornerRadius;
            cardElement.style.borderTopRightRadius = Chrome.Metrics.CardCornerRadius;
            cardElement.style.borderBottomLeftRadius = Chrome.Metrics.CardCornerRadius;
            cardElement.style.borderBottomRightRadius = Chrome.Metrics.CardCornerRadius;

            var headerPadding = Chrome.Metrics.CardPadding;
            var contentPadding = card.ImguiPadding ?? Chrome.Metrics.CardPadding;

            var titleLabel = new Label(card.Title);

            titleLabel.style.marginLeft = headerPadding.left;
            titleLabel.style.marginTop = headerPadding.top;
            titleLabel.style.marginRight = headerPadding.right;
            titleLabel.style.marginBottom = 0f;
            titleLabel.style.paddingTop = 0f;
            titleLabel.style.paddingBottom = 0f;
            titleLabel.style.height = 16f;
            titleLabel.style.fontSize = 12;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            titleLabel.style.color = ResolveCardTitleTextColor();

            var content = new VisualElement();

            content.style.paddingLeft = contentPadding.left;
            content.style.paddingTop = contentPadding.top;
            content.style.paddingRight = contentPadding.right;
            content.style.paddingBottom = contentPadding.bottom;

            cardElement.Add(titleLabel);
            cardElement.Add(content);

            if (buildContent != null)
            {
                buildContent.Invoke(content, card);
            }
            else
            {
                card.BuildUiToolkit(context, content);
            }

            RegisterConditionalElement(cardElement, () => card.ShouldDraw(context));

            return cardElement;
        }

        public IMGUIContainer CreateImGuiContainer(Action draw)
        {
            var container = new IMGUIContainer(
                () =>
            {
                _prepare?.Invoke();
                draw?.Invoke();
            });

            container.style.flexGrow = 0f;
            container.style.flexShrink = 0f;
            container.style.marginLeft = 0f;
            container.style.marginTop = 0f;
            container.style.marginRight = 0f;
            container.style.marginBottom = 0f;
            container.style.paddingLeft = 0f;
            container.style.paddingTop = 0f;
            container.style.paddingRight = 0f;
            container.style.paddingBottom = 0f;

            return container;
        }

        public IMGUIContainer CreateVisualizationImGuiContainer(Action draw)
        {
            var container = CreateImGuiContainer(
                () =>
            {
                draw?.Invoke();
            });

            container.RegisterCallback<DetachFromPanelEvent>(_ => _visualizationContainers.Remove(container));
            _visualizationContainers.Add(container);

            return container;
        }

        public VisualElement CreateFieldRow(string cardTitle, string label, VisualElement control, string tooltip, bool compactControl = false)
        {
            return CreateFieldRow(cardTitle, label, control, tooltip, compactControl, out _);
        }

        public VisualElement CreateFieldRow(string cardTitle, string label, VisualElement control, string tooltip, bool compactControl, out Label labelElement)
        {
            var row = new VisualElement() { tooltip = tooltip ?? string.Empty };

            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.minHeight = 22f;
            row.style.marginBottom = FieldRowBottomSpacing;
            row.style.flexGrow = 1f;
            row.style.minWidth = 0f;

            labelElement = new Label(label);
            labelElement.tooltip = tooltip ?? string.Empty;
            labelElement.style.minWidth = MinFieldLabelWidth;
            labelElement.style.flexShrink = 1f;
            labelElement.style.marginRight = 8f;
            labelElement.style.unityTextAlign = TextAnchor.MiddleLeft;
            labelElement.style.color = ResolveFieldLabelTextColor();
            labelElement.style.translate = new Translate(0f, FieldLabelVerticalOffset);

            RegisterFieldLabel(cardTitle, label, labelElement);

            var controlArea = new VisualElement();

            controlArea.style.flexDirection = FlexDirection.Row;
            controlArea.style.alignItems = Align.Center;
            controlArea.style.flexGrow = 1f;
            controlArea.style.flexShrink = 1f;
            controlArea.style.minWidth = 0f;

            control.tooltip = tooltip ?? string.Empty;

            if (compactControl)
            {
                var spacer = new VisualElement();

                spacer.style.flexGrow = 1f;
                spacer.style.minWidth = 0f;

                controlArea.Add(spacer);

                control.style.width = NumericFieldWidth;
                control.style.flexGrow = 0f;
                control.style.flexShrink = 0f;
            }
            else
            {
                control.style.flexGrow = 1f;
                control.style.flexShrink = 1f;
                control.style.minWidth = 0f;
            }

            controlArea.Add(control);
            row.Add(labelElement);
            row.Add(controlArea);

            return row;
        }

        public VisualElement CreateNativeFieldRow<TValue>(string cardTitle, string label, BaseField<TValue> field, string tooltip, bool compactControl = false)
        {
            return CreateNativeFieldRow(cardTitle, label, field, tooltip, compactControl, out _);
        }

        public VisualElement CreateNativeFieldRow<TValue>(string cardTitle, string label, BaseField<TValue> field, string tooltip, bool compactControl, out Label labelElement)
        {
            field.label = label;
            field.tooltip = tooltip ?? string.Empty;
            field.style.flexDirection = FlexDirection.Row;
            field.style.alignItems = Align.Center;
            field.style.justifyContent = compactControl ? Justify.SpaceBetween : Justify.FlexStart;
            field.style.minHeight = 22f;
            field.style.marginLeft = 0f;
            field.style.marginTop = 0f;
            field.style.marginRight = 0f;
            field.style.marginBottom = FieldRowBottomSpacing;
            field.style.flexGrow = 1f;
            field.style.flexShrink = 1f;
            field.style.minWidth = 0f;

            labelElement = field.labelElement;

            if (labelElement != null)
            {
                labelElement.tooltip = tooltip ?? string.Empty;
                labelElement.pickingMode = PickingMode.Position;
                labelElement.style.minWidth = MinFieldLabelWidth;
                labelElement.style.flexShrink = 1f;
                labelElement.style.marginRight = 8f;
                labelElement.style.unityTextAlign = TextAnchor.MiddleLeft;
                labelElement.style.color = ResolveFieldLabelTextColor();
                labelElement.style.translate = new Translate(0f, FieldLabelVerticalOffset);

                RegisterFieldLabel(cardTitle, label, labelElement);
            }

            void ApplyInputAreaLayout()
            {
                var input = FindFieldInput(field);

                if (input == null)
                {
                    return;
                }

                input.tooltip = tooltip ?? string.Empty;
                input.style.alignSelf = Align.Center;
                input.style.marginLeft = 0f;
                input.style.marginTop = 0f;
                input.style.marginRight = 0f;
                input.style.marginBottom = 0f;

                ApplySliderInputVerticalOffset(field, input);

                if (compactControl)
                {
                    input.style.width = NumericFieldWidth;
                    input.style.minWidth = NumericFieldWidth;
                    input.style.maxWidth = NumericFieldWidth;
                    input.style.flexGrow = 0f;
                    input.style.flexShrink = 0f;

                    return;
                }

                input.style.flexGrow = 1f;
                input.style.flexShrink = 1f;
                input.style.minWidth = 0f;
            }

            ApplyInputAreaLayout();
            field.schedule.Execute(ApplyInputAreaLayout);
            field.RegisterCallback<GeometryChangedEvent>(_ => ApplyInputAreaLayout());

            return field;
        }

        public void ConfigureControl(VisualElement control, bool compact)
        {
            control.style.marginLeft = 0f;
            control.style.marginTop = 0f;
            control.style.marginRight = 0f;
            control.style.marginBottom = 0f;
            control.style.minHeight = 20f;

            if (compact)
            {
                control.style.maxWidth = NumericFieldWidth;
            }
        }

        public VisualElement CreateToggleControl(bool initialValue, Action<bool> onValueChanged, out Action<bool> setValueWithoutNotify, out Action toggleValue)
        {
            var control = new VisualElement
            {
                focusable = true,
                tabIndex = 0
            };

            var checkmark = new Label("\u2713");
            bool currentValue = initialValue;
            var hovered = false;
            var focused = false;

            ConfigureToggleControlSize(control);

            control.style.translate = new Translate(0f, ToggleControlVerticalOffset);
            control.style.alignItems = Align.Center;
            control.style.justifyContent = Justify.Center;
            control.style.borderTopWidth = 1f;
            control.style.borderRightWidth = 1f;
            control.style.borderBottomWidth = 1f;
            control.style.borderLeftWidth = 1f;
            control.style.borderTopLeftRadius = 3f;
            control.style.borderTopRightRadius = 3f;
            control.style.borderBottomLeftRadius = 3f;
            control.style.borderBottomRightRadius = 3f;
            control.pickingMode = PickingMode.Position;

            checkmark.style.width = 16f;
            checkmark.style.height = 16f;
            checkmark.style.unityTextAlign = TextAnchor.MiddleCenter;
            checkmark.style.fontSize = 13f;
            checkmark.style.unityFontStyleAndWeight = FontStyle.Bold;
            checkmark.style.marginLeft = 0f;
            checkmark.style.marginTop = -1f;
            checkmark.style.marginRight = 0f;
            checkmark.style.marginBottom = 0f;
            checkmark.style.translate = new Translate(.5f, -1f);
            checkmark.pickingMode = PickingMode.Ignore;

            control.Add(checkmark);

            void ApplyVisualState()
            {
                var background = ResolveInputBackgroundColor();

                if (hovered || focused)
                {
                    background = Color.Lerp(background, ResolveInputTextColor(), EditorGUIUtility.isProSkin ? .08f : .12f);
                }

                ApplyInputBoxStyle(control, background, false);

                checkmark.style.color = ResolveInputTextColor();
                checkmark.style.display = currentValue ? DisplayStyle.Flex : DisplayStyle.None;
            }

            void SetValue(bool value, bool notify)
            {
                if (currentValue == value)
                {
                    return;
                }

                currentValue = value;

                ApplyVisualState();

                if (notify)
                {
                    onValueChanged?.Invoke(value);
                }
            }

            Action invokeToggle = () => SetValue(currentValue == false, true);

            setValueWithoutNotify = value => SetValue(value, false);
            toggleValue = invokeToggle;

            control.RegisterCallback<MouseDownEvent>(
                evt =>
            {
                if (evt.button != 0)
                {
                    return;
                }

                control.Focus();
                evt.StopImmediatePropagation();
            });

            control.RegisterCallback<MouseUpEvent>(
                evt =>
            {
                if (evt.button != 0)
                {
                    return;
                }

                invokeToggle();
                evt.StopImmediatePropagation();
            });

            control.RegisterCallback<KeyDownEvent>(
                evt =>
            {
                if (evt.keyCode != KeyCode.Space &&
                    evt.keyCode != KeyCode.Return &&
                    evt.keyCode != KeyCode.KeypadEnter)
                {
                    return;
                }

                invokeToggle();
                evt.StopImmediatePropagation();
            });

            control.RegisterCallback<PointerEnterEvent>(
                _ =>
            {
                hovered = true;

                ApplyVisualState();
            });

            control.RegisterCallback<PointerLeaveEvent>(
                _ =>
            {
                hovered = false;

                ApplyVisualState();
            });

            control.RegisterCallback<FocusInEvent>(
                _ =>
            {
                focused = true;

                ApplyVisualState();
            });

            control.RegisterCallback<FocusOutEvent>(
                _ =>
            {
                focused = false;

                ApplyVisualState();
            });

            ApplyVisualState();

            return control;
        }

        public static void RegisterToggleLabelClick(Label labelElement, VisualElement control, Action toggleValue)
        {
            if (labelElement == null || toggleValue == null)
            {
                return;
            }

            labelElement.pickingMode = PickingMode.Position;

            labelElement.RegisterCallback<MouseDownEvent>(
                evt =>
            {
                if (evt.button != 0)
                {
                    return;
                }

                control?.Focus();
                evt.StopImmediatePropagation();
            });

            labelElement.RegisterCallback<MouseUpEvent>(
                evt =>
            {
                if (evt.button != 0)
                {
                    return;
                }

                toggleValue();
                evt.StopImmediatePropagation();
            });
        }

        public static void ConfigureToggleLabelOffset(Label labelElement)
        {
            if (labelElement == null)
            {
                return;
            }

            labelElement.style.translate = new Translate(0f, ToggleFieldLabelVerticalOffset);
        }

        public static void ConfigureToggleControlSize(VisualElement toggle)
        {
            if (toggle == null)
            {
                return;
            }

            const float toggleSize = 16f;

            toggle.style.width = toggleSize;
            toggle.style.minWidth = toggleSize;
            toggle.style.maxWidth = toggleSize;
            toggle.style.height = toggleSize;
            toggle.style.minHeight = toggleSize;
            toggle.style.maxHeight = toggleSize;
            toggle.style.flexGrow = 0f;
            toggle.style.flexShrink = 0f;
            toggle.style.alignSelf = Align.Center;
        }

        public void ApplyInputStyling(VisualElement field, bool centerText, bool numericInput, bool tintBaseInput)
        {
            if (field == null)
            {
                return;
            }

            ApplyInputStylingNow(field, centerText, numericInput, tintBaseInput);
            field.schedule.Execute(() => ApplyInputStylingNow(field, centerText, numericInput, tintBaseInput));
            field.RegisterCallback<GeometryChangedEvent>(_ => ApplyInputStylingNow(field, centerText, numericInput, tintBaseInput));
        }

        public void RegisterSliderUndoGroup(VisualElement field, string label)
        {
            if (field == null)
            {
                return;
            }

            field.RegisterCallback<PointerDownEvent>(
                evt =>
            {
                if (evt.button == 0)
                {
                    BeginSliderUndoGroup(label);
                }
            },
                TrickleDown.TrickleDown);

            field.RegisterCallback<PointerUpEvent>(_ => EndSliderUndoGroup(), TrickleDown.TrickleDown);
            field.RegisterCallback<PointerCancelEvent>(_ => EndSliderUndoGroup(), TrickleDown.TrickleDown);
            field.RegisterCallback<PointerCaptureOutEvent>(_ => EndSliderUndoGroup());

            field.RegisterCallback<MouseDownEvent>(
                evt =>
            {
                if (evt.button == 0)
                {
                    BeginSliderUndoGroup(label);
                }
            },
                TrickleDown.TrickleDown);

            field.RegisterCallback<MouseUpEvent>(_ => EndSliderUndoGroup(), TrickleDown.TrickleDown);
            field.RegisterCallback<MouseCaptureOutEvent>(_ => EndSliderUndoGroup());
        }

        public void RegisterConditionalElement(VisualElement element, Func<bool> isVisible)
        {
            if (element == null || isVisible == null)
            {
                return;
            }

            RegisterRefresh(
                () =>
            {
                element.style.display = isVisible() ? DisplayStyle.Flex : DisplayStyle.None;

                RecalculateCardLabelWidths();
            });
        }

        public void RegisterRefresh(Action refresh)
        {
            if (refresh == null)
            {
                return;
            }

            _refreshActions.Add(refresh);
            refresh();
        }

        public void Refresh(Action beforeRefresh = null)
        {
            if (IsRefreshing)
            {
                return;
            }

            IsRefreshing = true;

            try
            {
                beforeRefresh?.Invoke();

                for (var i = 0; i < _refreshActions.Count; i++)
                {
                    _refreshActions[i]?.Invoke();
                }

                for (var i = _visualizationContainers.Count - 1; i >= 0; i--)
                {
                    var container = _visualizationContainers[i];

                    if (container == null)
                    {
                        _visualizationContainers.RemoveAt(i);
                        continue;
                    }

                    container.MarkDirtyRepaint();
                }
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        private VisualElement CreateSectionHeaderElement<TContext>(EditorSectionBase<TContext> section, VisualElement sectionElement, VisualElement content)
            where TContext : EditorUiContext
        {
            var header = new VisualElement();

            header.style.height = Chrome.Metrics.SectionHeaderHeight;
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.backgroundColor = Chrome.Palette.PanelSoftColor;
            header.style.paddingLeft = 12f;
            header.style.paddingRight = 12f;

            var arrow = new Label();

            arrow.style.width = 22f;
            arrow.style.unityTextAlign = TextAnchor.MiddleLeft;
            arrow.style.color = Chrome.Palette.TextColor;

            var textColumn = new VisualElement();

            textColumn.style.flexGrow = 1f;
            textColumn.style.flexShrink = 1f;

            var titleLabel = new Label(section.Title);

            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.fontSize = 12;
            titleLabel.style.color = Chrome.Palette.TextColor;

            var hintLabel = new Label(section.Hint);

            hintLabel.style.fontSize = 10;
            hintLabel.style.color = Chrome.Palette.MutedTextColor;
            hintLabel.style.translate = new Translate(0f, SectionHeaderHintVerticalOffset);

            textColumn.Add(titleLabel);
            textColumn.Add(hintLabel);
            header.Add(arrow);
            header.Add(textColumn);

            void RefreshHeader()
            {
                var visible = section.GetExpanded();

                arrow.text = visible ? "▼" : "▶";
                sectionElement.style.paddingBottom = visible ? 2f : 0f;
                content.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }

            header.RegisterCallback<ClickEvent>(
                _ =>
            {
                section.SetExpanded(section.GetExpanded() == false);
                RefreshHeader();
                Refresh();
            });

            RegisterRefresh(RefreshHeader);

            return header;
        }

        private static VisualElement FindFieldInput(VisualElement field)
        {
            return field.Q<VisualElement>(className: "unity-base-field__input") ??
                   field.Q<VisualElement>(className: "unity-popup-field__input") ??
                   field.Q<VisualElement>(className: "unity-object-field__input") ??
                   field.Q<VisualElement>(className: "unity-color-field__input");
        }

        private void RegisterFieldLabel(string cardTitle, string label, Label labelElement)
        {
            if (labelElement == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(cardTitle))
            {
                ApplyLabelWidth(labelElement, ResolveFieldLabelWidth(label));

                return;
            }

            if (_fieldLabelsByCard.TryGetValue(cardTitle, out List<Label> labels) == false)
            {
                labels = new List<Label>();
                _fieldLabelsByCard[cardTitle] = labels;
            }

            labels.Add(labelElement);
            RecalculateCardLabelWidth(cardTitle);
        }

        private float ResolveFieldLabelWidth(string label)
        {
            var style = Chrome.FieldLabelStyle ?? EditorStyles.label;
            var measuredWidth = style.CalcSize(new GUIContent(label ?? string.Empty)).x + 26f;

            return Mathf.Max(measuredWidth, MinFieldLabelWidth);
        }

        private void ApplyCardLabelWidth(string cardTitle, float width)
        {
            if (_fieldLabelsByCard.TryGetValue(cardTitle, out List<Label> labels) == false)
            {
                return;
            }

            for (var i = 0; i < labels.Count; i++)
            {
                ApplyLabelWidth(labels[i], width);
            }
        }

        private void RecalculateCardLabelWidth(string cardTitle)
        {
            if (string.IsNullOrEmpty(cardTitle) ||
                _fieldLabelsByCard.TryGetValue(cardTitle, out List<Label> labels) == false)
            {
                return;
            }

            var resolvedWidth = MinFieldLabelWidth;

            for (var i = 0; i < labels.Count; i++)
            {
                var labelElement = labels[i];

                if (labelElement == null || IsFieldLabelVisible(labelElement) == false)
                {
                    continue;
                }

                resolvedWidth = Mathf.Max(resolvedWidth, ResolveFieldLabelWidth(labelElement.text));
            }

            if (_fieldCardLabelWidths.TryGetValue(cardTitle, out float cachedWidth) == false ||
                Mathf.Approximately(cachedWidth, resolvedWidth) == false)
            {
                _fieldCardLabelWidths[cardTitle] = resolvedWidth;

                _repaint?.Invoke();
            }

            ApplyCardLabelWidth(cardTitle, resolvedWidth);
        }

        private void RecalculateCardLabelWidths()
        {
            foreach (var cardTitle in _fieldLabelsByCard.Keys)
            {
                RecalculateCardLabelWidth(cardTitle);
            }
        }

        private static bool IsFieldLabelVisible(VisualElement element)
        {
            for (var current = element; current != null; current = current.parent)
            {
                if (current.style.display.value == DisplayStyle.None ||
                    current.resolvedStyle.display == DisplayStyle.None)
                {
                    return false;
                }
            }

            return true;
        }

        private void ApplyLabelWidth(Label labelElement, float width)
        {
            if (labelElement == null)
            {
                return;
            }

            labelElement.style.width = width;
            labelElement.style.minWidth = MinFieldLabelWidth;
        }

        private void ApplyInputStylingNow(VisualElement field, bool centerText, bool numericInput, bool tintBaseInput)
        {
            if (field == null)
            {
                return;
            }

            if (numericInput)
            {
                ApplyNumericFieldWidth(field);
                ApplyNumericInputVerticalOffset(field);
                field.Query<IntegerField>().ForEach(ApplyNumericFieldWidth);
                field.Query<FloatField>().ForEach(ApplyNumericFieldWidth);
                field.Query<IntegerField>().ForEach(ApplyNumericInputVerticalOffset);
                field.Query<FloatField>().ForEach(ApplyNumericInputVerticalOffset);
            }

            ApplyInputElementStyle(field, centerText, tintBaseInput, numericInput);
            field.Query<VisualElement>().ForEach(element => ApplyInputElementStyle(element, centerText, tintBaseInput, numericInput));
            ApplySliderStyle(field);

            if (centerText == false)
            {
                return;
            }

            if (field is TextElement rootText && field is not Label)
            {
                rootText.style.unityTextAlign = TextAnchor.MiddleCenter;
            }

            field.Query<TextElement>().ForEach(
                textElement =>
            {
                if (textElement is Label)
                {
                    return;
                }

                textElement.style.unityTextAlign = TextAnchor.MiddleCenter;

                if (numericInput)
                {
                    textElement.style.color = ResolveInputTextColor(true);
                }
            });
        }

        private void ApplyNumericFieldWidth(VisualElement field)
        {
            if (field is not IntegerField && field is not FloatField)
            {
                return;
            }

            if (field is IntegerField integerField && string.IsNullOrEmpty(integerField.label) == false)
            {
                return;
            }

            if (field is FloatField floatField && string.IsNullOrEmpty(floatField.label) == false)
            {
                return;
            }

            field.style.width = NumericFieldWidth;
            field.style.minWidth = NumericFieldWidth;
            field.style.maxWidth = NumericFieldWidth;
            field.style.flexGrow = 0f;
            field.style.flexShrink = 0f;
        }

        private static void ApplyNumericInputVerticalOffset(VisualElement field)
        {
            if (field is not IntegerField && field is not FloatField)
            {
                return;
            }

            var input = FindFieldInput(field);

            if (input == null)
            {
                return;
            }

            input.style.translate = new Translate(0f, NumericInputVerticalOffset);
        }

        private static void ApplySliderInputVerticalOffset(VisualElement field, VisualElement input)
        {
            if (field is not Slider && field is not SliderInt)
            {
                return;
            }

            input.style.translate = new Translate(0f, SliderInputVerticalOffset);
        }

        private void ApplyInputElementStyle(VisualElement element, bool centerText, bool tintBaseInput, bool numericInput)
        {
            if (element == null)
            {
                return;
            }

            var isTextInput =
                element.ClassListContains("unity-text-input") ||
                element.ClassListContains("unity-base-text-field__input");

            var isBaseInput =
                element.ClassListContains("unity-base-field__input") ||
                element.ClassListContains("unity-popup-field__input") ||
                element.ClassListContains("unity-object-field__input") ||
                element.ClassListContains("unity-color-field__input");

            if (isTextInput || (tintBaseInput && isBaseInput))
            {
                ApplyInputBoxStyle(element, ResolveInputBackgroundColor(), true, numericInput);
            }

            if (centerText && element is TextElement textElement && element is not Label)
            {
                textElement.style.unityTextAlign = TextAnchor.MiddleCenter;
            }
        }

        private void ApplyInputBoxStyle(VisualElement element, Color backgroundColor, bool applyTextColor, bool numericInput = false)
        {
            element.style.backgroundColor = backgroundColor;

            if (applyTextColor)
            {
                element.style.color = ResolveInputTextColor(numericInput);
            }

            element.style.borderTopColor = ResolveInputBorderTopColor();
            element.style.borderRightColor = ResolveInputBorderTopColor();
            element.style.borderBottomColor = ResolveInputBorderBottomColor();
            element.style.borderLeftColor = ResolveInputBorderTopColor();
        }

        private Color ResolveInputBackgroundColor()
        {
            if (EditorGUIUtility.isProSkin)
            {
                return _proInputBackgroundColor;
            }

            var color = Chrome.Palette.InputFieldColor;

            color.a = .1f;

            return color;
        }

        private void ApplySliderStyle(VisualElement field)
        {
            if (field is not Slider && field is not SliderInt)
            {
                return;
            }

            EnsureSliderHoverCallbacks(field);
            ApplySliderElementStyle(field);
            field.Query<VisualElement>().ForEach(ApplySliderElementStyle);
            ApplySliderHoverDot(field, field.ClassListContains(SliderHoverActiveClass));
        }

        private void ApplySliderElementStyle(VisualElement element)
        {
            if (element == null)
            {
                return;
            }

            if (IsSliderTrackElement(element))
            {
                var trackColor = ResolveSliderTrackColor();

                element.style.backgroundColor = trackColor;
                element.style.borderTopColor = trackColor;
                element.style.borderRightColor = trackColor;
                element.style.borderBottomColor = trackColor;
                element.style.borderLeftColor = trackColor;

                return;
            }

            if (IsSliderThumbElement(element) == false)
            {
                return;
            }

            HideSliderNativeThumbVisual(element);
        }

        private void EnsureSliderHoverCallbacks(VisualElement field)
        {
            if (field.ClassListContains(SliderHoverCallbacksClass))
            {
                return;
            }

            field.AddToClassList(SliderHoverCallbacksClass);
            field.RegisterCallback<PointerEnterEvent>(_ => SetSliderHoverDotVisible(field, true), TrickleDown.TrickleDown);
            field.RegisterCallback<PointerMoveEvent>(_ => SetSliderHoverDotVisible(field, true), TrickleDown.TrickleDown);
            field.RegisterCallback<PointerLeaveEvent>(_ => SetSliderHoverDotVisible(field, false), TrickleDown.TrickleDown);
            field.RegisterCallback<PointerDownEvent>(_ => RefreshSliderNativeThumbVisuals(field), TrickleDown.TrickleDown);
            field.RegisterCallback<PointerUpEvent>(_ => RefreshSliderNativeThumbVisuals(field), TrickleDown.TrickleDown);
            field.RegisterCallback<PointerCancelEvent>(_ => RefreshSliderNativeThumbVisuals(field), TrickleDown.TrickleDown);
            field.RegisterCallback<PointerCaptureOutEvent>(_ => RefreshSliderNativeThumbVisuals(field));
            field.RegisterCallback<MouseEnterEvent>(_ => SetSliderHoverDotVisible(field, true), TrickleDown.TrickleDown);
            field.RegisterCallback<MouseMoveEvent>(_ => SetSliderHoverDotVisible(field, true), TrickleDown.TrickleDown);
            field.RegisterCallback<MouseLeaveEvent>(_ => SetSliderHoverDotVisible(field, false), TrickleDown.TrickleDown);
            field.RegisterCallback<MouseDownEvent>(_ => RefreshSliderNativeThumbVisuals(field), TrickleDown.TrickleDown);
            field.RegisterCallback<MouseUpEvent>(_ => RefreshSliderNativeThumbVisuals(field), TrickleDown.TrickleDown);
            field.RegisterCallback<MouseCaptureOutEvent>(_ => RefreshSliderNativeThumbVisuals(field));
            field.RegisterCallback<FocusInEvent>(_ => RefreshSliderNativeThumbVisuals(field), TrickleDown.TrickleDown);
            field.RegisterCallback<FocusOutEvent>(_ => RefreshSliderNativeThumbVisuals(field), TrickleDown.TrickleDown);
        }

        private void RefreshSliderNativeThumbVisuals(VisualElement field)
        {
            field.Query<VisualElement>().ForEach(
                element =>
            {
                if (IsSliderThumbElement(element))
                {
                    HideSliderNativeThumbVisual(element);
                }
            });

            field.schedule.Execute(
                () =>
            {
                field.Query<VisualElement>().ForEach(
                    element =>
                {
                    if (IsSliderThumbElement(element))
                    {
                        HideSliderNativeThumbVisual(element);
                    }
                });
            });
        }

        private static void HideSliderNativeThumbVisual(VisualElement element)
        {
            element.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            element.style.backgroundColor = Color.clear;
            element.style.unityBackgroundImageTintColor = Color.clear;
            element.style.borderTopColor = Color.clear;
            element.style.borderRightColor = Color.clear;
            element.style.borderBottomColor = Color.clear;
            element.style.borderLeftColor = Color.clear;
        }

        private void ApplySliderHoverDot(VisualElement field, bool visible)
        {
            var thumbHost = FindSliderThumbHost(field);

            if (thumbHost == null)
            {
                return;
            }

            thumbHost.BringToFront();

            thumbHost.style.position = Position.Relative;
            thumbHost.style.overflow = Overflow.Hidden;

            var dot = thumbHost.Q<VisualElement>(SliderHoverDotName);

            if (dot == null)
            {
                dot = new VisualElement
                {
                    name = SliderHoverDotName,
                    pickingMode = PickingMode.Ignore
                };

                dot.style.position = Position.Absolute;
                dot.style.left = -.5f;
                dot.style.top = -1.5f;
                dot.style.right = -1.5f;
                dot.style.bottom = -.5f;
                dot.style.borderTopLeftRadius = 5f;
                dot.style.borderTopRightRadius = 5f;
                dot.style.borderBottomLeftRadius = 5f;
                dot.style.borderBottomRightRadius = 5f;

                thumbHost.Add(dot);
            }

            dot.style.backgroundColor = ResolveSliderDotColor(visible);
            dot.style.display = DisplayStyle.Flex;

            dot.BringToFront();
        }

        private static VisualElement FindSliderThumbHost(VisualElement field)
        {
            VisualElement fallback = null;
            VisualElement host = null;

            field.Query<VisualElement>().ForEach(
                element =>
            {
                if (host != null)
                {
                    return;
                }

                if (IsSliderThumbDraggerElement(element))
                {
                    host = element;

                    return;
                }

                if (fallback == null && IsSliderThumbElement(element))
                {
                    fallback = element;
                }
            });

            return host ?? fallback;
        }

        private void SetSliderHoverDotVisible(VisualElement field, bool visible)
        {
            if (visible)
            {
                field.AddToClassList(SliderHoverActiveClass);
            }
            else
            {
                field.RemoveFromClassList(SliderHoverActiveClass);
            }

            field.Query<VisualElement>().ForEach(
                element =>
            {
                if (element.name != SliderHoverDotName)
                {
                    return;
                }

                element.style.backgroundColor = ResolveSliderDotColor(visible);
                element.style.display = DisplayStyle.Flex;

                element.BringToFront();
            });

            ApplySliderHoverDot(field, visible);
        }

        private static bool IsSliderTrackElement(VisualElement element)
        {
            return element.ClassListContains("unity-base-slider__tracker") ||
                   element.ClassListContains("unity-base-slider__fill") ||
                   element.ClassListContains("unity-slider__tracker") ||
                   element.ClassListContains("unity-slider__fill");
        }

        private static bool IsSliderThumbElement(VisualElement element)
        {
            return element.ClassListContains("unity-base-slider__dragger") ||
                   element.ClassListContains("unity-base-slider__dragger-border") ||
                   element.ClassListContains("unity-slider__dragger") ||
                   element.ClassListContains("unity-slider__dragger-border");
        }

        private static bool IsSliderThumbDraggerElement(VisualElement element)
        {
            return element.ClassListContains("unity-base-slider__dragger") ||
                   element.ClassListContains("unity-slider__dragger");
        }

        private Color ResolveSliderTrackColor()
        {
            return EditorGUIUtility.isProSkin ? _proSliderTrackColor : new Color(.52f, .56f, .62f, 1f);
        }

        private Color ResolveSliderThumbColor()
        {
            return EditorGUIUtility.isProSkin ? _proSliderThumbColor : new Color(.58f, .62f, .68f, 1f);
        }

        private Color ResolveSliderHoverDotColor()
        {
            return EditorGUIUtility.isProSkin ? _proSliderHoverDotColor : new Color(.38f, .58f, .78f, 1f);
        }

        private Color ResolveSliderRestingDotColor()
        {
            return EditorGUIUtility.isProSkin ? _proSliderRestingDotColor : new Color(.49f, .53f, .58f, 1f);
        }

        private Color ResolveSliderDotColor(bool hovered)
        {
            return hovered ? ResolveSliderHoverDotColor() : ResolveSliderRestingDotColor();
        }

        private Color ResolveInputBorderTopColor()
        {
            return EditorGUIUtility.isProSkin ? _proInputBorderTopColor : Chrome.Palette.LineColor;
        }

        private Color ResolveInputBorderBottomColor()
        {
            return EditorGUIUtility.isProSkin ? _proInputBorderBottomColor : Chrome.Palette.LineColor;
        }

        private Color ResolveInputTextColor(bool numericInput = false)
        {
            if (numericInput && EditorGUIUtility.isProSkin)
            {
                return Color.white;
            }

            return Chrome.Palette.TextColor;
        }

        private Color ResolveCardTitleTextColor()
        {
            return ScaleRgb(Chrome.Palette.TextColor, CardTitleBrightnessMultiplier);
        }

        private Color ResolveFieldLabelTextColor()
        {
            return ScaleRgb(Chrome.Palette.TextColor, FieldLabelBrightnessMultiplier);
        }

        private static Color ScaleRgb(Color color, float multiplier)
        {
            return new Color(
                Mathf.Clamp01(color.r * multiplier),
                Mathf.Clamp01(color.g * multiplier),
                Mathf.Clamp01(color.b * multiplier),
                color.a);
        }

        private void BeginSliderUndoGroup(string label)
        {
            if (_sliderUndoGroup >= 0 || _undoTarget?.Invoke() == null)
            {
                return;
            }

            Undo.IncrementCurrentGroup();

            _sliderUndoGroup = Undo.GetCurrentGroup();

            Undo.SetCurrentGroupName("Modify " + (label ?? "Value"));
        }

        private void EndSliderUndoGroup()
        {
            if (_sliderUndoGroup < 0)
            {
                return;
            }

            Undo.CollapseUndoOperations(_sliderUndoGroup);

            _sliderUndoGroup = -1;
        }
    }
}
