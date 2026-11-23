using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace LucentPath.EditorUi.Core
{
    public class EditorPalette
    {
        public Color BackgroundColor { get; internal set; }
        public Color PanelColor { get; internal set; }
        public Color PanelSoftColor { get; internal set; }
        public Color CardColor { get; internal set; }
        public Color InputFieldColor { get; internal set; }
        public Color LineColor { get; internal set; }
        public Color TextColor { get; internal set; }
        public Color MutedTextColor { get; internal set; }
        public Color AccentColor { get; internal set; }
    }

    public class EditorMetrics
    {
        public float HeaderHeight { get; internal set; }
        public float HeaderBottomSpacing { get; internal set; }
        public float HeaderTitleX { get; internal set; }
        public float HeaderTitleY { get; internal set; }
        public float HeaderSubtitleY { get; internal set; }
        public float HeaderSidePadding { get; internal set; }
        public float SectionHeaderHeight { get; internal set; }
        public float SectionBottomSpacing { get; internal set; }
        public float SectionContentPaddingLeft { get; internal set; }
        public float SectionContentPaddingRight { get; internal set; }
        public float SectionContentPaddingTop { get; internal set; }
        public float CardBottomSpacing { get; internal set; }
        public float CardTitleBottomSpacing { get; internal set; }
        public RectOffset CardPadding { get; internal set; }
        public RectOffset CardBorder { get; internal set; }
        public float CardCornerRadius { get; internal set; }
    }

    public class EditorTypography
    {
        public Font NormalFont { get; internal set; }
        public Font BoldFont { get; internal set; }
        public int HeaderTitleFontSize { get; internal set; }
        public FontStyle HeaderTitleFontStyle { get; internal set; }
        public int HeaderSubtitleFontSize { get; internal set; }
        public FontStyle HeaderSubtitleFontStyle { get; internal set; }
        public int SectionTitleFontSize { get; internal set; }
        public FontStyle SectionTitleFontStyle { get; internal set; }
        public int SectionHintFontSize { get; internal set; }
        public FontStyle SectionHintFontStyle { get; internal set; }
        public int CardTitleFontSize { get; internal set; }
        public FontStyle CardTitleFontStyle { get; internal set; }
        public int CardHintFontSize { get; internal set; }
        public FontStyle CardHintFontStyle { get; internal set; }
        public int FieldLabelFontSize { get; internal set; }
        public FontStyle FieldLabelFontStyle { get; internal set; }
        public int NoteFontSize { get; internal set; }
        public FontStyle NoteFontStyle { get; internal set; }
        public Color ImGuiTextColor { get; internal set; }
        public Color ImGuiCardTitleTextColor { get; internal set; }
        public Color ImGuiFieldLabelTextColor { get; internal set; }
        public Color ImGuiMutedTextColor { get; internal set; }
    }

    public class EditorChrome
    {
        public EditorPalette Palette { get; }
        public EditorMetrics Metrics { get; }
        public EditorTypography Typography { get; }
        public GUIStyle SectionStyle { get; private set; }
        public GUIStyle SectionCollapsedStyle { get; private set; }
        public GUIStyle SectionContentStyle { get; private set; }
        public GUIStyle CardStyle { get; private set; }
        public GUIStyle CardTitleStyle { get; private set; }
        public GUIStyle CardHintStyle { get; private set; }
        public GUIStyle FieldLabelStyle { get; private set; }
        public GUIStyle NoteStyle { get; private set; }
        public GUIStyle HeaderTitleStyle { get; private set; }
        public GUIStyle HeaderSubtitleStyle { get; private set; }
        public GUIStyle SectionHeaderTitleStyle { get; private set; }
        public GUIStyle SectionHeaderHintStyle { get; private set; }

        private Texture2D _sectionTexture;
        private Texture2D _sectionHeaderTexture;
        private Texture2D _cardTexture;
        private Texture2D _imGuiInputFieldTexture;
        private Texture2D _imGuiToggleFieldTexture;
        private Texture2D _imGuiToggleFieldCheckedTexture;
        private Texture2D _imGuiToggleFieldHoverTexture;
        private Texture2D _imGuiToggleFieldCheckedHoverTexture;
        private RectOffset _imGuiInputFieldBorder;
        private GUIStyle _cardBackgroundStyle;

        private readonly bool _isSdfCardRoundingUsed;
        private bool _isStylesBuiltForProSkin;
        private bool _isCurrentCardDrewNote;

        private const float ImGuiFieldLabelWidth = 150f;
        private const float ImGuiSectionArrowVerticalOffset = 3f;
        private const float ImGuiNoteVerticalOffset = 2f;
        private const float ImGuiNoteSpacing = 4f;
        private const int ImGuiInputFieldCornerRadius = 5;
        private const int ImGuiInputFieldCornerPatchSize = 4;
        private const int ImGuiInputFieldShadowSize = 2;
        private const int ImGuiToggleHorizontalInset = 1;
        private static readonly Color _imGuiInputValueTextColor = new Color32(228, 228, 228, 255);
        private static readonly Color _imGuiInputFieldBackgroundColor = new Color32(80, 80, 80, 255);
        private static readonly Color _imGuiToggleFieldHoverColor = new Color32(96, 99, 106, 255);
        private static readonly Color _imGuiInputFieldShadowColor = new Color32(23, 25, 30, 255);
        private static readonly Color _imGuiToggleFieldHoverShadowColor = new Color32(29, 32, 38, 255);
        private static readonly Color _imGuiToggleCheckColor = new Color32(228, 234, 242, 255);

        private static PropertyInfo _guiClipVisibleRectProperty;

        private class TextStyleSnapshot
        {
            private readonly GUIStyle _style;
            private readonly Texture2D _normalBackground;
            private readonly Texture2D _hoverBackground;
            private readonly Texture2D _activeBackground;
            private readonly Texture2D _focusedBackground;
            private readonly Texture2D _onNormalBackground;
            private readonly Texture2D _onHoverBackground;
            private readonly Texture2D _onActiveBackground;
            private readonly Texture2D _onFocusedBackground;
            private readonly RectOffset _border;
            private readonly Font _font;

            private readonly Color _normal;
            private readonly Color _hover;
            private readonly Color _active;
            private readonly Color _focused;
            private readonly Color _onNormal;
            private readonly Color _onHover;
            private readonly Color _onActive;
            private readonly Color _onFocused;
            private readonly int _fontSize;
            private readonly FontStyle _fontStyle;

            public TextStyleSnapshot(GUIStyle style)
            {
                _style = style;
                _normal = style.normal.textColor;
                _hover = style.hover.textColor;
                _active = style.active.textColor;
                _focused = style.focused.textColor;
                _onNormal = style.onNormal.textColor;
                _onHover = style.onHover.textColor;
                _onActive = style.onActive.textColor;
                _onFocused = style.onFocused.textColor;
                _normalBackground = style.normal.background;
                _hoverBackground = style.hover.background;
                _activeBackground = style.active.background;
                _focusedBackground = style.focused.background;
                _onNormalBackground = style.onNormal.background;
                _onHoverBackground = style.onHover.background;
                _onActiveBackground = style.onActive.background;
                _onFocusedBackground = style.onFocused.background;
                _border = CopyRectOffset(style.border);
                _font = style.font;
                _fontSize = style.fontSize;
                _fontStyle = style.fontStyle;
            }

            public void Restore()
            {
                _style.normal.textColor = _normal;
                _style.hover.textColor = _hover;
                _style.active.textColor = _active;
                _style.focused.textColor = _focused;
                _style.onNormal.textColor = _onNormal;
                _style.onHover.textColor = _onHover;
                _style.onActive.textColor = _onActive;
                _style.onFocused.textColor = _onFocused;
                _style.normal.background = _normalBackground;
                _style.hover.background = _hoverBackground;
                _style.active.background = _activeBackground;
                _style.focused.background = _focusedBackground;
                _style.onNormal.background = _onNormalBackground;
                _style.onHover.background = _onHoverBackground;
                _style.onActive.background = _onActiveBackground;
                _style.onFocused.background = _onFocusedBackground;
                _style.border = CopyRectOffset(_border);
                _style.font = _font;
                _style.fontSize = _fontSize;
                _style.fontStyle = _fontStyle;
            }
        }

        public EditorChrome(bool useSdfCardRounding = false)
        {
            _isSdfCardRoundingUsed = useSdfCardRounding;
            Palette = new EditorPalette();
            Metrics = new EditorMetrics();
            Typography = new EditorTypography();
        }

        public void EnsureStyles()
        {
            var isProSkin = EditorGUIUtility.isProSkin;

            if (CardStyle != null &&
                HeaderTitleStyle != null &&
                HeaderSubtitleStyle != null &&
                SectionHeaderTitleStyle != null &&
                SectionHeaderHintStyle != null &&
                _isStylesBuiltForProSkin == isProSkin)
            {
                return;
            }

            _isStylesBuiltForProSkin = isProSkin;
            Palette.BackgroundColor = isProSkin ? new Color32(32, 36, 43, 255) : new Color(.78f, .79f, .81f, 1f);
            Palette.PanelColor = isProSkin ? new Color32(38, 44, 53, 255) : new Color(.91f, .92f, .94f, 1f);
            Palette.PanelSoftColor = isProSkin ? new Color32(48, 56, 70, 255) : new Color(.86f, .88f, .91f, 1f);
            Palette.CardColor = isProSkin ? new Color32(26, 31, 39, 255) : new Color(.95f, .96f, .98f, 1f);

            Palette.InputFieldColor = isProSkin
                ? new Color32(204, 216, 247, 255)
                : Color.Lerp(Palette.PanelSoftColor, Palette.AccentColor, .2f);

            Palette.LineColor = isProSkin ? new Color32(69, 81, 101, 255) : new Color(.42f, .46f, .54f, 1f);
            Palette.TextColor = isProSkin ? new Color32(225, 231, 239, 255) : new Color(.13f, .14f, .16f, 1f);
            Palette.MutedTextColor = isProSkin ? new Color32(159, 175, 195, 255) : new Color(.32f, .36f, .42f, 1f);
            Palette.AccentColor = isProSkin ? new Color32(122, 199, 245, 255) : new Color(.24f, .57f, 1f, 1f);

            Metrics.HeaderHeight = 84f;
            Metrics.HeaderBottomSpacing = 8f;
            Metrics.HeaderTitleX = 16f;
            Metrics.HeaderTitleY = 12f;
            Metrics.HeaderSubtitleY = 42f;
            Metrics.HeaderSidePadding = 32f;
            Metrics.SectionHeaderHeight = 42f;
            Metrics.SectionBottomSpacing = 8f;
            Metrics.SectionContentPaddingLeft = 8;
            Metrics.SectionContentPaddingRight = 8;
            Metrics.SectionContentPaddingTop = 0;
            Metrics.CardBottomSpacing = 6f;
            Metrics.CardTitleBottomSpacing = 4f;
            Metrics.CardPadding = new RectOffset(10, 10, 9, 10);
            Metrics.CardBorder = new RectOffset(8, 8, 8, 8);
            Metrics.CardCornerRadius = 8f;

            Typography.HeaderTitleFontSize = 18;
            Typography.NormalFont = ResolveEditorFont(() => EditorStyles.label, "Fonts/Inter-Small.ttf");
            Typography.BoldFont = ResolveEditorFont(() => EditorStyles.boldLabel, "Fonts/Inter-SemiBold.ttf");
            Typography.ImGuiTextColor = isProSkin ? Color.white : Palette.TextColor;

            var imGuiPrimaryTextColor = isProSkin ? Color.Lerp(Palette.TextColor, Color.white, .2f) : Palette.TextColor;

            Typography.ImGuiCardTitleTextColor = imGuiPrimaryTextColor;
            Typography.ImGuiFieldLabelTextColor = imGuiPrimaryTextColor;
            Typography.ImGuiMutedTextColor = isProSkin ? new Color32(184, 202, 224, 255) : Palette.MutedTextColor;
            Typography.HeaderTitleFontStyle = FontStyle.Normal;
            Typography.HeaderSubtitleFontSize = 11;
            Typography.HeaderSubtitleFontStyle = FontStyle.Normal;
            Typography.SectionTitleFontSize = 12;
            Typography.SectionTitleFontStyle = FontStyle.Normal;
            Typography.SectionHintFontSize = 10;
            Typography.SectionHintFontStyle = FontStyle.Normal;
            Typography.CardTitleFontSize = 12;
            Typography.CardTitleFontStyle = FontStyle.Normal;
            Typography.CardHintFontSize = 10;
            Typography.CardHintFontStyle = FontStyle.Normal;
            Typography.FieldLabelFontSize = 12;
            Typography.FieldLabelFontStyle = FontStyle.Normal;
            Typography.NoteFontSize = 10;
            Typography.NoteFontStyle = FontStyle.Normal;

            _sectionTexture = MakeTexture(Palette.PanelColor);
            _sectionHeaderTexture = MakeTexture(Palette.PanelSoftColor);

            _cardTexture = _isSdfCardRoundingUsed
                ? MakeSdfRoundedTexture(Palette.CardColor, Mathf.RoundToInt(Metrics.CardCornerRadius))
                : MakeRoundedTexture(Palette.CardColor, Mathf.RoundToInt(Metrics.CardCornerRadius));

            _imGuiInputFieldTexture = MakeRoundedFieldTexture(
                _imGuiInputFieldBackgroundColor,
                _imGuiInputFieldShadowColor,
                ImGuiInputFieldCornerRadius,
                ImGuiInputFieldShadowSize);

            _imGuiToggleFieldTexture = MakeRoundedFieldTexture(
                _imGuiInputFieldBackgroundColor,
                _imGuiInputFieldShadowColor,
                ImGuiInputFieldCornerRadius,
                ImGuiInputFieldShadowSize,
                false,
                ImGuiToggleHorizontalInset);

            _imGuiToggleFieldCheckedTexture = MakeRoundedFieldTexture(
                _imGuiInputFieldBackgroundColor,
                _imGuiInputFieldShadowColor,
                ImGuiInputFieldCornerRadius,
                ImGuiInputFieldShadowSize,
                true,
                ImGuiToggleHorizontalInset);

            _imGuiToggleFieldHoverTexture = MakeRoundedFieldTexture(
                _imGuiToggleFieldHoverColor,
                _imGuiToggleFieldHoverShadowColor,
                ImGuiInputFieldCornerRadius,
                ImGuiInputFieldShadowSize,
                false,
                ImGuiToggleHorizontalInset);

            _imGuiToggleFieldCheckedHoverTexture = MakeRoundedFieldTexture(
                _imGuiToggleFieldHoverColor,
                _imGuiToggleFieldHoverShadowColor,
                ImGuiInputFieldCornerRadius,
                ImGuiInputFieldShadowSize,
                true,
                ImGuiToggleHorizontalInset);

            _imGuiInputFieldBorder = CreateRectOffset(ImGuiInputFieldCornerPatchSize);

            SectionStyle = new GUIStyle
            {
                padding = new RectOffset(0, 0, 0, 2),
                margin = new RectOffset(0, 0, 0, 0),
                normal = { background = _sectionTexture }
            };

            SectionCollapsedStyle = new GUIStyle(SectionStyle)
            {
                padding = new RectOffset(0, 0, 0, 0)
            };

            SectionContentStyle = new GUIStyle
            {
                padding = new RectOffset(
                    Mathf.RoundToInt(Metrics.SectionContentPaddingLeft),
                    Mathf.RoundToInt(Metrics.SectionContentPaddingRight),
                    Mathf.RoundToInt(Metrics.SectionContentPaddingTop),
                    0),
                margin = new RectOffset(0, 0, 0, 0)
            };

            CardStyle = new GUIStyle
            {
                border = Metrics.CardBorder,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };

            _cardBackgroundStyle = new GUIStyle
            {
                border = Metrics.CardBorder,
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, 0, 0)
            };

            ApplyBackgroundToAllStates(_cardBackgroundStyle, _cardTexture);

            CardTitleStyle = new GUIStyle(GetEditorStyle(() => EditorStyles.boldLabel))
            {
                font = Typography.BoldFont,
                fontSize = Typography.CardTitleFontSize,
                fontStyle = Typography.CardTitleFontStyle,
                richText = false
            };

            ApplyTextColorToAllStates(CardTitleStyle, Typography.ImGuiCardTitleTextColor);

            CardHintStyle = new GUIStyle(GetEditorStyle(() => EditorStyles.wordWrappedMiniLabel))
            {
                font = Typography.NormalFont,
                fontSize = Typography.CardHintFontSize,
                fontStyle = Typography.CardHintFontStyle,
                richText = false
            };

            ApplyTextColorToAllStates(CardHintStyle, Typography.ImGuiMutedTextColor);

            FieldLabelStyle = new GUIStyle(GetEditorStyle(() => EditorStyles.label))
            {
                font = Typography.NormalFont,
                fontSize = Typography.FieldLabelFontSize,
                fontStyle = Typography.FieldLabelFontStyle,
                alignment = TextAnchor.MiddleLeft
            };

            ApplyTextColorToAllStates(FieldLabelStyle, Typography.ImGuiTextColor);

            NoteStyle = new GUIStyle(GetEditorStyle(() => EditorStyles.wordWrappedMiniLabel))
            {
                font = Typography.NormalFont,
                fontSize = Typography.NoteFontSize,
                fontStyle = Typography.NoteFontStyle,
                richText = false
            };

            ApplyTextColorToAllStates(NoteStyle, Typography.ImGuiMutedTextColor);

            HeaderTitleStyle = new GUIStyle(GetEditorStyle(() => EditorStyles.boldLabel))
            {
                font = Typography.BoldFont,
                fontSize = Typography.HeaderTitleFontSize,
                fontStyle = Typography.HeaderTitleFontStyle,
                alignment = TextAnchor.MiddleLeft
            };

            ApplyTextColorToAllStates(HeaderTitleStyle, Typography.ImGuiTextColor);

            HeaderSubtitleStyle = new GUIStyle(GetEditorStyle(() => EditorStyles.label))
            {
                font = Typography.NormalFont,
                fontSize = Typography.HeaderSubtitleFontSize,
                fontStyle = Typography.HeaderSubtitleFontStyle,
                alignment = TextAnchor.MiddleLeft
            };

            ApplyTextColorToAllStates(HeaderSubtitleStyle, Typography.ImGuiMutedTextColor);

            SectionHeaderTitleStyle = new GUIStyle(GetEditorStyle(() => EditorStyles.boldLabel))
            {
                font = Typography.BoldFont,
                fontSize = Typography.SectionTitleFontSize,
                fontStyle = Typography.SectionTitleFontStyle,
                alignment = TextAnchor.MiddleLeft
            };

            ApplyTextColorToAllStates(SectionHeaderTitleStyle, Typography.ImGuiTextColor);

            SectionHeaderHintStyle = new GUIStyle(GetEditorStyle(() => EditorStyles.label))
            {
                font = Typography.NormalFont,
                fontSize = Typography.SectionHintFontSize,
                fontStyle = Typography.SectionHintFontStyle,
                alignment = TextAnchor.MiddleLeft
            };

            ApplyTextColorToAllStates(SectionHeaderHintStyle, Typography.ImGuiMutedTextColor);
        }

        public void DrawFullWidthBackground(float startY)
        {
            EnsureStyles();

            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            var visibleRect = GetVisibleRect();
            var rect = new Rect(visibleRect.xMin, startY, visibleRect.width, Mathf.Max(0f, visibleRect.height - startY));

            EditorGUI.DrawRect(rect, Palette.BackgroundColor);
        }

        public void DrawVisibleBackground()
        {
            EnsureStyles();

            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            var visibleRect = GetVisibleRect();
            var rect = new Rect(0f, 0f, Mathf.Max(1f, EditorGUIUtility.currentViewWidth), Mathf.Max(1f, visibleRect.height));

            EditorGUI.DrawRect(rect, Palette.BackgroundColor);
        }

        public void DrawFullWidthHeader(string title, string subtitle)
        {
            DrawFullWidthHeader(title, subtitle, null);
        }

        public void DrawFullWidthHeader(string title, string subtitle, Action<Rect> drawOverlay)
        {
            EnsureStyles();

            var layoutRect = GUILayoutUtility.GetRect(1f, 84f, GUILayout.ExpandWidth(true));
            var visibleRect = GetVisibleRect();
            var rect = new Rect(visibleRect.xMin, layoutRect.y, visibleRect.width, layoutRect.height);

            EditorGUI.DrawRect(rect, Palette.PanelColor);

            var titleRect = new Rect(rect.x + 16f, rect.y + 12f, rect.width - 32f, 28f);

            GUI.Label(titleRect, title ?? string.Empty, HeaderTitleStyle);

            if (string.IsNullOrEmpty(subtitle) == false)
            {
                var subtitleRect = new Rect(rect.x + 16f, rect.y + 42f, rect.width - 32f, 20f);

                GUI.Label(subtitleRect, subtitle, HeaderSubtitleStyle);
            }

            drawOverlay?.Invoke(rect);

            GUILayout.Space(8f);
        }

        public void DrawSectionShell(string title, string hint, bool visible, Action toggle, Action drawContent)
        {
            EnsureStyles();

            var sectionRect = EditorGUILayout.BeginVertical(GUIStyle.none, GUILayout.ExpandWidth(true));

            try
            {
                if (Event.current.type == EventType.Repaint)
                {
                    var visibleRect = GetVisibleRect();
                    var fullRect = new Rect(visibleRect.xMin, sectionRect.y, visibleRect.width, sectionRect.height);

                    EditorGUI.DrawRect(fullRect, Palette.PanelColor);
                }

                DrawSectionHeader(title, hint, visible, toggle);

                if (visible)
                {
                    GUILayout.Space(Metrics.SectionBottomSpacing);
                    EditorGUILayout.BeginVertical(SectionContentStyle);
                    drawContent?.Invoke();
                    EditorGUILayout.EndVertical();
                }
            }
            finally
            {
                EditorGUILayout.EndVertical();
            }

            GUILayout.Space(Metrics.SectionBottomSpacing);
        }

        public void DrawSectionHeader(string title, string hint, bool visible, Action toggle)
        {
            EnsureStyles();

            var layoutRect = GUILayoutUtility.GetRect(1f, Metrics.SectionHeaderHeight, GUILayout.ExpandWidth(true));
            var visibleRect = GetVisibleRect();
            var headerRect = new Rect(visibleRect.xMin, layoutRect.y, visibleRect.width, layoutRect.height);

            DrawSectionHeaderRect(headerRect, title, hint, visible, toggle);
        }

        public void DrawCard(string title, string hint, RectOffset padding, Action drawContent)
        {
            EnsureStyles();

            var layoutRect = EditorGUILayout.BeginVertical(GUIStyle.none, GUILayout.ExpandWidth(true));

            try
            {
                var cardRect = GetCardRect(layoutRect);

                if (Event.current.type == EventType.Repaint)
                {
                    DrawCardBackground(cardRect);
                }

                EditorGUILayout.BeginVertical(CardStyle);
                DrawCardContent(cardRect, title, hint, ResolveCardPadding(padding), drawContent);
                EditorGUILayout.EndVertical();
            }
            finally
            {
                EditorGUILayout.EndVertical();
            }

            GUILayout.Space(Metrics.CardBottomSpacing);
        }

        public T DrawWithPipelineFieldStyling<T>(Func<T> draw)
        {
            EnsureStyles();
            EnableImmediateImGuiHoverUpdates();

            var previousWideMode = EditorGUIUtility.wideMode;
            var previousLabelWidth = EditorGUIUtility.labelWidth;

            try
            {
                EditorGUIUtility.wideMode = true;
                EditorGUIUtility.labelWidth = ImGuiFieldLabelWidth;

                return DrawWithInputFieldTint(() => DrawWithCenteredNumberFieldStyle(() => DrawWithStandardLabelTint(
                    Typography.ImGuiFieldLabelTextColor,
                    draw)));
            }
            finally
            {
                EditorGUIUtility.wideMode = previousWideMode;
                EditorGUIUtility.labelWidth = previousLabelWidth;
            }
        }

        public void DrawWithPipelineFieldStyling(Action draw)
        {
            DrawWithPipelineFieldStyling(
                () =>
            {
                draw?.Invoke();

                return 0;
            });
        }

        public void DrawNote(string text)
        {
            EnsureStyles();

            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            _isCurrentCardDrewNote = true;

            GUILayout.Space(ImGuiNoteSpacing);

            var content = EditorGUIUtility.TrTempContent(text);
            var estimatedWidth = Mathf.Max(1f, EditorGUIUtility.currentViewWidth - 80f);
            var noteHeight = Mathf.Max(NoteStyle.lineHeight, NoteStyle.CalcHeight(content, estimatedWidth));
            var layoutRect = GUILayoutUtility.GetRect(1f, noteHeight + ImGuiNoteVerticalOffset, GUILayout.ExpandWidth(true));

            var noteRect = new Rect(
                layoutRect.x,
                layoutRect.y + ImGuiNoteVerticalOffset,
                layoutRect.width,
                noteHeight);

            GUI.Label(noteRect, content, NoteStyle);
        }

        public Rect DrawTexturePropertySingleLine(
            MaterialEditor materialEditor,
            GUIContent label,
            MaterialProperty textureProperty,
            MaterialProperty inlineProperty = null,
            MaterialProperty inlineProperty2 = null)
        {
            EnsureStyles();

            if (materialEditor == null || textureProperty == null)
            {
                return default;
            }

            return DrawWithPipelineFieldStyling(
                () =>
            {
                if (inlineProperty != null && inlineProperty2 != null)
                {
                    return materialEditor.TexturePropertySingleLine(label, textureProperty, inlineProperty, inlineProperty2);
                }

                if (inlineProperty != null)
                {
                    return materialEditor.TexturePropertySingleLine(label, textureProperty, inlineProperty);
                }

                return materialEditor.TexturePropertySingleLine(label, textureProperty);
            });
        }

        public T DrawWithInputFieldTint<T>(Func<T> draw)
        {
            EnsureStyles();

            var previousBackgroundColor = GUI.backgroundColor;

            GUI.backgroundColor = Palette.InputFieldColor;

            try
            {
                return draw != null ? draw() : default;
            }
            finally
            {
                GUI.backgroundColor = previousBackgroundColor;
            }
        }

        public T DrawWithCenteredNumberFieldStyle<T>(Func<T> draw)
        {
            EnsureStyles();

            var numberField = EditorStyles.numberField;
            var textField = EditorStyles.textField;
            var miniTextField = EditorStyles.miniTextField;
            var popup = EditorStyles.popup;
            var skinTextField = GUI.skin?.textField;
            var toggleStyles = new List<GUIStyle>();

            AddUniqueStyle(toggleStyles, GetEditorStyle("toggle"));
            AddUniqueStyle(toggleStyles, GUI.skin?.toggle);
            AddUniqueStyle(toggleStyles, GetSkinStyle("Toggle"));

            var previousNumberAlignment = numberField.alignment;
            var previousTextAlignment = textField.alignment;
            var previousMiniTextAlignment = miniTextField.alignment;
            var numberFieldSnapshot = new TextStyleSnapshot(numberField);
            var textFieldSnapshot = new TextStyleSnapshot(textField);
            var miniTextFieldSnapshot = new TextStyleSnapshot(miniTextField);
            var popupSnapshot = new TextStyleSnapshot(popup);
            var toggleSnapshots = CreateStyleSnapshots(toggleStyles);

            var skinTextFieldSnapshot = skinTextField != null &&
                                        ReferenceEquals(skinTextField, numberField) == false &&
                                        ReferenceEquals(skinTextField, textField) == false &&
                                        ReferenceEquals(skinTextField, miniTextField) == false
                ? new TextStyleSnapshot(skinTextField)
                : null;

            try
            {
                ApplyStandardFieldTextStyle(numberField);
                ApplyStandardFieldTextStyle(textField, _imGuiInputValueTextColor, Typography.FieldLabelFontSize - 1);
                ApplyStandardFieldTextStyle(miniTextField, _imGuiInputValueTextColor, Typography.FieldLabelFontSize - 1);
                ApplyStandardFieldTextStyle(skinTextField, _imGuiInputValueTextColor, Typography.FieldLabelFontSize - 1);
                ApplyImGuiInputFieldBackground(textField);
                ApplyImGuiInputFieldBackground(miniTextField);
                ApplyImGuiInputFieldBackground(skinTextField);
                ApplyImGuiToggleBackgrounds(toggleStyles);
                ApplyStandardFieldTextStyle(popup);

                numberField.alignment = TextAnchor.MiddleCenter;
                textField.alignment = TextAnchor.MiddleCenter;
                miniTextField.alignment = TextAnchor.MiddleCenter;

                return draw != null ? draw() : default;
            }
            finally
            {
                skinTextFieldSnapshot?.Restore();
                RestoreStyleSnapshots(toggleSnapshots);
                popupSnapshot.Restore();
                miniTextFieldSnapshot.Restore();
                textFieldSnapshot.Restore();
                numberFieldSnapshot.Restore();

                numberField.alignment = previousNumberAlignment;
                textField.alignment = previousTextAlignment;
                miniTextField.alignment = previousMiniTextAlignment;
            }
        }

        public T DrawWithStandardLabelTint<T>(Func<T> draw)
        {
            EnsureStyles();

            return DrawWithStandardLabelTint(Typography.ImGuiTextColor, draw);
        }

        public void DrawWithStandardLabelTint(Action draw)
        {
            DrawWithStandardLabelTint(
                () =>
            {
                draw?.Invoke();

                return 0;
            });
        }

        internal static GUIStyle GetEditorStyle(Func<GUIStyle> getStyle)
        {
            try
            {
                var style = getStyle?.Invoke();

                if (style != null)
                {
                    return style;
                }
            }
            catch (NullReferenceException)
            { }

            return Event.current != null && GUI.skin != null ? GUI.skin.label : new GUIStyle();
        }

        private void DrawSectionHeaderRect(Rect headerRect, string title, string hint, bool visible, Action toggle)
        {
            EditorGUI.DrawRect(headerRect, Palette.PanelSoftColor);

            var arrow = visible ? "▼" : "▶";

            GUI.Label(new Rect(headerRect.x + 12f, headerRect.y + 7f + ImGuiSectionArrowVerticalOffset, 22f, 22f), arrow, SectionHeaderTitleStyle);
            GUI.Label(new Rect(headerRect.x + 36f, headerRect.y + 5f, headerRect.width - 48f, 18f), title, SectionHeaderTitleStyle);
            GUI.Label(new Rect(headerRect.x + 36f, headerRect.y + 23f, headerRect.width - 48f, 16f), hint, SectionHeaderHintStyle);

            if (GUI.Button(headerRect, GUIContent.none, GUIStyle.none))
            {
                toggle?.Invoke();
            }
        }

        private RectOffset ResolveCardPadding(RectOffset padding)
        {
            if (padding == null)
            {
                return Metrics.CardPadding;
            }

            return padding;
        }

        private Rect GetCardRect(Rect layoutRect)
        {
            var visibleRect = GetVisibleRect();
            var leftInset = Metrics.SectionContentPaddingLeft;
            var rightInset = Metrics.SectionContentPaddingRight;

            return new Rect(
                visibleRect.xMin + leftInset,
                layoutRect.y,
                Mathf.Max(1f, visibleRect.width - leftInset - rightInset),
                layoutRect.height);
        }

        private void DrawCardBackground(Rect cardRect)
        {
            _cardBackgroundStyle.Draw(cardRect, GUIContent.none, false, false, false, false);
        }

        private void DrawCardContent(Rect cardRect, string title, string hint, RectOffset padding, Action drawContent)
        {
            var headerPadding = Metrics.CardPadding;
            var headerWidth = Mathf.Max(1f, cardRect.width - headerPadding.left - headerPadding.right);
            var contentWidth = Mathf.Max(1f, cardRect.width - padding.left - padding.right);

            GUILayout.Space(headerPadding.top);

            EditorGUILayout.BeginHorizontal();

            try
            {
                GUILayout.Space(headerPadding.left);

                EditorGUILayout.BeginVertical(GUILayout.Width(headerWidth), GUILayout.ExpandWidth(false));

                try
                {
                    DrawCardHeader(cardRect, title, hint, headerPadding);
                }
                finally
                {
                    EditorGUILayout.EndVertical();
                }

                GUILayout.FlexibleSpace();
            }
            finally
            {
                EditorGUILayout.EndHorizontal();
            }

            if (padding.top > 0)
            {
                GUILayout.Space(padding.top);
            }

            EditorGUILayout.BeginHorizontal();

            try
            {
                GUILayout.Space(padding.left);

                EditorGUILayout.BeginVertical(GUILayout.Width(contentWidth), GUILayout.ExpandWidth(false));

                try
                {
                    _isCurrentCardDrewNote = false;

                    drawContent?.Invoke();
                }
                finally
                {
                    EditorGUILayout.EndVertical();
                }

                GUILayout.FlexibleSpace();
            }
            finally
            {
                EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(_isCurrentCardDrewNote ? ImGuiNoteSpacing : padding.bottom);

            _isCurrentCardDrewNote = false;
        }

        private void DrawCardHeader(Rect cardRect, string title, string hint, RectOffset padding)
        {
            var _ = hint;
            var titleContent = EditorGUIUtility.TrTempContent(title ?? string.Empty);
            var contentWidth = Mathf.Max(1f, cardRect.width - padding.left - padding.right);
            var titleHeight = Mathf.Max(EditorGUIUtility.singleLineHeight, CardTitleStyle.CalcHeight(titleContent, contentWidth));

            GUILayoutUtility.GetRect(1f, titleHeight, GUILayout.ExpandWidth(true));

            var titleRect = new Rect(
                cardRect.x + padding.left,
                cardRect.y + padding.top,
                contentWidth,
                titleHeight);

            GUI.Label(titleRect, titleContent, CardTitleStyle);

            GUILayout.Space(Metrics.CardTitleBottomSpacing);
        }

        private T DrawWithStandardLabelTint<T>(Color labelTextColor, Func<T> draw)
        {
            EnsureStyles();

            var editorLabelStyle = EditorStyles.label;
            var skinLabelStyle = GUI.skin?.label;
            var editorLabelSnapshot = new TextStyleSnapshot(editorLabelStyle);

            var skinLabelSnapshot = skinLabelStyle != null && ReferenceEquals(skinLabelStyle, editorLabelStyle) == false
                ? new TextStyleSnapshot(skinLabelStyle)
                : null;

            var guiColor = GUI.color;
            var contentColor = GUI.contentColor;

            try
            {
                ApplyStandardLabelStyle(editorLabelStyle, labelTextColor);
                ApplyStandardLabelStyle(skinLabelStyle, labelTextColor);

                GUI.color = Color.white;
                GUI.contentColor = Typography.ImGuiTextColor;

                return draw != null ? draw() : default;
            }
            finally
            {
                GUI.contentColor = contentColor;
                GUI.color = guiColor;

                skinLabelSnapshot?.Restore();
                editorLabelSnapshot.Restore();
            }
        }

        private static Texture2D MakeTexture(Color fillColor)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };

            texture.SetPixel(0, 0, fillColor);
            texture.Apply(false, true);

            return texture;
        }

        private static Font ResolveEditorFont(Func<GUIStyle> getStyle, string resourcePath)
        {
            var resourceFont = EditorGUIUtility.Load(resourcePath) as Font ??
                               LoadEditorResourceFont(resourcePath);

            if (resourceFont != null)
            {
                return resourceFont;
            }

            return GetEditorStyle(getStyle).font;
        }

        private static Font LoadEditorResourceFont(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
            {
                return null;
            }

            var contentsPath = EditorApplication.applicationContentsPath;

            if (string.IsNullOrEmpty(contentsPath))
            {
                return null;
            }

            var fontPath = Path.Combine(contentsPath, "Resources", relativePath.Replace('/', Path.DirectorySeparatorChar));

            if (File.Exists(fontPath) == false)
            {
                return null;
            }

            var font = new Font(fontPath)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            return font;
        }

        private static void ApplyTextColorToAllStates(GUIStyle style, Color color)
        {
            if (style == null)
            {
                return;
            }

            style.normal.textColor = color;
            style.hover.textColor = color;
            style.active.textColor = color;
            style.focused.textColor = color;
            style.onNormal.textColor = color;
            style.onHover.textColor = color;
            style.onActive.textColor = color;
            style.onFocused.textColor = color;
        }

        private static void EnableImmediateImGuiHoverUpdates()
        {
            var window = EditorWindow.mouseOverWindow ?? EditorWindow.focusedWindow;

            if (window == null)
            {
                return;
            }

            window.wantsMouseMove = true;

            if (Event.current?.type == EventType.MouseMove)
            {
                window.Repaint();
            }
        }

        private void ApplyImGuiInputFieldBackground(GUIStyle style)
        {
            if (style == null)
            {
                return;
            }

            ApplyBackgroundToAllStates(style, _imGuiInputFieldTexture);

            style.border = CopyRectOffset(_imGuiInputFieldBorder);
        }

        private void ApplyImGuiToggleBackgrounds(List<GUIStyle> styles)
        {
            for (var i = 0; i < styles.Count; i++)
            {
                var style = styles[i];

                if (style == null)
                {
                    continue;
                }

                ApplyToggleBackgroundStates(style);

                style.border = CopyRectOffset(_imGuiInputFieldBorder);
            }
        }

        private void ApplyToggleBackgroundStates(GUIStyle style)
        {
            style.normal.background = _imGuiToggleFieldTexture;
            style.hover.background = _imGuiToggleFieldHoverTexture;
            style.active.background = _imGuiToggleFieldHoverTexture;
            style.focused.background = _imGuiToggleFieldHoverTexture;
            style.onNormal.background = _imGuiToggleFieldCheckedTexture;
            style.onHover.background = _imGuiToggleFieldCheckedHoverTexture;
            style.onActive.background = _imGuiToggleFieldCheckedHoverTexture;
            style.onFocused.background = _imGuiToggleFieldCheckedHoverTexture;
        }

        private static void ApplyBackgroundToAllStates(GUIStyle style, Texture2D background)
        {
            if (style == null)
            {
                return;
            }

            style.normal.background = background;
            style.hover.background = background;
            style.active.background = background;
            style.focused.background = background;
            style.onNormal.background = background;
            style.onHover.background = background;
            style.onActive.background = background;
            style.onFocused.background = background;
        }

        private void ApplyStandardLabelStyle(GUIStyle style, Color textColor)
        {
            if (style == null)
            {
                return;
            }

            style.font = Typography.NormalFont;
            style.fontSize = Typography.FieldLabelFontSize;
            style.fontStyle = Typography.FieldLabelFontStyle;

            ApplyTextColorToAllStates(style, textColor);
        }

        private void ApplyStandardFieldTextStyle(GUIStyle style)
        {
            ApplyStandardLabelStyle(style, Typography.ImGuiTextColor);
        }

        private void ApplyStandardFieldTextStyle(GUIStyle style, Color textColor)
        {
            ApplyStandardFieldTextStyle(style, textColor, Typography.FieldLabelFontSize);
        }

        private void ApplyStandardFieldTextStyle(GUIStyle style, Color textColor, int fontSize)
        {
            ApplyStandardLabelStyle(style, textColor);

            if (style != null)
            {
                style.fontSize = fontSize;
            }
        }

        private static RectOffset CreateRectOffset(int value)
        {
            return new RectOffset(value, value, value, value);
        }

        private static RectOffset CopyRectOffset(RectOffset source)
        {
            return source == null ? null : new RectOffset(source.left, source.right, source.top, source.bottom);
        }

        private static GUIStyle GetEditorStyle(string propertyName)
        {
            return typeof(EditorStyles).GetProperty(propertyName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(null, null) as GUIStyle;
        }

        private static GUIStyle GetSkinStyle(string styleName)
        {
            return GUI.skin?.FindStyle(styleName);
        }

        private static void AddUniqueStyle(List<GUIStyle> styles, GUIStyle style)
        {
            if (style == null)
            {
                return;
            }

            for (var i = 0; i < styles.Count; i++)
            {
                var existing = styles[i];

                if (ReferenceEquals(existing, style))
                {
                    return;
                }
            }

            styles.Add(style);
        }

        private static List<TextStyleSnapshot> CreateStyleSnapshots(List<GUIStyle> styles)
        {
            var snapshots = new List<TextStyleSnapshot>(styles.Count);

            for (var i = 0; i < styles.Count; i++)
            {
                var style = styles[i];

                snapshots.Add(new TextStyleSnapshot(style));
            }

            return snapshots;
        }

        private static void RestoreStyleSnapshots(List<TextStyleSnapshot> snapshots)
        {
            for (var i = snapshots.Count - 1; i >= 0; i--)
            {
                snapshots[i].Restore();
            }
        }

        private static Texture2D MakeRoundedTexture(Color fillColor, int radius, int size = 32)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color[size * size];

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var alpha = ComputeRoundedRectAlpha(new Vector2(x + .5f, y + .5f), size, radius);

                    pixels[y * size + x] = new Color(fillColor.r, fillColor.g, fillColor.b, fillColor.a * alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);

            return texture;
        }

        private static Texture2D MakeSdfRoundedTexture(Color fillColor, int radius, int size = 32)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color[size * size];

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var alpha = ComputeFieldRoundedRectAlpha(new Vector2(x + .5f, y + .5f), size, radius);

                    pixels[y * size + x] = new Color(fillColor.r, fillColor.g, fillColor.b, fillColor.a * alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);

            return texture;
        }

        private static Texture2D MakeRoundedFieldTexture(
            Color fillColor,
            Color shadowColor,
            int radius,
            int shadowSize,
            bool drawCheckmark = false,
            int horizontalInset = 0,
            int size = 32)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color[size * size];

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var pixelCenter = new Vector2(x + .5f, y + .5f);
                    var alpha = ComputeFieldRoundedRectAlpha(pixelCenter, size, radius, horizontalInset);
                    var shadow = ComputeInsetShadow(pixelCenter, size, shadowSize);
                    var color = Color.Lerp(fillColor, shadowColor, shadow);

                    pixels[y * size + x] = new Color(color.r, color.g, color.b, color.a * alpha);
                }
            }

            if (drawCheckmark)
            {
                DrawCheckmark(pixels, size, _imGuiToggleCheckColor);
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);

            return texture;
        }

        private static void DrawCheckmark(Color[] pixels, int size, Color color)
        {
            DrawLine(pixels, size, new Vector2(9.5f, 16f), new Vector2(14f, 20.5f), color, 2.2f);
            DrawLine(pixels, size, new Vector2(14f, 20.5f), new Vector2(23f, 10.5f), color, 2.2f);
        }

        private static void DrawLine(Color[] pixels, int size, Vector2 start, Vector2 end, Color color, float thickness)
        {
            var minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(start.x, end.x) - thickness));
            var maxX = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(start.x, end.x) + thickness));
            var minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(start.y, end.y) - thickness));
            var maxY = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(start.y, end.y) + thickness));
            var segment = end - start;
            var segmentLengthSquared = segment.sqrMagnitude;

            if (segmentLengthSquared <= Mathf.Epsilon)
            {
                return;
            }

            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var point = new Vector2(x + .5f, y + .5f);
                    var t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / segmentLengthSquared);
                    var closest = start + segment * t;
                    var distance = Vector2.Distance(point, closest);
                    var alpha = Mathf.Clamp01((thickness * .5f) + .75f - distance);

                    if (alpha <= 0f)
                    {
                        continue;
                    }

                    var index = y * size + x;

                    pixels[index] = Color.Lerp(pixels[index], color, color.a * alpha);
                }
            }
        }

        private static float ComputeInsetShadow(Vector2 pixelCenter, int size, int shadowSize)
        {
            if (shadowSize <= 0)
            {
                return 0f;
            }

            var shadow = shadowSize;
            var left = Mathf.Clamp01((shadow - pixelCenter.x) / shadow);
            var right = Mathf.Clamp01((pixelCenter.x - (size - shadow)) / shadow);
            var top = Mathf.Clamp01((shadow - pixelCenter.y) / shadow);
            var bottom = Mathf.Clamp01((pixelCenter.y - (size - shadow)) / shadow);
            var vertical = Mathf.Max(top * .75f, bottom);
            var horizontal = Mathf.Max(left * .45f, right * .45f);

            return Mathf.Clamp01(Mathf.Max(vertical, horizontal));
        }

        private static float ComputeFieldRoundedRectAlpha(Vector2 pixelCenter, int size, int radius, int horizontalInset = 0)
        {
            var width = Mathf.Max(1f, size - horizontalInset * 2f);
            var halfSize = new Vector2(width * .5f, size * .5f);
            var r = Mathf.Clamp(radius, 0, Mathf.FloorToInt(Mathf.Min(halfSize.x, halfSize.y)));

            if (r <= 0)
            {
                return 1f;
            }

            var center = new Vector2(size * .5f, size * .5f);

            var cornerSpace = new Vector2(
                Mathf.Abs(pixelCenter.x - center.x),
                Mathf.Abs(pixelCenter.y - center.y)) - (halfSize - new Vector2(r, r));

            var outside = new Vector2(Mathf.Max(cornerSpace.x, 0f), Mathf.Max(cornerSpace.y, 0f)).magnitude;
            var inside = Mathf.Min(Mathf.Max(cornerSpace.x, cornerSpace.y), 0f);
            var signedDistance = outside + inside - r;

            return Mathf.Clamp01(.5f - signedDistance);
        }

        private static float ComputeRoundedRectAlpha(Vector2 pixelCenter, int size, int radius)
        {
            var max = size - 1f;
            var x = pixelCenter.x;
            var y = pixelCenter.y;
            var r = Mathf.Clamp(radius, 0, size / 2);

            if (x < r && y < r)
            {
                return DistanceAlpha(pixelCenter, new Vector2(r, r), r);
            }

            if (x > max - r && y < r)
            {
                return DistanceAlpha(pixelCenter, new Vector2(max - r, r), r);
            }

            if (x > max - r && y > max - r)
            {
                return DistanceAlpha(pixelCenter, new Vector2(max - r, max - r), r);
            }

            if (x < r && y > max - r)
            {
                return DistanceAlpha(pixelCenter, new Vector2(r, max - r), r);
            }

            return 1f;
        }

        private static float DistanceAlpha(Vector2 pixelCenter, Vector2 cornerCenter, float radius)
        {
            if (radius <= 0f)
            {
                return 1f;
            }

            var distance = Vector2.Distance(pixelCenter, cornerCenter);

            return Mathf.Clamp01(radius + .5f - distance);
        }

        private static Rect GetVisibleRect()
        {
            _guiClipVisibleRectProperty ??= Type.GetType("UnityEngine.GUIClip, UnityEngine")?.GetProperty(
                "visibleRect",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            if (_guiClipVisibleRectProperty != null)
            {
                return (Rect)_guiClipVisibleRectProperty.GetValue(null, null);
            }

            return new Rect(0f, 0f, EditorGUIUtility.currentViewWidth, 10000f);
        }
    }
}
