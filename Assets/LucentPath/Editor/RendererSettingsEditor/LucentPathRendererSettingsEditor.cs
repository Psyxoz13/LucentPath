using LucentPath.EditorUi.Core;
using LucentPath.Rendering;
using LucentPath.Rendering.Settings;
using LucentPath.Upscale.DLSS;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace LucentPath.EditorUi.RendererSettings
{
    [CustomEditor(typeof(LucentPathRendererSettings))]
    public partial class LucentPathRendererSettingsEditor : UnityEditor.Editor, IEditorUiToolkitValueSink
    {
        private const string SpherePreviewShaderName = "Hidden/LucentPath/Editor/Sphere";
        private const string ConePreviewShaderName = "Hidden/LucentPath/Editor/LightCone";
        private static readonly Vector2 _bounceDistanceSphereBaseNormalized = new Vector2(.78f, .46f);
        private static readonly Vector2 _bounceDistanceSphereDefaultOffset = new Vector2(40f, 10f);
        private static readonly Vector2 _lightingSphereRestNormalized = new Vector2(.5f, .5f);
        private const float SamplesPerPixelVisualizationFps = 2f;
        private const float BounceDistanceVisualizationFps = 30f;
        private const float BounceCountVisualizationFps = 24f;
        private const float LightingVisualizationFps = 24f;
        private const float TemporalHistoryVisualizationFps = 24f;
        private const float AtrousVisualizationFps = 24f;
        private const int LightingPreviewMaxShaderLights = 128;
        private const string SectionPrefsKeyPrefix = "LucentPath.EditorUi.RendererSettings.Section.";
        private const string ShadersSectionPrefsKey = SectionPrefsKeyPrefix + "Shaders";
        private const string DlssSectionPrefsKey = SectionPrefsKeyPrefix + "Dlss";
        private const string PathTracingSectionPrefsKey = SectionPrefsKeyPrefix + "PathTracing";
        private const string TemporalSectionPrefsKey = SectionPrefsKeyPrefix + "Temporal";
        private const string DebugSectionPrefsKey = SectionPrefsKeyPrefix + "Debug";
        private const string ReconstructionSectionPrefsKey = SectionPrefsKeyPrefix + "Reconstruction";

        private static readonly int _sphereShadowColorId = Shader.PropertyToID("_ShadowColor");
        private static readonly int _sphereMidColorId = Shader.PropertyToID("_MidColor");
        private static readonly int _sphereLightColorId = Shader.PropertyToID("_LightColor");
        private static readonly int _sphereRimColorId = Shader.PropertyToID("_RimColor");
        private static readonly int _sphereLightDirectionId = Shader.PropertyToID("_LightDirection");
        private static readonly int _sphereAmbientId = Shader.PropertyToID("_Ambient");
        private static readonly int _sphereSpecularStrengthId = Shader.PropertyToID("_SpecularStrength");
        private static readonly int _sphereGlossId = Shader.PropertyToID("_Gloss");
        private static readonly int _sphereFlipYId = Shader.PropertyToID("_FlipY");
        private static readonly int _sphereEditorLightCountId = Shader.PropertyToID("_EditorLightCount");
        private static readonly int _coneColorId = Shader.PropertyToID("_ConeColor");
        private static readonly int _coneDrawRectId = Shader.PropertyToID("_DrawRect");
        private static readonly int _coneOriginId = Shader.PropertyToID("_ConeOrigin");
        private static readonly int _coneDirectionId = Shader.PropertyToID("_ConeDirection");
        private static readonly int _coneLengthId = Shader.PropertyToID("_ConeLength");
        private static readonly int _coneBaseHalfWidthId = Shader.PropertyToID("_ConeBaseHalfWidth");
        private static readonly int _coneSpreadId = Shader.PropertyToID("_ConeSpread");
        private static readonly int _coneSphereCenterId = Shader.PropertyToID("_SphereCenter");
        private static readonly int _coneSphereRadiusId = Shader.PropertyToID("_SphereRadius");

        internal LucentPathRendererSettings Asset => target as LucentPathRendererSettings;

        private RendererSettingsEditorContext _uiContext;
        private EditorUiToolkitView _uiToolkitView;
        private EditorUiToolkitFields _uiToolkitFields;
        private IReadOnlyList<EditorSectionBase<RendererSettingsEditorContext>> _sections;

        private bool _isShadersShown;
        private bool _isDlssShown;
        private bool _isPathTracingShown;
        private bool _isTemporalShown;
        private bool _isDebugShown;
        private bool _isReconstructionShown;

        internal bool IsShadersSectionExpanded => _isShadersShown;
        internal bool IsDlssSectionExpanded => _isDlssShown;
        internal bool IsPathTracingSectionExpanded => _isPathTracingShown;
        internal bool IsTemporalSectionExpanded => _isTemporalShown;
        internal bool IsDebugSectionExpanded => _isDebugShown;
        internal bool IsReconstructionSectionExpanded => _isReconstructionShown;

        private EditorChrome _chrome;
        private GUIStyle _visualTitleStyle;
        private GUIStyle _miniLabelStyle;
        private GUIStyle _centerMiniLabelStyle;
        private GUIStyle _rayLabelStyle;
        private GUIStyle _legendLabelStyle;

        private Texture2D _visualTexture;
        private Shader _conePreviewShader;
        private Material _conePreviewMaterial;
        private Shader _spherePreviewShader;
        private Material _spherePreviewMaterial;
        private IMGUIContainer _bounceDistanceCardContainer;
        private IVisualElementScheduledItem _bounceDistanceCardTicker;
        private IMGUIContainer _bounceCountCardContainer;
        private IVisualElementScheduledItem _bounceCountCardTicker;
        private IMGUIContainer _lightingCardContainer;
        private IVisualElementScheduledItem _lightingCardTicker;
        private IMGUIContainer _samplesPerPixelCardContainer;
        private IVisualElementScheduledItem _samplesPerPixelCardTicker;
        private IMGUIContainer _temporalHistoryCardContainer;
        private IVisualElementScheduledItem _temporalHistoryCardTicker;
        private IMGUIContainer _atrousCardContainer;
        private IVisualElementScheduledItem _atrousCardTicker;

        private Color _panelSoftColor;
        private Color _fieldColor;
        private Color _lineColor;
        private Color _textColor;
        private Color _mutedTextColor;
        private Color _accentColor;
        private Color _accentSoftColor;
        private Color _warningColor;
        private Color _goodColor;
        private bool _isStylesBuiltForProSkin;
        private int _sampleVisualizationCachedCount = -1;
        private Vector2[] _sampleVisualizationPoints = Array.Empty<Vector2>();
        private bool _isBounceDistanceCardPointerInside;
        private bool _isBounceDistanceCardDragActive;
        private int _bounceDistanceCardDragPointerId = -1;
        private Vector2 _bounceDistanceCardPointerPosition;
        private Rect _bounceDistanceVisualizationRect;
        private Rect _bounceDistanceSphereCenterBoundsRect;
        private Vector2 _bounceDistanceSphereCurrentNormalized = _bounceDistanceSphereBaseNormalized;
        private Vector2 _bounceDistanceSphereTargetNormalized = _bounceDistanceSphereBaseNormalized;
        private bool _isBounceCountCardHovered;
        private Rect _bounceCountVisualizationRect;
        private float _bounceCountAnimationTime;
        private bool _isLightingCardHovered;
        private Vector2 _lightingCardPointerPosition;
        private Rect _lightingVisualizationRect;
        private Rect _lightingSphereCenterBoundsRect;
        private Vector2 _lightingSphereCurrentNormalized = _lightingSphereRestNormalized;
        private Vector2 _lightingSphereTargetNormalized = _lightingSphereRestNormalized;
        private bool _isLightingPreviewLightsInitialized;
        private int _lightingPreviewLayoutLightCount = -1;
        private readonly Vector2[] _lightingPreviewLightNormalizedPositions = new Vector2[LightingPreviewMaxShaderLights];
        private readonly Vector2[] _lightingPreviewLightDirections = new Vector2[LightingPreviewMaxShaderLights];
        private bool _isSamplesPerPixelCardHovered;
        private Rect _samplesPerPixelVisualizationRect;
        private bool _isTemporalHistoryCardHovered;
        private Rect _temporalHistoryVisualizationRect;
        private float _temporalHistoryAnimationTime;
        private float _temporalHistoryScatterDisplayStrength;
        private int _temporalHistoryScatterCycleIndex = -1;
        private readonly float[] _temporalHistoryScatterOffsets = new float[8];
        private bool _isAtrousCardHovered;
        private Rect _atrousVisualizationRect;
        private float _atrousAnimationTime;
        private float _atrousOrbitDisplayStrength;

        private void OnEnable()
        {
            Undo.undoRedoPerformed -= HandleUndoRedoPerformed;
            Undo.undoRedoPerformed += HandleUndoRedoPerformed;

            ResetStyles();
            LoadSectionVisibilityState();
            InvalidateSampleVisualization();

            _bounceDistanceSphereCurrentNormalized = _bounceDistanceSphereBaseNormalized;
            _bounceDistanceSphereTargetNormalized = _bounceDistanceSphereBaseNormalized;
            _isBounceDistanceCardDragActive = false;
            _bounceDistanceCardDragPointerId = -1;
            _isBounceCountCardHovered = false;
            _bounceCountAnimationTime = 0f;
            _isLightingCardHovered = false;
            _lightingSphereCurrentNormalized = _lightingSphereRestNormalized;
            _lightingSphereTargetNormalized = _lightingSphereRestNormalized;
            _isLightingPreviewLightsInitialized = false;
            _lightingPreviewLayoutLightCount = -1;
            _isSamplesPerPixelCardHovered = false;
            _isTemporalHistoryCardHovered = false;
            _temporalHistoryAnimationTime = 0f;
            _temporalHistoryScatterDisplayStrength = 0f;
            _temporalHistoryScatterCycleIndex = -1;
            _isAtrousCardHovered = false;
            _atrousAnimationTime = 0f;
            _atrousOrbitDisplayStrength = 0f;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= HandleUndoRedoPerformed;

            TeardownBounceDistanceCardTicker();
            TeardownBounceCountCardTicker();
            TeardownLightingCardTicker();
            TeardownSamplesPerPixelCardTicker();
            TeardownTemporalHistoryCardTicker();
            TeardownAtrousCardTicker();
            ResetStyles();
        }

        private void HandleUndoRedoPerformed()
        {
            if (target == null)
            {
                return;
            }

            serializedObject.UpdateIfRequiredOrScript();
            RefreshUiToolkitState();
            Repaint();
        }

        private void EnsureUiComposition()
        {
            _uiContext ??= new RendererSettingsEditorContext(this);
            _sections ??= CreateSections();
        }

        private IReadOnlyList<EditorSectionBase<RendererSettingsEditorContext>> CreateSections()
        {
            return new EditorSectionBase<RendererSettingsEditorContext>[]
            {
                new ShadersSection(this),
                new DlssSuperResolutionSection(this),
                new PathTracingSection(this),
                new ReconstructionSection(this),
                new TemporalDenoisingSection(this),
                new DebugSection(this)
            };
        }

        private void BuildSectionElement(VisualElement parent, EditorSectionBase<RendererSettingsEditorContext> section)
        {
            if (parent == null || section == null)
            {
                return;
            }

            parent.Add(_uiToolkitView.CreateSectionElement(section, _uiContext, BuildCardElement));
        }

        private void BuildCardElement(VisualElement parent, EditorCardBase<RendererSettingsEditorContext> card)
        {
            if (parent == null || card == null)
            {
                return;
            }

            parent.Add(_uiToolkitView.CreateCardElement(card, _uiContext));
        }

        public override VisualElement CreateInspectorGUI()
        {
            TeardownBounceDistanceCardTicker();
            TeardownBounceCountCardTicker();
            TeardownLightingCardTicker();
            TeardownSamplesPerPixelCardTicker();
            TeardownTemporalHistoryCardTicker();
            TeardownAtrousCardTicker();
            EnsureStyles();
            EnsureAssetState(Asset);
            EnsureUiComposition();
            EnsureUiToolkitInfrastructure();

            _uiToolkitView.Reset();

            var root = _uiToolkitView.CreateRoot();

            root.Add(CreateAssetHeaderElement());

            for (var i = 0; i < _sections.Count; i++)
            {
                var section = _sections[i];

                BuildSectionElement(root, section);
            }

            RefreshUiToolkitState();

            return root;
        }

        public override bool UseDefaultMargins()
        {
            return false;
        }

        private void TickBounceDistanceCard()
        {
            if (_bounceDistanceCardContainer == null)
            {
                return;
            }

            UpdateBounceDistanceTargetFromPointer();

            if ((_bounceDistanceSphereCurrentNormalized - _bounceDistanceSphereTargetNormalized).sqrMagnitude <= .000001f)
            {
                _bounceDistanceSphereCurrentNormalized = _bounceDistanceSphereTargetNormalized;

                if (_isBounceDistanceCardPointerInside == false)
                {
                    _bounceDistanceCardTicker?.Pause();
                }

                return;
            }

            _bounceDistanceSphereCurrentNormalized = Vector2.MoveTowards(
                _bounceDistanceSphereCurrentNormalized,
                _bounceDistanceSphereTargetNormalized,
                .16f);

            _bounceDistanceCardContainer.MarkDirtyRepaint();
        }

        private void UpdateBounceDistancePointer(bool inside, Vector2 localPosition)
        {
            _isBounceDistanceCardPointerInside = inside;

            if (inside)
            {
                _bounceDistanceCardPointerPosition = localPosition;
            }
            else
            {
                _bounceDistanceSphereTargetNormalized = GetBounceDistanceRestNormalized();
            }

            UpdateBounceDistanceTargetFromPointer();

            if (_bounceDistanceCardTicker == null)
            {
                return;
            }

            if (inside || (_bounceDistanceSphereCurrentNormalized - _bounceDistanceSphereTargetNormalized).sqrMagnitude > .000001f)
            {
                _bounceDistanceCardTicker.Resume();
            }
            else
            {
                _bounceDistanceCardTicker.Pause();
            }

            _bounceDistanceCardContainer?.MarkDirtyRepaint();
        }

        private bool IsBounceDistancePointerOverVisualization(Vector2 localPosition)
        {
            return _bounceDistanceVisualizationRect.width > 0f &&
                   _bounceDistanceVisualizationRect.height > 0f &&
                   _bounceDistanceVisualizationRect.Contains(localPosition);
        }

        private void UpdateBounceDistanceTargetFromPointer()
        {
            if (_isBounceDistanceCardPointerInside == false ||
                _bounceDistanceVisualizationRect.width <= 0f ||
                _bounceDistanceVisualizationRect.Contains(_bounceDistanceCardPointerPosition) == false ||
                _bounceDistanceSphereCenterBoundsRect.width <= 0f ||
                _bounceDistanceSphereCenterBoundsRect.height <= 0f)
            {
                return;
            }

            var clamped = new Vector2(
                Mathf.Clamp(
                    _bounceDistanceCardPointerPosition.x,
                    _bounceDistanceSphereCenterBoundsRect.xMin,
                    _bounceDistanceSphereCenterBoundsRect.xMax),
                Mathf.Clamp(
                    _bounceDistanceCardPointerPosition.y,
                    _bounceDistanceSphereCenterBoundsRect.yMin,
                    _bounceDistanceSphereCenterBoundsRect.yMax));

            _bounceDistanceSphereTargetNormalized = new Vector2(
                Mathf.InverseLerp(_bounceDistanceSphereCenterBoundsRect.xMin, _bounceDistanceSphereCenterBoundsRect.xMax, clamped.x),
                Mathf.InverseLerp(_bounceDistanceSphereCenterBoundsRect.yMin, _bounceDistanceSphereCenterBoundsRect.yMax, clamped.y));
        }

        private void TeardownBounceDistanceCardTicker()
        {
            _isBounceDistanceCardPointerInside = false;
            _isBounceDistanceCardDragActive = false;
            _bounceDistanceCardDragPointerId = -1;

            _bounceDistanceCardTicker?.Pause();

            _bounceDistanceCardTicker = null;
            _bounceDistanceCardContainer = null;
            _bounceDistanceVisualizationRect = default;
            _bounceDistanceSphereCenterBoundsRect = default;
            _bounceDistanceSphereCurrentNormalized = _bounceDistanceSphereBaseNormalized;
            _bounceDistanceSphereTargetNormalized = _bounceDistanceSphereBaseNormalized;
        }

        private void TickSamplesPerPixelCard()
        {
            if (_isSamplesPerPixelCardHovered == false || _samplesPerPixelCardContainer == null)
            {
                return;
            }

            InvalidateSampleVisualization();
            _samplesPerPixelCardContainer.MarkDirtyRepaint();
        }

        private void TickBounceCountCard()
        {
            if (_isBounceCountCardHovered == false || _bounceCountCardContainer == null)
            {
                return;
            }

            _bounceCountAnimationTime += 1f / Mathf.Max(.01f, BounceCountVisualizationFps);

            _bounceCountCardContainer.MarkDirtyRepaint();
        }

        private void TickLightingCard()
        {
            if (_lightingCardContainer == null)
            {
                return;
            }

            UpdateLightingTargetFromPointer();

            if ((_lightingSphereCurrentNormalized - _lightingSphereTargetNormalized).sqrMagnitude <= .000001f)
            {
                _lightingSphereCurrentNormalized = _lightingSphereTargetNormalized;

                if (_isLightingCardHovered == false)
                {
                    _lightingCardTicker?.Pause();
                }

                return;
            }

            _lightingSphereCurrentNormalized = Vector2.MoveTowards(_lightingSphereCurrentNormalized, _lightingSphereTargetNormalized, .14f);

            _lightingCardContainer.MarkDirtyRepaint();
        }

        private void TickTemporalHistoryCard()
        {
            if (_temporalHistoryCardContainer == null)
            {
                return;
            }

            var deltaTime = 1f / Mathf.Max(.01f, TemporalHistoryVisualizationFps);
            var targetScatterStrength = 0f;

            if (_isTemporalHistoryCardHovered)
            {
                _temporalHistoryAnimationTime += deltaTime;

                targetScatterStrength = EvaluateTemporalHistoryScatterStrength(_temporalHistoryAnimationTime);
            }

            _temporalHistoryScatterDisplayStrength = Mathf.MoveTowards(_temporalHistoryScatterDisplayStrength, targetScatterStrength, deltaTime * 3f);

            _temporalHistoryCardContainer.MarkDirtyRepaint();

            if (_isTemporalHistoryCardHovered == false && _temporalHistoryScatterDisplayStrength <= .0001f)
            {
                _temporalHistoryCardTicker?.Pause();
            }
        }

        private void TickAtrousCard()
        {
            if (_atrousCardContainer == null)
            {
                return;
            }

            var deltaTime = 1f / Mathf.Max(.01f, AtrousVisualizationFps);

            if (_isAtrousCardHovered)
            {
                _atrousAnimationTime += deltaTime;
            }

            var targetVisibility = _isAtrousCardHovered ? 1f : 0f;

            _atrousOrbitDisplayStrength = Mathf.MoveTowards(_atrousOrbitDisplayStrength, targetVisibility, deltaTime * 4f);

            _atrousCardContainer.MarkDirtyRepaint();

            if (_isAtrousCardHovered == false && _atrousOrbitDisplayStrength <= .0001f)
            {
                _atrousCardTicker?.Pause();
            }
        }

        private void SetSamplesPerPixelCardHovered(bool hovered)
        {
            if (_isSamplesPerPixelCardHovered == hovered)
            {
                return;
            }

            _isSamplesPerPixelCardHovered = hovered;

            if (_samplesPerPixelCardTicker == null)
            {
                return;
            }

            if (hovered)
            {
                InvalidateSampleVisualization();
                _samplesPerPixelCardTicker.Resume();
                _samplesPerPixelCardContainer?.MarkDirtyRepaint();
            }
            else
            {
                _samplesPerPixelCardTicker.Pause();
            }
        }

        private void SetBounceCountCardHovered(bool hovered)
        {
            if (_isBounceCountCardHovered == hovered)
            {
                return;
            }

            _isBounceCountCardHovered = hovered;

            if (_bounceCountCardTicker == null)
            {
                return;
            }

            if (hovered)
            {
                _bounceCountCardTicker.Resume();
                _bounceCountCardContainer?.MarkDirtyRepaint();
            }
            else
            {
                _bounceCountCardTicker.Pause();
                _bounceCountCardContainer?.MarkDirtyRepaint();
            }
        }

        private void SetTemporalHistoryCardHovered(bool hovered)
        {
            if (_isTemporalHistoryCardHovered == hovered)
            {
                return;
            }

            _isTemporalHistoryCardHovered = hovered;

            if (_temporalHistoryCardTicker == null)
            {
                return;
            }

            if (hovered)
            {
                _temporalHistoryAnimationTime = 0f;
                _temporalHistoryScatterDisplayStrength = 0f;
                _temporalHistoryScatterCycleIndex = -1;

                _temporalHistoryCardTicker.Resume();
                _temporalHistoryCardContainer?.MarkDirtyRepaint();
            }
            else
            {
                _temporalHistoryCardContainer?.MarkDirtyRepaint();
            }
        }

        private void SetAtrousCardHovered(bool hovered)
        {
            if (_isAtrousCardHovered == hovered)
            {
                return;
            }

            _isAtrousCardHovered = hovered;

            if (_atrousCardTicker == null)
            {
                return;
            }

            if (hovered)
            {
                _atrousCardTicker.Resume();
                _atrousCardContainer?.MarkDirtyRepaint();
            }
            else
            {
                _atrousCardContainer?.MarkDirtyRepaint();
            }
        }

        private void UpdateLightingPointer(bool inside, Vector2 localPosition)
        {
            _isLightingCardHovered = inside;

            if (inside)
            {
                _lightingCardPointerPosition = localPosition;
            }
            else
            {
                _lightingSphereTargetNormalized = _lightingSphereRestNormalized;
            }

            UpdateLightingTargetFromPointer();

            if (_lightingCardTicker == null)
            {
                return;
            }

            if (inside || (_lightingSphereCurrentNormalized - _lightingSphereTargetNormalized).sqrMagnitude > .000001f)
            {
                _lightingCardTicker.Resume();
            }
            else
            {
                _lightingCardTicker.Pause();
            }

            _lightingCardContainer?.MarkDirtyRepaint();
        }

        private bool IsSamplesPerPixelPointerOverVisualization(Vector2 localPosition)
        {
            return _samplesPerPixelVisualizationRect.width > 0f &&
                   _samplesPerPixelVisualizationRect.height > 0f &&
                   _samplesPerPixelVisualizationRect.Contains(localPosition);
        }

        private bool IsBounceCountPointerOverVisualization(Vector2 localPosition)
        {
            return _bounceCountVisualizationRect.width > 0f &&
                   _bounceCountVisualizationRect.height > 0f &&
                   _bounceCountVisualizationRect.Contains(localPosition);
        }

        private bool IsLightingPointerOverVisualization(Vector2 localPosition)
        {
            return _lightingVisualizationRect.width > 0f &&
                   _lightingVisualizationRect.height > 0f &&
                   _lightingVisualizationRect.Contains(localPosition);
        }

        private bool IsTemporalHistoryPointerOverVisualization(Vector2 localPosition)
        {
            return _temporalHistoryVisualizationRect.width > 0f &&
                   _temporalHistoryVisualizationRect.height > 0f &&
                   _temporalHistoryVisualizationRect.Contains(localPosition);
        }

        private bool IsAtrousPointerOverVisualization(Vector2 localPosition)
        {
            return _atrousVisualizationRect.width > 0f &&
                   _atrousVisualizationRect.height > 0f &&
                   _atrousVisualizationRect.Contains(localPosition);
        }

        private void UpdateLightingTargetFromPointer()
        {
            if (_isLightingCardHovered == false ||
                _lightingVisualizationRect.width <= 0f ||
                _lightingVisualizationRect.Contains(_lightingCardPointerPosition) == false ||
                _lightingSphereCenterBoundsRect.width <= 0f ||
                _lightingSphereCenterBoundsRect.height <= 0f)
            {
                return;
            }

            var clamped = new Vector2(
                Mathf.Clamp(_lightingCardPointerPosition.x, _lightingSphereCenterBoundsRect.xMin, _lightingSphereCenterBoundsRect.xMax),
                Mathf.Clamp(_lightingCardPointerPosition.y, _lightingSphereCenterBoundsRect.yMin, _lightingSphereCenterBoundsRect.yMax));

            _lightingSphereTargetNormalized = new Vector2(
                Mathf.InverseLerp(_lightingSphereCenterBoundsRect.xMin, _lightingSphereCenterBoundsRect.xMax, clamped.x),
                Mathf.InverseLerp(_lightingSphereCenterBoundsRect.yMin, _lightingSphereCenterBoundsRect.yMax, clamped.y));
        }

        private void TeardownSamplesPerPixelCardTicker()
        {
            _isSamplesPerPixelCardHovered = false;

            _samplesPerPixelCardTicker?.Pause();

            _samplesPerPixelCardTicker = null;
            _samplesPerPixelCardContainer = null;
            _samplesPerPixelVisualizationRect = default;
        }

        private void TeardownBounceCountCardTicker()
        {
            _isBounceCountCardHovered = false;

            _bounceCountCardTicker?.Pause();

            _bounceCountCardTicker = null;
            _bounceCountCardContainer = null;
            _bounceCountVisualizationRect = default;
            _bounceCountAnimationTime = 0f;
        }

        private void TeardownLightingCardTicker()
        {
            _isLightingCardHovered = false;

            _lightingCardTicker?.Pause();

            _lightingCardTicker = null;
            _lightingCardContainer = null;
            _lightingVisualizationRect = default;
            _lightingSphereCenterBoundsRect = default;
            _lightingSphereCurrentNormalized = _lightingSphereRestNormalized;
            _lightingSphereTargetNormalized = _lightingSphereRestNormalized;
        }

        private void TeardownTemporalHistoryCardTicker()
        {
            _isTemporalHistoryCardHovered = false;

            _temporalHistoryCardTicker?.Pause();

            _temporalHistoryCardTicker = null;
            _temporalHistoryCardContainer = null;
            _temporalHistoryVisualizationRect = default;
            _temporalHistoryAnimationTime = 0f;
            _temporalHistoryScatterDisplayStrength = 0f;
            _temporalHistoryScatterCycleIndex = -1;
        }

        private void TeardownAtrousCardTicker()
        {
            _isAtrousCardHovered = false;

            _atrousCardTicker?.Pause();

            _atrousCardTicker = null;
            _atrousCardContainer = null;
            _atrousVisualizationRect = default;
            _atrousAnimationTime = 0f;
            _atrousOrbitDisplayStrength = 0f;
        }

        private void RefreshTemporalHistoryScatterOffsets(int cycleIndex)
        {
            if (_temporalHistoryScatterCycleIndex == cycleIndex)
            {
                return;
            }

            _temporalHistoryScatterCycleIndex = cycleIndex;

            for (var i = 0; i < _temporalHistoryScatterOffsets.Length; i++)
            {
                var sign = UnityEngine.Random.value < .5f ? -1f : 1f;
                var magnitude = UnityEngine.Random.Range(.35f, 1f);

                _temporalHistoryScatterOffsets[i] = sign * magnitude;
            }
        }

        private float EvaluateTemporalHistoryScatterStrength(float animationTime)
        {
            const float scatterPeakT = .24f;
            const float scatterReturnT = .56f;
            const float scatterCycleDuration = 3.4f;

            var scatterCycleIndex = Mathf.FloorToInt(animationTime / scatterCycleDuration);

            RefreshTemporalHistoryScatterOffsets(scatterCycleIndex);

            var scatterCycleT = Mathf.Repeat(animationTime, scatterCycleDuration) / scatterCycleDuration;

            if (scatterCycleT < scatterPeakT)
            {
                return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, scatterPeakT, scatterCycleT));
            }

            if (scatterCycleT < scatterReturnT)
            {
                return Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(scatterPeakT, scatterReturnT, scatterCycleT));
            }

            return 0f;
        }

        internal void OnPathTracingSectionVisibilityChanged(bool visible)
        {
            SetSectionVisibility(PathTracingSectionPrefsKey, ref _isPathTracingShown, visible);

            if (visible == false)
            {
                ResetBounceDistanceVisualizationState();
                ResetLightingVisualizationState();
                SetBounceCountCardHovered(false);
                SetSamplesPerPixelCardHovered(false);
            }
        }

        internal void OnTemporalSectionVisibilityChanged(bool visible)
        {
            SetSectionVisibility(TemporalSectionPrefsKey, ref _isTemporalShown, visible);

            if (visible == false)
            {
                SetTemporalHistoryCardHovered(false);
                SetAtrousCardHovered(false);
            }
        }

        internal void SetDebugSectionVisibility(bool visible)
        {
            SetSectionVisibility(DebugSectionPrefsKey, ref _isDebugShown, visible);
        }

        private void LoadSectionVisibilityState()
        {
            _isShadersShown = EditorPrefs.GetBool(ShadersSectionPrefsKey, false);
            _isDlssShown = EditorPrefs.GetBool(DlssSectionPrefsKey, false);
            _isPathTracingShown = EditorPrefs.GetBool(PathTracingSectionPrefsKey, false);
            _isTemporalShown = EditorPrefs.GetBool(TemporalSectionPrefsKey, false);
            _isDebugShown = EditorPrefs.GetBool(DebugSectionPrefsKey, false);
            _isReconstructionShown = EditorPrefs.GetBool(ReconstructionSectionPrefsKey, false);
        }

        internal void SetShadersSectionVisibility(bool visible)
        {
            SetSectionVisibility(ShadersSectionPrefsKey, ref _isShadersShown, visible);
        }

        internal void SetDlssSectionVisibility(bool visible)
        {
            SetSectionVisibility(DlssSectionPrefsKey, ref _isDlssShown, visible);
        }

        internal void SetReconstructionSectionVisibility(bool visible)
        {
            SetSectionVisibility(ReconstructionSectionPrefsKey, ref _isReconstructionShown, visible);
        }

        private static void SetSectionVisibility(string prefsKey, ref bool field, bool visible)
        {
            field = visible;

            EditorPrefs.SetBool(prefsKey, visible);
        }

        private void ResetBounceDistanceVisualizationState()
        {
            _isBounceDistanceCardPointerInside = false;
            _isBounceDistanceCardDragActive = false;
            _bounceDistanceCardDragPointerId = -1;

            var restNormalized = GetBounceDistanceRestNormalized();

            _bounceDistanceSphereCurrentNormalized = restNormalized;
            _bounceDistanceSphereTargetNormalized = restNormalized;

            _bounceDistanceCardTicker?.Pause();
            _bounceDistanceCardContainer?.MarkDirtyRepaint();
        }

        private void ResetLightingVisualizationState()
        {
            _isLightingCardHovered = false;
            _lightingSphereCurrentNormalized = _lightingSphereRestNormalized;
            _lightingSphereTargetNormalized = _lightingSphereRestNormalized;

            _lightingCardTicker?.Pause();
            _lightingCardContainer?.MarkDirtyRepaint();
        }

        private Vector2 GetBounceDistanceRestNormalized()
        {
            if (_bounceDistanceSphereCenterBoundsRect.width <= 0f || _bounceDistanceSphereCenterBoundsRect.height <= 0f)
            {
                return _bounceDistanceSphereBaseNormalized;
            }

            var restPosition = new Vector2(
                Mathf.Lerp(
                    _bounceDistanceSphereCenterBoundsRect.xMin,
                    _bounceDistanceSphereCenterBoundsRect.xMax,
                    _bounceDistanceSphereBaseNormalized.x) + _bounceDistanceSphereDefaultOffset.x,
                Mathf.Lerp(
                    _bounceDistanceSphereCenterBoundsRect.yMin,
                    _bounceDistanceSphereCenterBoundsRect.yMax,
                    _bounceDistanceSphereBaseNormalized.y) + _bounceDistanceSphereDefaultOffset.y);

            restPosition = new Vector2(
                Mathf.Clamp(restPosition.x, _bounceDistanceSphereCenterBoundsRect.xMin, _bounceDistanceSphereCenterBoundsRect.xMax),
                Mathf.Clamp(restPosition.y, _bounceDistanceSphereCenterBoundsRect.yMin, _bounceDistanceSphereCenterBoundsRect.yMax));

            return new Vector2(
                Mathf.InverseLerp(_bounceDistanceSphereCenterBoundsRect.xMin, _bounceDistanceSphereCenterBoundsRect.xMax, restPosition.x),
                Mathf.InverseLerp(_bounceDistanceSphereCenterBoundsRect.yMin, _bounceDistanceSphereCenterBoundsRect.yMax, restPosition.y));
        }

        private void GenerateLightingPreviewLightLayout()
        {
            var paddingMin = .08f;
            var paddingMax = .92f;

            for (var i = 0; i < LightingPreviewMaxShaderLights; i++)
            {
                _lightingPreviewLightNormalizedPositions[i] = new Vector2(
                    UnityEngine.Random.Range(paddingMin, paddingMax),
                    UnityEngine.Random.Range(paddingMin, paddingMax));

                var direction = UnityEngine.Random.insideUnitCircle;

                if (direction.sqrMagnitude <= .0001f)
                {
                    direction = Vector2.right;
                }

                _lightingPreviewLightDirections[i] = direction.normalized;
            }

            _isLightingPreviewLightsInitialized = true;
        }

        private void EnsureLightingPreviewLightLayout(int lightCount)
        {
            var clampedLightCount = Mathf.Clamp(lightCount, 1, LightingPreviewMaxShaderLights);

            if (_isLightingPreviewLightsInitialized && _lightingPreviewLayoutLightCount == clampedLightCount)
            {
                return;
            }

            GenerateLightingPreviewLightLayout();

            _lightingPreviewLayoutLightCount = clampedLightCount;
        }

        private bool IsUsedByCurrentRenderPipeline(LucentPathRendererSettings asset)
        {
            if (asset == null)
            {
                return false;
            }

            if (GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset universalAsset)
            {
                return false;
            }

            var rendererDataList = universalAsset.rendererDataList;

            for (var i = 0; i < rendererDataList.Length; i++)
            {
                if (rendererDataList[i] != null && IsUsedByRendererData(rendererDataList[i], asset))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsUsedByRendererData(ScriptableRendererData rendererData, LucentPathRendererSettings asset)
        {
            var features = rendererData.rendererFeatures;

            for (var i = 0; i < features.Count; i++)
            {
                if (features[i] is LucentPathRendererFeature feature && feature.isActive && feature.Settings == asset)
                {
                    return true;
                }
            }

            return false;
        }

        private void DrawBounceDistanceVisualization(float distance)
        {
            var rect = BeginVisualization(190f, string.Empty);

            _bounceDistanceVisualizationRect = rect;

            var inner = InsetVisualization(rect);
            var gridRect = new Rect(inner.x, inner.y + 12f, inner.width, inner.height - 12f);

            DrawGrid(gridRect);

            const float legendHeight = 34f;
            var cameraCenter = new Vector2(inner.x + 28f, inner.center.y - 6f);
            var cameraRect = GetCameraIconRect(cameraCenter);
            var rayY = cameraRect.center.y;
            var sphereRadius = Mathf.Min(36f, inner.height * .24f);
            var incomingStart = new Vector2(cameraRect.xMax - 2f, rayY);
            var primaryDirection = Vector2.right;
            var primaryMaxDistance = Mathf.Max(1f, inner.xMax - 12f - incomingStart.x);
            var primaryEnd = incomingStart + primaryDirection * primaryMaxDistance;

            _bounceDistanceSphereCenterBoundsRect = new Rect(
                cameraRect.xMax + 8f + sphereRadius,
                inner.y + 12f + sphereRadius,
                Mathf.Max(1f, inner.xMax - 4f - sphereRadius - (cameraRect.xMax + 8f + sphereRadius)),
                Mathf.Max(1f, inner.yMax - legendHeight + 6f - sphereRadius - (inner.y + 12f + sphereRadius)));

            if (_isBounceDistanceCardPointerInside == false && _isBounceDistanceCardDragActive == false)
            {
                var restNormalized = GetBounceDistanceRestNormalized();

                if ((_bounceDistanceSphereCurrentNormalized - _bounceDistanceSphereTargetNormalized).sqrMagnitude <= .000001f)
                {
                    _bounceDistanceSphereCurrentNormalized = restNormalized;
                }

                _bounceDistanceSphereTargetNormalized = restNormalized;
            }

            UpdateBounceDistanceTargetFromPointer();

            var sphereCenter = new Vector2(
                Mathf.Lerp(
                    _bounceDistanceSphereCenterBoundsRect.xMin,
                    _bounceDistanceSphereCenterBoundsRect.xMax,
                    _bounceDistanceSphereCurrentNormalized.x),
                Mathf.Lerp(
                    _bounceDistanceSphereCenterBoundsRect.yMin,
                    _bounceDistanceSphereCenterBoundsRect.yMax,
                    _bounceDistanceSphereCurrentNormalized.y));

            var primaryDistance = GetGameCameraFarDistance(primaryMaxDistance);
            var clampedDistance = Mathf.Min(Mathf.Max(0f, distance), primaryDistance);

            var hasHit = TryRayCircleIntersection(
                incomingStart,
                primaryDirection,
                sphereCenter,
                sphereRadius,
                primaryMaxDistance,
                out float hitDistance,
                out Vector2 hit);

            DrawCamera(cameraCenter);
            DrawSphere(sphereCenter, sphereRadius);

            Handles.BeginGUI();

            if (hasHit)
            {
                var bounceInset = Mathf.Min(Mathf.Max(5f, sphereRadius * .18f), sphereRadius - 2f);
                var bounceOrigin = hit + primaryDirection * bounceInset;

                var normal = (bounceOrigin - sphereCenter).sqrMagnitude > .0001f
                    ? (bounceOrigin - sphereCenter).normalized
                    : Vector2.left;

                var reflectedDir = Vector2.Reflect(primaryDirection, normal).normalized;
                var reflectedLength = Mathf.Lerp(sphereRadius * 1.4f, primaryMaxDistance * .72f, clampedDistance / Mathf.Max(.001f, primaryDistance));

                var reflectedEnd = ClampRayToRect(
                    bounceOrigin,
                    reflectedDir,
                    reflectedLength,
                    new Rect(inner.x + 8f, inner.y + 8f, inner.width - 16f, inner.height - legendHeight - 10f));

                DrawLine(bounceOrigin, reflectedEnd, _accentColor, 4f);
                DrawLine(incomingStart, bounceOrigin, Color.white, 3f);
            }
            else
            {
                DrawLine(incomingStart, primaryEnd, Color.white, 3f);
            }

            Handles.EndGUI();

            var legendRect = new Rect(inner.x + 4f, inner.yMax - 38f, inner.width - 8f, 32f);

            DrawLegendLine(
                new Vector2(legendRect.x + 4f, legendRect.y + 8f),
                Color.white,
                "primary ray (" + FormatFloat(primaryDistance) + " units)");

            DrawLegendLine(
                new Vector2(legendRect.x + 4f, legendRect.y + 24f),
                _accentColor,
                "bounce ray (" + FormatFloat(clampedDistance) + " units)");

            EndVisualization();
        }

        private void DrawBounceCountVisualization(int maxBounces, int russianRouletteStart)
        {
            var rect = BeginVisualization(124f, "Visualization: path chain length");

            _bounceCountVisualizationRect = rect;

            var inner = InsetVisualization(rect);
            const float legendHeight = 32f;
            const float legendBottomPadding = 6f;
            const float chainToLegendGap = 6f;
            var legendRect = new Rect(inner.x + 4f, inner.yMax - legendHeight - legendBottomPadding, inner.width - 8f, legendHeight);
            var count = Mathf.Clamp(maxBounces, 1, 16);
            var chainMinX = inner.x + 8f;
            var chainMaxX = inner.xMax - 8f;
            var topY = inner.y + 2f;
            var bottomY = legendRect.y - chainToLegendGap;
            var centerY = (topY + bottomY) * .5f;
            var normalizedCount = (count - 1f) / 15f;
            var lineWidth = Mathf.Lerp(3f, 2f, normalizedCount);
            var pointRadius = Mathf.Lerp(4f, 3f, normalizedCount);
            var points = new Vector2[count + 1];
            var animationTime = _bounceCountAnimationTime;
            const float waveAmplitude = 19f;
            const float waveSpeed = 3.2f;
            const float phaseStep = .65f;

            points[0] = new Vector2(chainMinX, centerY);

            for (var i = 1; i <= count; i++)
            {
                var x = Mathf.Lerp(chainMinX, chainMaxX, i / (float)count);
                var phase = animationTime * waveSpeed + i * phaseStep + (i % 2 == 0 ? Mathf.PI : 0f);
                var y = centerY + Mathf.Sin(phase) * waveAmplitude;

                y = Mathf.Clamp(y, topY, bottomY);
                points[i] = new Vector2(x, y);
            }

            Handles.BeginGUI();

            for (var i = 0; i < count; i++)
            {
                var color = i + 1 >= russianRouletteStart ? _warningColor : _accentColor;

                Handles.color = color;

                Handles.DrawAAPolyLine(lineWidth, ToVector3(points[i]), ToVector3(points[i + 1]));
            }

            for (var i = 0; i <= count; i++)
            {
                var color = i == 0
                    ? Color.white
                    : (i >= russianRouletteStart ? _warningColor : _accentColor);

                Handles.color = color;

                Handles.DrawSolidDisc(ToVector3(points[i]), Vector3.forward, pointRadius);
            }

            Handles.EndGUI();

            DrawLegendLine(new Vector2(legendRect.x + 4f, legendRect.y + 10f), _accentColor, "continues without Russian Roulette");
            DrawLegendLine(new Vector2(legendRect.x + 4f, legendRect.y + 26f), _warningColor, "subject to Russian Roulette");
            EndVisualization();
        }

        private void DrawSamplesVisualization(int samplesPerPixel)
        {
            var rect = BeginVisualization(128f, string.Empty);

            _samplesPerPixelVisualizationRect = rect;

            var inner = Inset(rect, 10f, 8f, 10f, 10f);
            var pixelRect = new Rect(inner.x + 4f, inner.y + 4f, inner.width - 8f, inner.height - 26f);

            EditorGUI.DrawRect(pixelRect, new Color(.08f, .1f, .13f, 1f));
            DrawRectOutline(pixelRect, _lineColor);

            var count = Mathf.Clamp(samplesPerPixel, 1, 16);
            var points = GetSampleVisualizationPoints(count);
            var pointInsetX = Mathf.Min(14f, pixelRect.width * .05f);
            var pointInsetY = Mathf.Min(10f, pixelRect.height * .12f);

            Handles.BeginGUI();

            for (var i = 0; i < count; i++)
            {
                var p = new Vector2(
                    Mathf.Lerp(pixelRect.x + pointInsetX, pixelRect.xMax - pointInsetX, points[i].x),
                    Mathf.Lerp(pixelRect.y + pointInsetY, pixelRect.yMax - pointInsetY, points[i].y));

                Handles.color = _accentColor;

                Handles.DrawSolidDisc(ToVector3(p), Vector3.forward, 4.2f);

                Handles.color = _accentSoftColor;

                Handles.DrawWireDisc(ToVector3(p), Vector3.forward, 7f);
            }

            Handles.EndGUI();

            GUI.Label(
                new Rect(inner.x, inner.yMax - 18f, inner.width, 18f),
                "SPP: " + samplesPerPixel + "  ->  less noise, higher frame cost",
                _centerMiniLabelStyle);

            EndVisualization();
        }

        private Vector2[] GetSampleVisualizationPoints(int count)
        {
            if (_sampleVisualizationCachedCount == count && _sampleVisualizationPoints.Length == count)
            {
                return _sampleVisualizationPoints;
            }

            _sampleVisualizationCachedCount = count;
            _sampleVisualizationPoints = new Vector2[count];

            var columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(count * 2f)));
            var rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)columns));
            var cells = new int[columns * rows];

            for (var i = 0; i < cells.Length; i++)
            {
                cells[i] = i;
            }

            for (var i = cells.Length - 1; i > 0; i--)
            {
                var swapIndex = UnityEngine.Random.Range(0, i + 1);
                var value = cells[i];

                cells[i] = cells[swapIndex];
                cells[swapIndex] = value;
            }

            for (var i = 0; i < count; i++)
            {
                var cellIndex = cells[i];
                var column = cellIndex % columns;
                var row = cellIndex / columns;
                var x = (column + .18f + UnityEngine.Random.value * .64f) / columns;
                var y = (row + .2f + UnityEngine.Random.value * .6f) / rows;

                _sampleVisualizationPoints[i] = new Vector2(x, y);
            }

            return _sampleVisualizationPoints;
        }

        internal void InvalidateSampleVisualization()
        {
            _sampleVisualizationCachedCount = -1;
        }

        private static bool TryRayCircleIntersection(Vector2 origin, Vector2 direction, Vector2 center, float radius, float maxDistance, out float hitDistance, out Vector2 hitPoint)
        {
            var offset = origin - center;
            var halfB = Vector2.Dot(offset, direction);
            var c = offset.sqrMagnitude - radius * radius;
            var discriminant = halfB * halfB - c;

            if (discriminant < 0f)
            {
                hitDistance = 0f;
                hitPoint = default;

                return false;
            }

            var sqrtDiscriminant = Mathf.Sqrt(discriminant);
            var t = -halfB - sqrtDiscriminant;

            if (t < 0f)
            {
                t = -halfB + sqrtDiscriminant;
            }

            if (t < 0f || t > maxDistance)
            {
                hitDistance = 0f;
                hitPoint = default;

                return false;
            }

            hitDistance = t;
            hitPoint = origin + direction * t;

            return true;
        }

        private static Vector2 ClampRayToRect(Vector2 origin, Vector2 direction, float length, Rect rect)
        {
            var scale = 1f;

            if (direction.x < -.0001f)
            {
                scale = Mathf.Min(scale, (rect.xMin - origin.x) / (direction.x * length));
            }
            else if (direction.x > .0001f)
            {
                scale = Mathf.Min(scale, (rect.xMax - origin.x) / (direction.x * length));
            }

            if (direction.y < -.0001f)
            {
                scale = Mathf.Min(scale, (rect.yMin - origin.y) / (direction.y * length));
            }
            else if (direction.y > .0001f)
            {
                scale = Mathf.Min(scale, (rect.yMax - origin.y) / (direction.y * length));
            }

            return origin + direction * length * Mathf.Clamp01(scale);
        }

        private void DrawLightVisualization(int maxLights, float intensity)
        {
            GUILayout.Space(6f);

            var rect = GUILayoutUtility.GetAspectRect(4f / 3f, GUILayout.ExpandWidth(true));

            _lightingVisualizationRect = rect;

            GUI.DrawTexture(rect, _visualTexture, ScaleMode.StretchToFill);

            var stage = rect;
            var widthScale = Mathf.Clamp(stage.width / 420f, .4f, 1f);
            var sphereRadius = Mathf.Max(4f, stage.width * .0475f);

            var lightMarkerRadius = Mathf.Lerp(
                2.6f,
                .95f,
                Mathf.InverseLerp(1f, LightingPreviewMaxShaderLights, Mathf.Clamp(maxLights, 1, LightingPreviewMaxShaderLights))) * widthScale;

            var boundaryPadding = sphereRadius + 2f;

            _lightingSphereCenterBoundsRect = new Rect(
                stage.xMin + boundaryPadding,
                stage.yMin + boundaryPadding,
                Mathf.Max(1f, stage.width - boundaryPadding * 2f),
                Mathf.Max(1f, stage.height - boundaryPadding * 2f));

            if (_isLightingCardHovered == false)
            {
                if ((_lightingSphereCurrentNormalized - _lightingSphereTargetNormalized).sqrMagnitude <= .000001f)
                {
                    _lightingSphereCurrentNormalized = _lightingSphereRestNormalized;
                }

                _lightingSphereTargetNormalized = _lightingSphereRestNormalized;
            }

            UpdateLightingTargetFromPointer();

            var sphereCenter = new Vector2(
                Mathf.Lerp(_lightingSphereCenterBoundsRect.xMin, _lightingSphereCenterBoundsRect.xMax, _lightingSphereCurrentNormalized.x),
                Mathf.Lerp(_lightingSphereCenterBoundsRect.yMin, _lightingSphereCenterBoundsRect.yMax, _lightingSphereCurrentNormalized.y));

            if (_lightingSphereCenterBoundsRect.width <= 1f || _lightingSphereCenterBoundsRect.height <= 1f)
            {
                sphereCenter = stage.center;
            }

            DrawLightingPreview(stage, sphereCenter, sphereRadius, lightMarkerRadius, maxLights, intensity);
            EndVisualization();
        }

        private void DrawLightingPreview(Rect stage, Vector2 sphereCenter, float sphereRadius, float lightMarkerRadius, int maxLights, float directIntensity)
        {
            EnsureLightingPreviewLightLayout(maxLights);

            var lightCount = Mathf.Clamp(maxLights, 1, LightingPreviewMaxShaderLights);
            var lightCountT = Mathf.InverseLerp(1f, LightingPreviewMaxShaderLights, lightCount);
            var widthScale = Mathf.Clamp(stage.width / 420f, .4f, 1f);
            var directIntensitySafe = Mathf.Max(0f, directIntensity);
            var intensityT = 1f - Mathf.Exp(-directIntensitySafe * .42f);
            var lightColor = Color.Lerp(_warningColor, Color.white, Mathf.Lerp(.10f, .34f, intensityT));
            var coneFillColor = new Color(lightColor.r, lightColor.g, lightColor.b, directIntensitySafe * .08f);
            var sourceGlowColor = lightColor * Mathf.Max(0f, directIntensitySafe * .35f);

            sourceGlowColor.a = directIntensitySafe * .16f;

            var sourceColor = lightColor * Mathf.Max(0f, directIntensitySafe);

            sourceColor.a = directIntensitySafe * .58f;

            var sourceRadius = stage.width * Mathf.Lerp(.02f, .01f, lightCountT) * widthScale;

            sourceRadius = Mathf.Clamp(sourceRadius, .9f, sphereRadius * .28f);

            var sourceGlowRadius = Mathf.Max(sourceRadius * 1.5f, sourceRadius + .95f * widthScale);
            var stagePadding = sourceGlowRadius;
            const float coneHalfAngleDegrees = 15f;
            var coneHalfAngleRadians = coneHalfAngleDegrees * Mathf.Deg2Rad;
            var safeRect = stage;

            for (var i = 0; i < lightCount; i++)
            {
                var normalizedLightPosition = _lightingPreviewLightNormalizedPositions[i];

                var sourceCenter = new Vector2(
                    Mathf.Lerp(stage.xMin + stagePadding, stage.xMax - stagePadding, normalizedLightPosition.x),
                    Mathf.Lerp(stage.yMin + stagePadding, stage.yMax - stagePadding, normalizedLightPosition.y));

                var direction = _lightingPreviewLightDirections[i];
                var coneOrigin = sourceCenter;
                var fullConeEnd = ClampRayToRect(coneOrigin, direction, stage.width + stage.height, safeRect);
                var fullConeLength = Vector2.Distance(coneOrigin, fullConeEnd);

                if (fullConeLength > 2f)
                {
                    DrawLightingCone(
                        safeRect,
                        coneOrigin,
                        direction,
                        fullConeLength,
                        coneHalfAngleRadians,
                        sourceRadius * .55f,
                        sphereCenter,
                        sphereRadius,
                        coneFillColor);
                }
            }

            Handles.BeginGUI();
            DrawLightingBall(sphereCenter, sphereRadius);

            for (var i = 0; i < lightCount; i++)
            {
                var normalizedLightPosition = _lightingPreviewLightNormalizedPositions[i];

                var sourceCenter = new Vector2(
                    Mathf.Lerp(stage.xMin + stagePadding, stage.xMax - stagePadding, normalizedLightPosition.x),
                    Mathf.Lerp(stage.yMin + stagePadding, stage.yMax - stagePadding, normalizedLightPosition.y));

                Handles.color = sourceGlowColor;

                Handles.DrawSolidDisc(ToVector3(sourceCenter), Vector3.forward, sourceGlowRadius);

                Handles.color = sourceColor;

                Handles.DrawSolidDisc(ToVector3(sourceCenter), Vector3.forward, sourceRadius);
            }

            Handles.EndGUI();
        }

        private void DrawLightingBall(Vector2 center, float radius)
        {
            var fillColor = EditorGUIUtility.isProSkin
                ? Color.Lerp(_fieldColor, _accentColor, .16f)
                : Color.Lerp(_panelSoftColor, _accentColor, .08f);

            Handles.color = fillColor;

            Handles.DrawSolidDisc(ToVector3(center), Vector3.forward, radius);
        }

        private void DrawLightingCone(Rect clipRect, Vector2 origin, Vector2 direction, float fullLength, float halfAngleRadians, float baseHalfWidth, Vector2 sphereCenter, float sphereRadius, Color fillColor)
        {
            EnsureConePreviewMaterial();

            if (_conePreviewMaterial == null)
            {
                return;
            }

            var spread = Mathf.Tan(halfAngleRadians);

            if (fullLength <= 1f)
            {
                return;
            }

            var normal = new Vector2(-direction.y, direction.x);
            var endCenter = origin + direction * fullLength;
            var endHalfWidth = baseHalfWidth + spread * fullLength;
            var startLeft = origin + normal * baseHalfWidth;
            var startRight = origin - normal * baseHalfWidth;
            var endLeft = endCenter + normal * endHalfWidth;
            var endRight = endCenter - normal * endHalfWidth;
            var minX = Mathf.Min(Mathf.Min(startLeft.x, startRight.x), Mathf.Min(endLeft.x, endRight.x));
            var minY = Mathf.Min(Mathf.Min(startLeft.y, startRight.y), Mathf.Min(endLeft.y, endRight.y));
            var maxX = Mathf.Max(Mathf.Max(startLeft.x, startRight.x), Mathf.Max(endLeft.x, endRight.x));
            var maxY = Mathf.Max(Mathf.Max(startLeft.y, startRight.y), Mathf.Max(endLeft.y, endRight.y));

            var drawRect = Rect.MinMaxRect(
                Mathf.Max(clipRect.xMin, minX - 2f),
                Mathf.Max(clipRect.yMin, minY - 2f),
                Mathf.Min(clipRect.xMax, maxX + 2f),
                Mathf.Min(clipRect.yMax, maxY + 2f));

            if (drawRect.width <= 1f || drawRect.height <= 1f)
            {
                return;
            }

            _conePreviewMaterial.SetColor(_coneColorId, fillColor);
            _conePreviewMaterial.SetVector(_coneDrawRectId, new Vector4(drawRect.x, drawRect.y, drawRect.width, drawRect.height));
            _conePreviewMaterial.SetVector(_coneOriginId, new Vector4(origin.x, origin.y, 0f, 0f));
            _conePreviewMaterial.SetVector(_coneDirectionId, new Vector4(direction.x, direction.y, 0f, 0f));
            _conePreviewMaterial.SetFloat(_coneLengthId, fullLength);
            _conePreviewMaterial.SetFloat(_coneBaseHalfWidthId, baseHalfWidth);
            _conePreviewMaterial.SetFloat(_coneSpreadId, spread);
            _conePreviewMaterial.SetVector(_coneSphereCenterId, new Vector4(sphereCenter.x, sphereCenter.y, 0f, 0f));
            _conePreviewMaterial.SetFloat(_coneSphereRadiusId, sphereRadius);
            EditorGUI.DrawPreviewTexture(drawRect, Texture2D.whiteTexture, _conePreviewMaterial, ScaleMode.StretchToFill);
        }

        internal void DrawBiasVisualization(float bias)
        {
            var rect = BeginVisualization(132f, "Visualization: new ray origin is offset from the surface");
            var inner = InsetVisualization(rect);
            const float surfaceBottomInset = 23f;
            var surfaceY = inner.yMax - surfaceBottomInset;
            var hit = new Vector2(inner.center.x - 40f, surfaceY);
            var pixelLift = Mathf.Clamp(bias * 10f, 0f, Mathf.Max(0f, surfaceY - inner.y - 12f));
            var origin = hit + new Vector2(0f, -pixelLift);
            var rayDirection = new Vector2(Mathf.Cos(15f * Mathf.Deg2Rad), -Mathf.Sin(15f * Mathf.Deg2Rad));
            var horizontalRun = Mathf.Max(24f, Mathf.Min(92f, inner.xMax - 18f - origin.x));
            var rayLength = horizontalRun / Mathf.Max(.001f, rayDirection.x);
            var end = ClampRayToRect(origin, rayDirection, rayLength, new Rect(inner.x + 8f, inner.y + 8f, inner.width - 16f, inner.height - 12f));

            Handles.BeginGUI();

            Handles.color = _lineColor;

            Handles.DrawAAPolyLine(3f, ToVector3(new Vector2(inner.x + 18f, surfaceY)), ToVector3(new Vector2(inner.xMax - 18f, surfaceY)));
            DrawArrow(origin, end, _accentColor, 3f);
            DrawDashedLine(hit, origin, _warningColor, 1.4f, 4f);

            Handles.color = _warningColor;

            Handles.DrawSolidDisc(ToVector3(hit), Vector3.forward, 5f);
            Handles.EndGUI();
            EndVisualization();
        }

        private void DrawTemporalHistoryVisualization(int historyLength, int radius, float spatialBlend)
        {
            var rect = BeginVisualization(166f, string.Empty);

            _temporalHistoryVisualizationRect = rect;

            var inner = Inset(rect, 12f, 10f, 12f, 10f);
            const float splitGap = 8f;
            var leftArea = new Rect(inner.x, inner.y, inner.width * .5f, inner.height);
            var rightArea = new Rect(leftArea.xMax + splitGap, inner.y + 4f, inner.xMax - (leftArea.xMax + splitGap), inner.height - 8f);
            var historyT = Mathf.InverseLerp(1f, 128f, historyLength);
            var visibleHistorySquares = Mathf.Clamp(Mathf.CeilToInt(Mathf.Lerp(1f, 8f, historyT)), 1, 8);
            const float stepX = 7f;
            var stepY = stepX * .78f;
            const int gridColumns = 9;
            const int gridRows = 7;
            const float frameAspect = 9f / 7f;
            var frameHeight = Mathf.Min(inner.height * .56f, inner.width * .24f / frameAspect);
            var frameWidth = frameHeight * frameAspect;
            var blendStrength = Mathf.Clamp01(spatialBlend);
            var stackWidth = visibleHistorySquares * stepX;
            var stackHeight = visibleHistorySquares * stepY;
            var chainWidth = frameWidth + stackWidth;
            var chainHeight = frameHeight + stackHeight;

            var leadRect = new Rect(
                leftArea.center.x - chainWidth * .5f + stackWidth - 8f,
                leftArea.center.y - chainHeight * .5f + stackHeight,
                frameWidth,
                frameHeight);

            var scatterStrength = _temporalHistoryScatterDisplayStrength;

            for (var i = visibleHistorySquares; i >= 1; i--)
            {
                var fadeT = i / (float)visibleHistorySquares;

                var historyRect = new Rect(
                    leadRect.x - i * stepX,
                    leadRect.y - i * stepY,
                    frameWidth,
                    frameHeight);

                if (scatterStrength > 0f)
                {
                    const float scatterPadding = 2f;
                    var scatterDistance = leftArea.width * .18f * scatterStrength;
                    var scatterFactor = _temporalHistoryScatterOffsets[Mathf.Clamp(i - 1, 0, _temporalHistoryScatterOffsets.Length - 1)];
                    var scatterMagnitude = Mathf.Abs(scatterFactor);
                    var availableLeft = Mathf.Max(0f, historyRect.xMin - (leftArea.xMin + scatterPadding));
                    var availableRight = Mathf.Max(0f, (leftArea.xMax - scatterPadding) - historyRect.xMax);
                    var allowedScatterDistance = scatterFactor < 0f ? availableLeft : availableRight;

                    historyRect.x += Mathf.Sign(scatterFactor) * scatterMagnitude * Mathf.Min(scatterDistance, allowedScatterDistance);
                }

                var fill = Color.Lerp(_panelSoftColor, _accentColor, .40f);

                fill.a = Mathf.Lerp(.05f, .44f, 1f - fadeT * .82f);

                var outline = Color.Lerp(_lineColor, _accentColor, .38f);

                outline.a = Mathf.Lerp(.06f, .32f, 1f - fadeT * .78f);

                EditorGUI.DrawRect(historyRect, fill);
                DrawRectOutline(historyRect, outline);
            }

            var cellWidth = Mathf.Floor(leadRect.width / gridColumns);
            var cellHeight = Mathf.Floor(leadRect.height / gridRows);
            var gridWidth = cellWidth * gridColumns;
            var gridHeight = cellHeight * gridRows;

            var leadGridRect = new Rect(
                leadRect.center.x - gridWidth * .5f,
                leadRect.center.y - gridHeight * .5f,
                gridWidth,
                gridHeight);

            var centerX = gridColumns / 2;
            var centerY = gridRows / 2;
            var frameBackground = Color.Lerp(_panelSoftColor, _accentColor, .26f + blendStrength * .14f);

            frameBackground.a = 1f;

            var frameOutline = Color.Lerp(_lineColor, Color.white, EditorGUIUtility.isProSkin ? .28f : .16f);

            frameOutline.a = .96f;

            var baseCellColor = Color.Lerp(_fieldColor, _accentColor, EditorGUIUtility.isProSkin ? .12f : .08f);

            baseCellColor = Color.Lerp(baseCellColor, _panelSoftColor, EditorGUIUtility.isProSkin ? .08f : .05f);
            baseCellColor.a = .25f;

            var radiusCellColor = Color.Lerp(_accentColor, _fieldColor, EditorGUIUtility.isProSkin ? .38f : .2f);

            radiusCellColor.a = .25f;

            var centerCellColor = Color.Lerp(radiusCellColor, _accentColor, EditorGUIUtility.isProSkin ? .22f : .28f);

            centerCellColor = Color.Lerp(centerCellColor, Color.white, .15f);
            centerCellColor.a = .25f;

            var radiusOutlineColor = Color.Lerp(_accentColor, _fieldColor, EditorGUIUtility.isProSkin ? .58f : .42f);

            radiusOutlineColor.a = 1f;

            EditorGUI.DrawRect(leadRect, frameBackground);

            var radiusRect = new Rect(
                leadGridRect.x + (centerX - radius) * cellWidth,
                leadGridRect.y + (centerY - radius) * cellHeight,
                cellWidth * (radius * 2 + 1),
                cellHeight * (radius * 2 + 1));

            var radiusBackgroundColor = Color.Lerp(_accentColor, frameBackground, EditorGUIUtility.isProSkin ? .78f : .84f);

            radiusBackgroundColor = Color.Lerp(radiusBackgroundColor, _panelSoftColor, EditorGUIUtility.isProSkin ? .08f : .04f);
            radiusBackgroundColor.a = 1f;

            EditorGUI.DrawRect(radiusRect, radiusBackgroundColor);

            for (var y = 0; y < gridRows; y++)
            {
                for (var x = 0; x < gridColumns; x++)
                {
                    var cellRect = new Rect(
                        leadGridRect.x + x * cellWidth,
                        leadGridRect.y + y * cellHeight,
                        Mathf.Max(1f, cellWidth - 1f),
                        Mathf.Max(1f, cellHeight - 1f));

                    var chebyshevDistance = Mathf.Max(Mathf.Abs(x - centerX), Mathf.Abs(y - centerY));
                    var cellColor = baseCellColor;

                    if (chebyshevDistance <= radius && chebyshevDistance > 0)
                    {
                        cellColor = radiusCellColor;
                    }

                    if (x == centerX && y == centerY)
                    {
                        cellColor = centerCellColor;
                    }

                    EditorGUI.DrawRect(cellRect, cellColor);
                }
            }

            DrawRectOutline(leadRect, frameOutline);
            DrawRectOutline(leadGridRect, new Color(_lineColor.r, _lineColor.g, _lineColor.b, .75f));
            DrawRectOutline(radiusRect, radiusOutlineColor);

            const float demoHistoryConfidence = .28f;
            var diagramRect = new Rect(rightArea.x + 2f, rightArea.y + 2f, rightArea.width - 4f, rightArea.height - 4f);
            const float gridLeftInset = 2f;
            const float resultRightInset = 14f;
            const float squareGap = 24f;

            var targetSquareSize = Mathf.Min(
                diagramRect.height * .84f,
                (diagramRect.width - gridLeftInset - resultRightInset - squareGap) * .5f) * .92f;

            var squareSize = Mathf.Max(35f, Mathf.Floor(targetSquareSize / 5f) * 5f);
            var bootstrapGridSize = squareSize;

            var bootstrapGridRect = new Rect(
                diagramRect.x + gridLeftInset,
                diagramRect.center.y - bootstrapGridSize * .5f,
                bootstrapGridSize,
                bootstrapGridSize);

            var bootstrapCellSize = squareSize / 5f;
            var bootstrapRadius = Mathf.Clamp(radius, 0, 2);
            var bootstrapCellColor = Color.Lerp(_accentColor, _panelSoftColor, EditorGUIUtility.isProSkin ? .18f : .1f);

            bootstrapCellColor.a = 1f;

            var bootstrapBackgroundColor = Color.Lerp(_panelSoftColor, _accentColor, EditorGUIUtility.isProSkin ? .34f : .2f);

            bootstrapBackgroundColor.a = 1f;

            var bootstrapOutline = Color.Lerp(_accentColor, _lineColor, .2f);

            bootstrapOutline.a = 1f;

            var currentRawColor = Color.Lerp(_fieldColor, _lineColor, EditorGUIUtility.isProSkin ? .42f : .24f);

            currentRawColor.a = 1f;

            if (bootstrapRadius > 0)
            {
                var bootstrapRadiusRect = new Rect(
                    bootstrapGridRect.x + (2 - bootstrapRadius) * bootstrapCellSize + 1f,
                    bootstrapGridRect.y + (2 - bootstrapRadius) * bootstrapCellSize + 1f,
                    (bootstrapRadius * 2 + 1) * bootstrapCellSize - 2f,
                    (bootstrapRadius * 2 + 1) * bootstrapCellSize - 2f);

                EditorGUI.DrawRect(bootstrapRadiusRect, bootstrapBackgroundColor);
                DrawRectOutline(bootstrapRadiusRect, bootstrapOutline);
            }

            for (var y = 0; y < 5; y++)
            {
                for (var x = 0; x < 5; x++)
                {
                    var dist = Mathf.Max(Mathf.Abs(x - 2), Mathf.Abs(y - 2));

                    var cellRect = new Rect(
                        bootstrapGridRect.x + x * bootstrapCellSize + 1f,
                        bootstrapGridRect.y + y * bootstrapCellSize + 1f,
                        bootstrapCellSize - 2f,
                        bootstrapCellSize - 2f);

                    var cellColor = Color.Lerp(_fieldColor, _panelSoftColor, .22f);

                    if (dist <= bootstrapRadius && dist > 0)
                    {
                        cellColor = bootstrapCellColor;
                    }

                    if (dist == 0)
                    {
                        cellColor = currentRawColor;
                    }

                    EditorGUI.DrawRect(cellRect, cellColor);
                }
            }

            DrawRectOutline(bootstrapGridRect, new Color(_lineColor.r, _lineColor.g, _lineColor.b, .74f));

            var resultRect = new Rect(
                diagramRect.xMax - squareSize - resultRightInset,
                diagramRect.center.y - squareSize * .5f,
                squareSize,
                squareSize);

            var effectiveBlend = Mathf.Clamp01(blendStrength * (1f - demoHistoryConfidence));
            var filteredCurrentColor = Color.Lerp(currentRawColor, bootstrapCellColor, effectiveBlend);

            filteredCurrentColor = Color.Lerp(filteredCurrentColor, _accentColor, effectiveBlend * .3f);
            filteredCurrentColor.a = 1f;

            var arrowStart = new Vector2(bootstrapGridRect.xMax + 10f, diagramRect.center.y);
            var arrowEnd = new Vector2(resultRect.x - 10f, diagramRect.center.y);

            Handles.BeginGUI();
            DrawArrow(arrowStart, arrowEnd, Color.Lerp(_accentColor, _lineColor, .16f), 3f);
            Handles.EndGUI();

            EditorGUI.DrawRect(resultRect, Color.Lerp(_fieldColor, _panelSoftColor, .18f));
            DrawRectOutline(resultRect, Color.Lerp(_accentColor, Color.white, EditorGUIUtility.isProSkin ? .16f : .08f));

            var resultFillRect = Inset(resultRect, 7f, 7f, 7f, 7f);

            EditorGUI.DrawRect(resultFillRect, filteredCurrentColor);
            DrawRectOutline(resultFillRect, Color.Lerp(filteredCurrentColor, Color.white, .22f));

            EndVisualization();
        }

        private void DrawAtrousVisualization(int iterations, float depthPhi, float normalPhi)
        {
            var rect = BeginVisualization(138f, string.Empty);

            _atrousVisualizationRect = rect;

            var inner = InsetVisualization(rect);
            var count = Mathf.Clamp(iterations, 1, 5);
            var columnWidth = inner.width / 3f;
            const float columnInsetX = 4f;
            const float columnInsetY = 10f;
            var contentHeight = inner.height - columnInsetY * 2f;
            var iterationsRect = new Rect(inner.x, inner.y, columnWidth, inner.height);
            var depthRect = new Rect(iterationsRect.xMax, inner.y, columnWidth, inner.height);
            var normalRect = new Rect(depthRect.xMax, inner.y, columnWidth, inner.height);

            var iterationsScene = new Rect(
                iterationsRect.x + columnInsetX,
                inner.y + columnInsetY,
                iterationsRect.width - columnInsetX * 2f,
                contentHeight);

            var depthScene = new Rect(depthRect.x + columnInsetX, inner.y + columnInsetY, depthRect.width - columnInsetX * 2f, contentHeight);
            var normalScene = new Rect(normalRect.x + columnInsetX, inner.y + columnInsetY, normalRect.width - columnInsetX * 2f, contentHeight);
            var sharedVisualHeight = contentHeight * .96f;
            var iterationsVisualSize = Mathf.Min(sharedVisualHeight, iterationsScene.width * .96f);
            var depthVisualSize = new Vector2(depthScene.width * .96f, sharedVisualHeight);
            var normalVisualSize = new Vector2(normalScene.width * .96f, sharedVisualHeight);

            var iterationsVisualRect = new Rect(
                iterationsRect.center.x - iterationsVisualSize * .5f,
                inner.center.y - iterationsVisualSize * .5f,
                iterationsVisualSize,
                iterationsVisualSize);

            var depthVisualRect = new Rect(
                depthRect.center.x - depthVisualSize.x * .5f,
                inner.center.y - depthVisualSize.y * .5f,
                depthVisualSize.x,
                depthVisualSize.y);

            var normalVisualRect = new Rect(
                normalRect.center.x - normalVisualSize.x * .5f,
                inner.center.y - normalVisualSize.y * .5f,
                normalVisualSize.x,
                normalVisualSize.y);

            var animationTime = _atrousAnimationTime;
            var hoverBlend = Mathf.SmoothStep(0f, 1f, _atrousOrbitDisplayStrength);
            var orbitVisibility = hoverBlend;
            var depthJitterStrength = hoverBlend;
            var normalSwayStrength = hoverBlend;
            const float defaultDepthPhi = 10f;
            const float minDepthPhi = 0f;
            const float maxDepthPhi = 20f;
            const float defaultNormalPhi = 32f;
            const float minNormalPhi = 0f;
            const float maxNormalPhi = 64f;
            var visualDepthPhi = Mathf.Clamp(depthPhi, minDepthPhi, maxDepthPhi);
            var visualNormalPhi = Mathf.Clamp(normalPhi, minNormalPhi, maxNormalPhi);

            Handles.BeginGUI();

            var iterationsCenter = iterationsVisualRect.center;
            var maxIterationsRadius = iterationsVisualRect.height * .5f;
            var baseRadius = maxIterationsRadius * .28f;
            var ringSpacing = (maxIterationsRadius - baseRadius) / 4f;

            for (var i = count - 1; i >= 0; i--)
            {
                var radius = Mathf.Min(baseRadius + ringSpacing * i, maxIterationsRadius);
                var alpha = Mathf.Lerp(.12f, .34f, i / 4f) * .75f;

                Handles.color = new Color(_accentColor.r, _accentColor.g, _accentColor.b, alpha);

                Handles.DrawSolidDisc(ToVector3(iterationsCenter), Vector3.forward, radius);

                Handles.color = _accentColor;

                Handles.DrawWireDisc(ToVector3(iterationsCenter), Vector3.forward, radius);
            }

            Handles.color = Color.Lerp(_accentColor, Color.white, .18f);

            Handles.DrawSolidDisc(ToVector3(iterationsCenter), Vector3.forward, 3.8f);

            if (orbitVisibility > .001f)
            {
                var orbitSpeedMultipliers = new[] { .72f, .98f, 1.28f, 1.56f, 1.94f };

                for (var ringIndex = 0; ringIndex < count; ringIndex++)
                {
                    var orbitRadius = Mathf.Min(baseRadius + ringSpacing * ringIndex, maxIterationsRadius);
                    var speed = orbitSpeedMultipliers[Mathf.Clamp(ringIndex, 0, orbitSpeedMultipliers.Length - 1)];
                    var phase = animationTime * speed + ringIndex * .93f;
                    var orbitOffset = new Vector2(Mathf.Cos(phase), Mathf.Sin(phase)) * orbitRadius;
                    var particlePosition = iterationsCenter + orbitOffset;
                    var particleColor = Color.Lerp(_accentColor, Color.white, .22f);

                    particleColor.a = Mathf.Lerp(.52f, .82f, ringIndex / 4f) * orbitVisibility;
                    Handles.color = particleColor;

                    Handles.DrawSolidDisc(ToVector3(particlePosition), Vector3.forward, 2.2f + ringIndex * .2f);
                }
            }

            var depthCenter = new Vector2(depthVisualRect.x + depthVisualRect.width * .18f, depthVisualRect.center.y);
            var depthEdgeX = depthVisualRect.x + depthVisualRect.width * .45f;

            EditorGUI.DrawRect(
                new Rect(depthVisualRect.x, depthVisualRect.y, depthEdgeX - depthVisualRect.x, depthVisualRect.height),
                new Color(_panelSoftColor.r, _panelSoftColor.g, _panelSoftColor.b, .18f));

            EditorGUI.DrawRect(
                new Rect(depthEdgeX, depthVisualRect.y, depthVisualRect.xMax - depthEdgeX, depthVisualRect.height),
                new Color(_fieldColor.r, _fieldColor.g, _fieldColor.b, .12f));

            Handles.color = new Color(_lineColor.r, _lineColor.g, _lineColor.b, .8f);

            Handles.DrawAAPolyLine(
                2f,
                ToVector3(new Vector2(depthEdgeX, depthVisualRect.y)),
                ToVector3(new Vector2(depthEdgeX, depthVisualRect.yMax)));

            var depthReferenceWeights = new[] { .85f, .7f, .5f, .3f, .15f };

            var depthSamplePositions = new[]
            {
            new Vector2(depthVisualRect.x + depthVisualRect.width * .34f, depthVisualRect.center.y - depthVisualRect.height * .33f),
            new Vector2(depthVisualRect.x + depthVisualRect.width * .34f, depthVisualRect.center.y + depthVisualRect.height * .33f),
            new Vector2(depthVisualRect.x + depthVisualRect.width * .60f, depthVisualRect.center.y - depthVisualRect.height * .40f),
            new Vector2(depthVisualRect.x + depthVisualRect.width * .74f, depthVisualRect.center.y),
            new Vector2(depthVisualRect.x + depthVisualRect.width * .86f, depthVisualRect.center.y + depthVisualRect.height * .40f)
        };

            var depthSampleDeltas = new float[depthReferenceWeights.Length];

            for (var i = 0; i < depthReferenceWeights.Length; i++)
            {
                depthSampleDeltas[i] = -Mathf.Log(Mathf.Clamp(depthReferenceWeights[i], .001f, .999f)) / defaultDepthPhi;
            }

            for (var i = 0; i < depthSamplePositions.Length; i++)
            {
                var jitterRadius = (2.4f + i * .35f) * depthJitterStrength;

                var jitter = new Vector2(
                    Mathf.Cos(animationTime * 1.65f + i * 1.17f),
                    Mathf.Sin(animationTime * 1.25f + i * .94f)) * jitterRadius;

                var samplePosition = depthSamplePositions[i] + jitter;
                var weight = Mathf.Exp(-depthSampleDeltas[i] * visualDepthPhi);
                var alpha = Mathf.Lerp(.06f, .82f, Mathf.Clamp01(weight));
                var color = new Color(_accentColor.r, _accentColor.g, _accentColor.b, alpha);

                Handles.color = color;

                Handles.DrawAAPolyLine(2f, ToVector3(depthCenter), ToVector3(samplePosition));
                Handles.DrawSolidDisc(ToVector3(samplePosition), Vector3.forward, Mathf.Lerp(1.8f, 4.2f, Mathf.Clamp01(weight)));
            }

            Handles.color = new Color(_warningColor.r, _warningColor.g, _warningColor.b, .24f);

            Handles.DrawAAPolyLine(
                1.5f,
                ToVector3(new Vector2(depthEdgeX + 6f, depthVisualRect.y + 4f)),
                ToVector3(new Vector2(depthEdgeX + 6f, depthVisualRect.yMax - 4f)));

            Handles.color = Color.Lerp(_accentColor, Color.white, .18f);

            Handles.DrawSolidDisc(ToVector3(depthCenter), Vector3.forward, 4f);

            var normalOrigin = normalVisualRect.center;
            var normalBaseLength = normalVisualRect.height * .286f;

            DrawArrow(normalOrigin, normalOrigin + Vector2.up * -normalBaseLength, Color.Lerp(_accentColor, Color.white, .08f), 2.5f);

            var normalReferenceWeights = new[] { .04f, .12f, .35f, 1.0f, .35f, .12f, .04f };
            var normalAlphaAgreements = new float[normalReferenceWeights.Length];

            for (var i = 0; i < normalReferenceWeights.Length; i++)
            {
                normalAlphaAgreements[i] = Mathf.Pow(Mathf.Clamp(normalReferenceWeights[i], .001f, 1f), 1f / defaultNormalPhi);
            }

            var normalGeometryAgreements = new[] { .58f, .72f, .86f, 1.0f, .86f, .72f, .58f };
            var normalSigns = new[] { -1f, -1f, -1f, 0f, 1f, 1f, 1f };

            var normalDistances = new[]
            {
            normalVisualRect.height * .429f,
            normalVisualRect.height * .403f,
            normalVisualRect.height * .377f,
            normalVisualRect.height * .351f,
            normalVisualRect.height * .377f,
            normalVisualRect.height * .403f,
            normalVisualRect.height * .429f
        };

            for (var i = 0; i < normalGeometryAgreements.Length; i++)
            {
                var geometryAgreement = normalGeometryAgreements[i];
                var alphaAgreement = normalAlphaAgreements[i];
                var baseAngle = Mathf.Acos(Mathf.Clamp(geometryAgreement, -1f, 1f)) * Mathf.Rad2Deg * normalSigns[i];

                var swayAngle = Mathf.Sin(animationTime * 1.45f + i * .82f) *
                                Mathf.Lerp(1.4f, 6.2f, 1f - geometryAgreement) *
                                normalSwayStrength;

                var rotated = Quaternion.Euler(0f, 0f, baseAngle + swayAngle) * Vector3.up;
                var direction = new Vector2(rotated.x, rotated.y).normalized;
                var sampleOrigin = normalOrigin + direction * normalDistances[i];
                var weight = Mathf.Pow(alphaAgreement, visualNormalPhi);
                var alpha = Mathf.Lerp(.04f, .88f, Mathf.Clamp01(weight));
                var color = new Color(_accentColor.r, _accentColor.g, _accentColor.b, alpha);

                Handles.color = color;

                Handles.DrawAAPolyLine(1.6f, ToVector3(normalOrigin), ToVector3(sampleOrigin));
                DrawArrow(sampleOrigin, sampleOrigin + direction * (normalBaseLength * .72f), color, 2.1f);
                Handles.DrawSolidDisc(ToVector3(sampleOrigin), Vector3.forward, Mathf.Lerp(1.6f, 3.6f, Mathf.Clamp01(weight)));
            }

            Handles.color = Color.Lerp(_accentColor, Color.white, .18f);

            Handles.DrawSolidDisc(ToVector3(normalOrigin), Vector3.forward, 3.8f);

            Handles.EndGUI();
            EndVisualization();
        }

        internal void DrawEnvironmentVisualization(Color top, Color bottom, float intensity, float exponent)
        {
            var rect = BeginVisualization(168f, "Visualization: ray color when the scene is missed");
            var inner = InsetVisualization(rect);
            var sky = new Rect(inner.x + 10f, inner.y + 8f, inner.width - 20f, inner.height - 36f);

            for (var y = 0; y < Mathf.CeilToInt(sky.height); y++)
            {
                var t = 1f - y / Mathf.Max(1f, sky.height - 1f);
                var curved = Mathf.Pow(Mathf.Clamp01(t), Mathf.Max(.1f, exponent));
                var color = Color.Lerp(bottom, top, curved);

                color.r *= intensity;
                color.g *= intensity;
                color.b *= intensity;

                color.a = 1f;

                EditorGUI.DrawRect(new Rect(sky.x, sky.y + y, sky.width, 1f), color);
            }

            Handles.BeginGUI();

            var origin = new Vector2(sky.x + 28f, sky.yMax - 24f);

            DrawArrow(origin, origin + new Vector2(sky.width * .62f, -sky.height * .62f), Color.white, 3f);
            Handles.EndGUI();

            GUI.Label(new Rect(sky.x + 8f, sky.y + 8f, sky.width, 18f), "top", _miniLabelStyle);
            GUI.Label(new Rect(sky.x + 8f, sky.yMax - 24f, sky.width, 18f), "bottom", _miniLabelStyle);

            GUI.Label(
                new Rect(inner.x, inner.yMax - 18f, inner.width, 18f),
                "Intensity: " + FormatFloat(intensity) + "    Exponent: " + FormatFloat(exponent),
                _centerMiniLabelStyle);

            EndVisualization();
        }

        internal void DrawSceneUpdateVisualization(int rendererFrames, int lightFrames, bool skinnedOffscreen)
        {
            var rect = BeginVisualization(132f, "Visualization: scene state synchronization frequency");
            var inner = InsetVisualization(rect);
            var timeline = new Rect(inner.x + 12f, inner.center.y - 10f, inner.width - 24f, 20f);

            DrawRectOutline(timeline, _lineColor);

            DrawTimelineTicks(timeline, rendererFrames, _accentColor, -18f, "renderers");
            DrawTimelineTicks(timeline, lightFrames, _warningColor, 18f, "lights");

            GUI.Label(
                new Rect(inner.x, inner.yMax - 18f, inner.width, 18f),
                skinnedOffscreen ? "Skinned mesh updates even offscreen." : "Skinned mesh may stop updating offscreen.",
                _centerMiniLabelStyle);

            EndVisualization();
        }

        private Rect BeginVisualization(float height, string title)
        {
            GUILayout.Space(6f);

            var rect = GUILayoutUtility.GetRect(1f, height, GUILayout.ExpandWidth(true));

            GUI.DrawTexture(rect, _visualTexture, ScaleMode.StretchToFill);

            return rect;
        }

        private Rect InsetVisualization(Rect rect)
        {
            return Inset(rect, 14f, 12f, 14f, 14f);
        }

        private void EndVisualization()
        {
            GUILayout.Space(2f);
        }

        private void DrawGrid(Rect rect)
        {
            Handles.BeginGUI();

            Handles.color = new Color(_lineColor.r, _lineColor.g, _lineColor.b, .35f);

            for (var i = 0; i <= 6; i++)
            {
                var t = i / 6f;
                var x = Mathf.Lerp(rect.x, rect.xMax, t);

                Handles.DrawAAPolyLine(
                    1f,
                    ToVector3(new Vector2(x, rect.center.y + 28f)),
                    ToVector3(new Vector2(rect.center.x + (x - rect.center.x) * 1.6f, rect.yMax - 6f)));
            }

            for (var i = 0; i <= 5; i++)
            {
                var y = Mathf.Lerp(rect.center.y + 30f, rect.yMax - 8f, i / 5f);

                Handles.DrawAAPolyLine(1f, ToVector3(new Vector2(rect.x, y)), ToVector3(new Vector2(rect.xMax, y)));
            }

            Handles.EndGUI();
        }

        private void DrawCamera(Vector2 center)
        {
            var icon = GetCameraIconTexture();
            var rect = GetCameraIconRect(center);

            if (icon != null)
            {
                GUI.DrawTexture(rect, icon, ScaleMode.ScaleToFit, true);

                return;
            }

            var fallbackRect = new Rect(center.x - 18f, center.y - 12f, 24f, 18f);

            EditorGUI.DrawRect(fallbackRect, new Color(.13f, .16f, .2f, 1f));
            DrawRectOutline(fallbackRect, _lineColor);
        }

        private static Rect GetCameraIconRect(Vector2 center)
        {
            return new Rect(center.x - 22f, center.y - 18f, 32f, 32f);
        }

        private static Vector2 GetCameraRayOrigin(Vector2 center)
        {
            var rect = GetCameraIconRect(center);

            return new Vector2(rect.xMax - 2f, rect.center.y + 1f);
        }

        private static Texture GetCameraIconTexture()
        {
            var proIcon = EditorGUIUtility.IconContent("d_Camera Icon");

            if (proIcon != null && proIcon.image != null)
            {
                return proIcon.image;
            }

            var icon = EditorGUIUtility.IconContent("Camera Icon");

            if (icon != null && icon.image != null)
            {
                return icon.image;
            }

            var typeIcon = EditorGUIUtility.ObjectContent(null, typeof(Camera));

            if (typeIcon != null && typeIcon.image != null)
            {
                return typeIcon.image;
            }

            return null;
        }

        private void DrawSphere(Vector2 center, float radius)
        {
            EnsureSpherePreviewMaterial();

            if (_spherePreviewMaterial == null)
            {
                Handles.BeginGUI();

                Handles.color = Color.Lerp(_fieldColor, _panelSoftColor, .45f);

                Handles.DrawSolidDisc(ToVector3(center), Vector3.forward, radius);
                Handles.EndGUI();

                return;
            }

            var shadowColor = Color.Lerp(_fieldColor, new Color(.03f, .04f, .05f, 1f), .45f);
            var midColor = Color.Lerp(_panelSoftColor, _fieldColor, .48f);
            var lightColor = Color.Lerp(_textColor, _accentColor, .14f);
            var rimColor = Color.Lerp(_textColor, _accentColor, .2f);
            var rect = new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f);

            _spherePreviewMaterial.SetColor(_sphereShadowColorId, shadowColor);
            _spherePreviewMaterial.SetColor(_sphereMidColorId, midColor);
            _spherePreviewMaterial.SetColor(_sphereLightColorId, lightColor);
            _spherePreviewMaterial.SetColor(_sphereRimColorId, rimColor);
            _spherePreviewMaterial.SetVector(_sphereLightDirectionId, new Vector4(-.52f, .64f, .57f, 0f));
            _spherePreviewMaterial.SetFloat(_sphereAmbientId, .16f);
            _spherePreviewMaterial.SetFloat(_sphereSpecularStrengthId, .12f);
            _spherePreviewMaterial.SetFloat(_sphereGlossId, 42f);
            _spherePreviewMaterial.SetFloat(_sphereFlipYId, 0f);
            _spherePreviewMaterial.SetInt(_sphereEditorLightCountId, 0);

            EditorGUI.DrawPreviewTexture(rect, Texture2D.whiteTexture, _spherePreviewMaterial, ScaleMode.StretchToFill);
        }

        private void DrawLegendLine(Vector2 start, Color color, string text)
        {
            Handles.BeginGUI();

            Handles.color = color;

            Handles.DrawAAPolyLine(3f, ToVector3(start), ToVector3(start + new Vector2(24f, 0f)));
            Handles.EndGUI();
            GUI.Label(new Rect(start.x + 34f, start.y - 8f, 320f, 18f), text, _legendLabelStyle);
        }

        private void DrawTreeNode(Vector2 center, string label, Color color)
        {
            var rect = new Rect(center.x - 28f, center.y - 13f, 56f, 26f);

            EditorGUI.DrawRect(rect, new Color(color.r, color.g, color.b, .28f));
            DrawRectOutline(rect, color);
            GUI.Label(rect, label, _centerMiniLabelStyle);
        }

        private void DrawTimelineTicks(Rect timeline, int period, Color color, float labelOffset, string label)
        {
            var normalizedPeriod = Mathf.Clamp01(Mathf.InverseLerp(1f, 120f, period));
            var ticks = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(10f, 3f, normalizedPeriod)), 3, 10);

            Handles.BeginGUI();

            Handles.color = color;

            for (var i = 0; i < ticks; i++)
            {
                var t = i / Mathf.Max(1f, ticks - 1f);
                var x = Mathf.Lerp(timeline.x + 4f, timeline.xMax - 4f, t);

                Handles.DrawAAPolyLine(2f, ToVector3(new Vector2(x, timeline.y)), ToVector3(new Vector2(x, timeline.yMax)));
            }

            Handles.EndGUI();

            GUI.Label(
                new Rect(timeline.x, timeline.center.y + labelOffset, timeline.width, 18f),
                label + ": every " + period + " frames",
                _centerMiniLabelStyle);
        }

        private void DrawWireBox(Rect rect, Color color, float width)
        {
            DrawLine(new Vector2(rect.x, rect.y), new Vector2(rect.xMax, rect.y), color, width);
            DrawLine(new Vector2(rect.xMax, rect.y), new Vector2(rect.xMax, rect.yMax), color, width);
            DrawLine(new Vector2(rect.xMax, rect.yMax), new Vector2(rect.x, rect.yMax), color, width);
            DrawLine(new Vector2(rect.x, rect.yMax), new Vector2(rect.x, rect.y), color, width);
        }

        private void DrawRectOutline(Rect rect, Color color)
        {
            Handles.BeginGUI();
            DrawLine(new Vector2(rect.x, rect.y), new Vector2(rect.xMax, rect.y), color, 1f);
            DrawLine(new Vector2(rect.xMax, rect.y), new Vector2(rect.xMax, rect.yMax), color, 1f);
            DrawLine(new Vector2(rect.xMax, rect.yMax), new Vector2(rect.x, rect.yMax), color, 1f);
            DrawLine(new Vector2(rect.x, rect.yMax), new Vector2(rect.x, rect.y), color, 1f);
            Handles.EndGUI();
        }

        private static void DrawLine(Vector2 a, Vector2 b, Color color, float width)
        {
            Handles.color = color;

            Handles.DrawAAPolyLine(width, ToVector3(a), ToVector3(b));
        }

        private void DrawRayLabel(Vector2 from, Vector2 to, float positionT, float normalOffset, string text)
        {
            var delta = to - from;

            if (delta.sqrMagnitude < .001f)
            {
                return;
            }

            var direction = delta.normalized;
            var normal = new Vector2(-direction.y, direction.x);

            if (normal.y > 0f)
            {
                normal = -normal;
            }

            var pivot = Vector2.Lerp(from, to, Mathf.Clamp01(positionT)) + normal * normalOffset;
            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            if (angle > 90f || angle < -90f)
            {
                angle += 180f;
            }

            var content = new GUIContent(text);
            var size = _rayLabelStyle.CalcSize(content);
            var rect = new Rect(pivot.x - size.x * .5f - 4f, pivot.y - size.y * .5f - 1f, size.x + 8f, size.y + 2f);
            var previousMatrix = GUI.matrix;

            GUIUtility.RotateAroundPivot(angle, pivot);
            GUI.Label(rect, content, _rayLabelStyle);

            GUI.matrix = previousMatrix;
        }

        private static void DrawArrow(Vector2 from, Vector2 to, Color color, float width)
        {
            var delta = to - from;

            if (delta.sqrMagnitude < .001f)
            {
                return;
            }

            var direction = delta.normalized;
            var normal = new Vector2(-direction.y, direction.x);
            var headLength = Mathf.Min(6f, delta.magnitude * .45f);
            var headWidth = Mathf.Min(3f, headLength * .55f);
            var shaftEnd = to - direction * Mathf.Max(.001f, headLength * .78f);
            var left = to - direction * headLength + normal * headWidth;
            var right = to - direction * headLength - normal * headWidth;

            Handles.color = color;

            Handles.DrawAAPolyLine(width, ToVector3(from), ToVector3(shaftEnd));
            Handles.DrawAAConvexPolygon(ToVector3(to), ToVector3(left), ToVector3(right));
        }

        private static void DrawDashedLine(Vector2 from, Vector2 to, Color color, float width, float dashLength)
        {
            var delta = to - from;
            var length = delta.magnitude;

            if (length <= .001f)
            {
                return;
            }

            var direction = delta / length;
            var cursor = 0f;

            Handles.color = color;

            while (cursor < length)
            {
                var a = from + direction * cursor;
                var b = from + direction * Mathf.Min(length, cursor + dashLength);

                Handles.DrawAAPolyLine(width, ToVector3(a), ToVector3(b));

                cursor += dashLength * 1.8f;
            }
        }

        private Rect Inset(Rect rect, float left, float top, float right, float bottom)
        {
            return new Rect(rect.x + left, rect.y + top, Mathf.Max(1f, rect.width - left - right), Mathf.Max(1f, rect.height - top - bottom));
        }

        internal float GetGameCameraFarDistance(float fallback)
        {
            var camera = Camera.main;

            if (camera != null && camera.enabled && camera.cameraType == CameraType.Game)
            {
                return camera.farClipPlane;
            }

            var cameras = FindObjectsByType<Camera>();

            for (var i = 0; i < cameras.Length; i++)
            {
                camera = cameras[i];

                if (camera != null && camera.enabled && camera.cameraType == CameraType.Game)
                {
                    return camera.farClipPlane;
                }
            }

            return fallback;
        }

        private void EnsureSpherePreviewMaterial()
        {
            if (_spherePreviewMaterial != null)
            {
                return;
            }

            if (_spherePreviewShader == null)
            {
                _spherePreviewShader = Shader.Find(SpherePreviewShaderName);
            }

            if (_spherePreviewShader == null)
            {
                return;
            }

            _spherePreviewMaterial = new Material(_spherePreviewShader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        private void EnsureConePreviewMaterial()
        {
            if (_conePreviewMaterial != null)
            {
                return;
            }

            if (_conePreviewShader == null)
            {
                _conePreviewShader = Shader.Find(ConePreviewShaderName);
            }

            if (_conePreviewShader == null)
            {
                return;
            }

            _conePreviewMaterial = new Material(_conePreviewShader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        private void EnsureAssetState(LucentPathRendererSettings asset)
        {
            if (asset == null)
            {
                return;
            }

            var changed = false;

            if (asset.DlssSettings == null)
            {
                asset.DlssSettings = new DlssSettings();
                changed = true;
            }

            if (asset.PathTracingSettings == null)
            {
                asset.PathTracingSettings = new PathTracingSettings();
                changed = true;
            }

            if (asset.TemporalHistorySettings == null)
            {
                asset.TemporalHistorySettings = new TemporalHistorySettings();
                changed = true;
            }

            if (asset.TemporalHistoryConfidenceSettings == null)
            {
                asset.TemporalHistoryConfidenceSettings = new TemporalHistoryConfidenceSettings();
                changed = true;
            }

            if (asset.TemporalHistoryRejectionSettings == null)
            {
                asset.TemporalHistoryRejectionSettings = new TemporalHistoryRejectionSettings();
                changed = true;
            }

            if (asset.FireflySuppressionSettings == null)
            {
                asset.FireflySuppressionSettings = new FireflySuppressionSettings();
                changed = true;
            }

            if (changed)
            {
                asset.ValidateForEditor();
                EditorUtility.SetDirty(asset);
            }
        }

        private string FormatFloat(float value)
        {
            if (Mathf.Abs(value) >= 10f)
            {
                return value.ToString("0.#");
            }

            if (Mathf.Abs(value) >= 1f)
            {
                return value.ToString("0.##");
            }

            return value.ToString("0.###");
        }

        private void EnsureStyles()
        {
            var pro = EditorGUIUtility.isProSkin;

            if (_visualTitleStyle != null && _isStylesBuiltForProSkin == pro)
            {
                return;
            }

            ResetStyles();

            _isStylesBuiltForProSkin = pro;

            _chrome ??= new EditorChrome();

            _chrome.EnsureStyles();

            _panelSoftColor = _chrome.Palette.PanelSoftColor;
            _fieldColor = _chrome.Palette.CardColor;
            _lineColor = _chrome.Palette.LineColor;
            _textColor = _chrome.Palette.TextColor;
            _mutedTextColor = _chrome.Palette.MutedTextColor;
            _accentColor = _chrome.Palette.AccentColor;
            _accentSoftColor = pro ? new Color(122f / 255f, 199f / 255f, 245f / 255f, .24f) : new Color(.24f, .57f, 1f, .35f);
            _warningColor = pro ? new Color32(229, 185, 109, 255) : new Color(1f, .67f, .22f, 1f);
            _goodColor = pro ? new Color32(143, 210, 176, 255) : new Color(.26f, .82f, .52f, 1f);

            _visualTexture = MakeTexture(pro ? new Color32(20, 24, 31, 255) : new Color(.88f, .9f, .93f, 1f));

            _visualTitleStyle = new GUIStyle(EditorChrome.GetEditorStyle(() => EditorStyles.miniBoldLabel));
            _visualTitleStyle.fontSize = 10;
            _visualTitleStyle.alignment = TextAnchor.MiddleLeft;
            _visualTitleStyle.normal.textColor = _mutedTextColor;

            _miniLabelStyle = new GUIStyle(EditorChrome.GetEditorStyle(() => EditorStyles.miniLabel));
            _miniLabelStyle.fontSize = 10;
            _miniLabelStyle.alignment = TextAnchor.MiddleLeft;
            _miniLabelStyle.normal.textColor = _textColor;

            _centerMiniLabelStyle = new GUIStyle(_miniLabelStyle);
            _centerMiniLabelStyle.alignment = TextAnchor.MiddleCenter;

            _rayLabelStyle = new GUIStyle(_centerMiniLabelStyle);
            _rayLabelStyle.fontSize = Mathf.Max(6, _centerMiniLabelStyle.fontSize - 1);

            _legendLabelStyle = new GUIStyle(_miniLabelStyle);
            _legendLabelStyle.fontSize = _miniLabelStyle.fontSize + 1;
        }

        private void ResetStyles()
        {
            _isStylesBuiltForProSkin = false;
            _visualTitleStyle = null;
            _miniLabelStyle = null;
            _centerMiniLabelStyle = null;
            _rayLabelStyle = null;
            _legendLabelStyle = null;
            _spherePreviewShader = null;

            DestroyTexture(ref _visualTexture);
            DestroyMaterial(ref _conePreviewMaterial);
            DestroyMaterial(ref _spherePreviewMaterial);
        }

        private Texture2D MakeTexture(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            texture.SetPixel(0, 0, color);
            texture.Apply();

            return texture;
        }

        private static void DestroyTexture(ref Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            DestroyImmediate(texture);

            texture = null;
        }

        private static void DestroyMaterial(ref Material material)
        {
            if (material == null)
            {
                return;
            }

            DestroyImmediate(material);

            material = null;
        }

        private static Vector3 ToVector3(Vector2 value)
        {
            return new Vector3(value.x, value.y, 0f);
        }
    }
}
