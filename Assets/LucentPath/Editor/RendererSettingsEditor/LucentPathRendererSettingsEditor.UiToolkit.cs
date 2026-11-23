using LucentPath.EditorUi.Core;
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LucentPath.EditorUi.RendererSettings
{
    public partial class LucentPathRendererSettingsEditor
    {
        internal VisualElement AddToggleField(VisualElement parent, string cardTitle, string label, Func<bool> getValue, Action<bool> assign, string tooltip = null)
        {
            return _uiToolkitFields.AddToggleField(parent, cardTitle, label, getValue, assign, tooltip);
        }

        internal VisualElement AddMinIntField(VisualElement parent, string cardTitle, string label, Func<int> getValue, Action<int> assign, int minValue, string tooltip = null)
        {
            return _uiToolkitFields.AddMinIntField(parent, cardTitle, label, getValue, assign, minValue, tooltip);
        }

        internal VisualElement AddIntField(VisualElement parent, string cardTitle, string label, Func<int> getValue, Action<int> assign, Func<int, int> normalize = null, string tooltip = null)
        {
            return _uiToolkitFields.AddIntField(parent, cardTitle, label, getValue, assign, normalize, tooltip);
        }

        internal VisualElement AddIntSliderField(VisualElement parent, string cardTitle, string label, Func<int> getValue, Action<int> assign, int minValue, int maxValue, string tooltip = null)
        {
            return _uiToolkitFields.AddIntSliderField(parent, cardTitle, label, getValue, assign, minValue, maxValue, tooltip);
        }

        internal VisualElement AddIntSliderField(VisualElement parent, string cardTitle, string label, Func<int> getValue, Action<int> assign, Func<int> getMinValue, Func<int> getMaxValue, string tooltip = null)
        {
            return _uiToolkitFields.AddIntSliderField(parent, cardTitle, label, getValue, assign, getMinValue, getMaxValue, tooltip);
        }

        internal VisualElement AddFloatField(VisualElement parent, string cardTitle, string label, Func<float> getValue, Action<float> assign, string tooltip = null)
        {
            return _uiToolkitFields.AddFloatField(parent, cardTitle, label, getValue, assign, tooltip);
        }

        internal VisualElement AddMinFloatField(VisualElement parent, string cardTitle, string label, Func<float> getValue, Action<float> assign, float minValue, string tooltip = null)
        {
            return _uiToolkitFields.AddMinFloatField(parent, cardTitle, label, getValue, assign, minValue, tooltip);
        }

        internal VisualElement AddLiveMinFloatField(VisualElement parent, string cardTitle, string label, Func<float> getValue, Action<float> assign, float minValue, string tooltip = null)
        {
            return _uiToolkitFields.AddLiveMinFloatField(parent, cardTitle, label, getValue, assign, minValue, tooltip);
        }

        internal VisualElement AddClampedFloatField(VisualElement parent, string cardTitle, string label, Func<float> getValue, Action<float> assign, float minValue, Func<float> getMaxValue, string tooltip = null)
        {
            return _uiToolkitFields.AddClampedFloatField(parent, cardTitle, label, getValue, assign, minValue, getMaxValue, tooltip);
        }

        internal VisualElement AddFloatSliderField(VisualElement parent, string cardTitle, string label, Func<float> getValue, Action<float> assign, float minValue, float maxValue, string tooltip = null)
        {
            return _uiToolkitFields.AddFloatSliderField(parent, cardTitle, label, getValue, assign, minValue, maxValue, tooltip);
        }

        internal VisualElement AddColorField(VisualElement parent, string cardTitle, string label, Func<Color> getValue, Action<Color> assign, string tooltip = null)
        {
            return _uiToolkitFields.AddColorField(parent, cardTitle, label, getValue, assign, tooltip);
        }

        internal VisualElement AddObjectField<T>(VisualElement parent, string cardTitle, string label, Func<T> getValue, Action<T> assign, string tooltip = null)
            where T : UnityEngine.Object
        {
            return _uiToolkitFields.AddObjectField(parent, cardTitle, label, getValue, assign, tooltip);
        }

        internal VisualElement AddTextField(VisualElement parent, string cardTitle, string label, Func<string> getValue, Action<string> assign, string tooltip = null)
        {
            return _uiToolkitFields.AddTextField(parent, cardTitle, label, getValue, assign, tooltip);
        }

        internal VisualElement AddEnumField<TEnum>(VisualElement parent, string cardTitle, string label, Func<TEnum> getValue, Action<TEnum> assign, string tooltip = null)
            where TEnum : Enum
        {
            return _uiToolkitFields.AddEnumField(parent, cardTitle, label, getValue, assign, tooltip);
        }

        internal void RegisterConditionalElement(VisualElement element, Func<bool> isVisible)
        {
            _uiToolkitView.RegisterConditionalElement(element, isVisible);
        }

        internal void RegisterUiToolkitRefresh(Action refresh)
        {
            _uiToolkitView.RegisterRefresh(refresh);
        }

        internal void ClampBounceDistanceToCameraLimit()
        {
            var pathTracingSettings = Asset.PathTracingSettings;

            if (pathTracingSettings == null)
            {
                return;
            }

            var primaryRayDistanceLimit = GetGameCameraFarDistance(float.PositiveInfinity);

            if (float.IsPositiveInfinity(primaryRayDistanceLimit) || pathTracingSettings.MaxBounceDistance <= primaryRayDistanceLimit)
            {
                return;
            }

            Undo.RecordObject(Asset, "Clamp Max Bounce Distance");

            pathTracingSettings.MaxBounceDistance = primaryRayDistanceLimit;

            Asset.ValidateForEditor();
            EditorUtility.SetDirty(Asset);
        }

        internal void AddVisualizationElement(VisualElement parent, Action draw)
        {
            parent?.Add(CreateVisualizationImGuiContainer(draw));
        }

        internal void AddBounceDistanceVisualizationElement(VisualElement parent)
        {
            var container = CreateVisualizationImGuiContainer(
                () =>
            {
                var pathTracingSettings = Asset.PathTracingSettings;

                if (pathTracingSettings == null)
                {
                    return;
                }

                var primaryRayDistanceLimit = GetGameCameraFarDistance(float.PositiveInfinity);

                DrawBounceDistanceVisualization(
                    float.IsPositiveInfinity(primaryRayDistanceLimit) ?
                    pathTracingSettings.MaxBounceDistance :
                    Mathf.Min(pathTracingSettings.MaxBounceDistance, primaryRayDistanceLimit));
            });

            container.RegisterCallback<PointerEnterEvent>(evt => UpdateBounceDistancePointer(
                IsBounceDistancePointerOverVisualization(evt.localPosition),
                evt.localPosition));

            container.RegisterCallback<PointerMoveEvent>(evt => UpdateBounceDistancePointer(
                IsBounceDistancePointerOverVisualization(evt.localPosition),
                evt.localPosition));

            container.RegisterCallback<PointerDownEvent>(
                evt =>
            {
                if (IsBounceDistancePointerOverVisualization(evt.localPosition) == false)
                {
                    return;
                }

                _isBounceDistanceCardDragActive = true;
                _bounceDistanceCardDragPointerId = evt.pointerId;

                container.CapturePointer(evt.pointerId);
                UpdateBounceDistancePointer(true, evt.localPosition);
            });

            container.RegisterCallback<PointerUpEvent>(
                evt =>
            {
                if (_isBounceDistanceCardDragActive && _bounceDistanceCardDragPointerId == evt.pointerId)
                {
                    if (container.HasPointerCapture(evt.pointerId))
                    {
                        container.ReleasePointer(evt.pointerId);
                    }

                    _isBounceDistanceCardDragActive = false;
                    _bounceDistanceCardDragPointerId = -1;
                }

                UpdateBounceDistancePointer(IsBounceDistancePointerOverVisualization(evt.localPosition), evt.localPosition);
            });

            container.RegisterCallback<PointerCaptureOutEvent>(
                _ =>
            {
                _isBounceDistanceCardDragActive = false;
                _bounceDistanceCardDragPointerId = -1;
            });

            container.RegisterCallback<PointerLeaveEvent>(evt => UpdateBounceDistancePointer(false, evt.localPosition));

            container.RegisterCallback<DetachFromPanelEvent>(
                _ =>
            {
                if (ReferenceEquals(_bounceDistanceCardContainer, container))
                {
                    TeardownBounceDistanceCardTicker();
                }
            });

            var intervalMs = Math.Max(1, Mathf.RoundToInt(1000f / BounceDistanceVisualizationFps));

            _bounceDistanceCardContainer = container;
            _bounceDistanceCardTicker = container.schedule.Execute(TickBounceDistanceCard).Every(intervalMs);

            _bounceDistanceCardTicker.Pause();
            parent?.Add(container);
        }

        internal void AddSamplesPerPixelVisualizationElement(VisualElement parent)
        {
            var container = CreateVisualizationImGuiContainer(() => DrawSamplesVisualization(Asset.PathTracingSettings.SamplesPerPixel));

            container.RegisterCallback<PointerEnterEvent>(evt => SetSamplesPerPixelCardHovered(IsSamplesPerPixelPointerOverVisualization(evt.localPosition)));
            container.RegisterCallback<PointerMoveEvent>(evt => SetSamplesPerPixelCardHovered(IsSamplesPerPixelPointerOverVisualization(evt.localPosition)));
            container.RegisterCallback<PointerLeaveEvent>(_ => SetSamplesPerPixelCardHovered(false));

            container.RegisterCallback<DetachFromPanelEvent>(
                _ =>
            {
                if (ReferenceEquals(_samplesPerPixelCardContainer, container))
                {
                    TeardownSamplesPerPixelCardTicker();
                }
            });

            var intervalMs = Math.Max(1, Mathf.RoundToInt(1000f / Mathf.Max(.01f, SamplesPerPixelVisualizationFps)));

            _samplesPerPixelCardContainer = container;
            _samplesPerPixelCardTicker = container.schedule.Execute(TickSamplesPerPixelCard).Every(intervalMs);

            _samplesPerPixelCardTicker.Pause();
            parent?.Add(container);
        }

        internal void AddBounceCountVisualizationElement(VisualElement parent)
        {
            var container = CreateVisualizationImGuiContainer(() => DrawBounceCountVisualization(
                Mathf.Max(
                    Mathf.Max(Asset.PathTracingSettings.SurfaceBounces, Asset.PathTracingSettings.ReflectionBounces),
                    Asset.PathTracingSettings.TransmissionBounces),
                Asset.PathTracingSettings.RussianRouletteStart));

            container.RegisterCallback<PointerEnterEvent>(evt => SetBounceCountCardHovered(IsBounceCountPointerOverVisualization(evt.localPosition)));
            container.RegisterCallback<PointerMoveEvent>(evt => SetBounceCountCardHovered(IsBounceCountPointerOverVisualization(evt.localPosition)));
            container.RegisterCallback<PointerLeaveEvent>(_ => SetBounceCountCardHovered(false));

            container.RegisterCallback<DetachFromPanelEvent>(
                _ =>
            {
                if (ReferenceEquals(_bounceCountCardContainer, container))
                {
                    TeardownBounceCountCardTicker();
                }
            });

            var intervalMs = Math.Max(1, Mathf.RoundToInt(1000f / Mathf.Max(.01f, BounceCountVisualizationFps)));

            _bounceCountCardContainer = container;
            _bounceCountCardTicker = container.schedule.Execute(TickBounceCountCard).Every(intervalMs);

            _bounceCountCardTicker.Pause();
            parent?.Add(container);
        }

        internal void AddLightingVisualizationElement(VisualElement parent)
        {
            var container = CreateVisualizationImGuiContainer(() => DrawLightVisualization(
                Asset.PathTracingSettings.MaxLights,
                Asset.PathTracingSettings.DirectIntensity));

            container.RegisterCallback<PointerEnterEvent>(evt => UpdateLightingPointer(
                IsLightingPointerOverVisualization(evt.localPosition),
                evt.localPosition));

            container.RegisterCallback<PointerMoveEvent>(evt => UpdateLightingPointer(
                IsLightingPointerOverVisualization(evt.localPosition),
                evt.localPosition));

            container.RegisterCallback<PointerLeaveEvent>(evt => UpdateLightingPointer(false, evt.localPosition));

            container.RegisterCallback<DetachFromPanelEvent>(
                _ =>
            {
                if (ReferenceEquals(_lightingCardContainer, container))
                {
                    TeardownLightingCardTicker();
                }
            });

            var intervalMs = Math.Max(1, Mathf.RoundToInt(1000f / Mathf.Max(.01f, LightingVisualizationFps)));

            _lightingCardContainer = container;
            _lightingCardTicker = container.schedule.Execute(TickLightingCard).Every(intervalMs);

            _lightingCardTicker.Pause();
            parent?.Add(container);
        }

        internal void AddTemporalHistoryVisualizationElement(VisualElement parent)
        {
            var container = CreateVisualizationImGuiContainer(
                () => DrawTemporalHistoryVisualization(
                    Asset.TemporalHistorySettings.MaxHistoryLength,
                    1,
                    Asset.TemporalHistorySettings.LowConfidenceSpatialBlend));

            container.RegisterCallback<PointerEnterEvent>(evt => SetTemporalHistoryCardHovered(IsTemporalHistoryPointerOverVisualization(evt.localPosition)));
            container.RegisterCallback<PointerMoveEvent>(evt => SetTemporalHistoryCardHovered(IsTemporalHistoryPointerOverVisualization(evt.localPosition)));
            container.RegisterCallback<PointerLeaveEvent>(_ => SetTemporalHistoryCardHovered(false));

            container.RegisterCallback<DetachFromPanelEvent>(
                _ =>
            {
                if (ReferenceEquals(_temporalHistoryCardContainer, container))
                {
                    TeardownTemporalHistoryCardTicker();
                }
            });

            var intervalMs = Math.Max(1, Mathf.RoundToInt(1000f / Mathf.Max(.01f, TemporalHistoryVisualizationFps)));

            _temporalHistoryCardContainer = container;
            _temporalHistoryCardTicker = container.schedule.Execute(TickTemporalHistoryCard).Every(intervalMs);

            _temporalHistoryCardTicker.Pause();
            parent?.Add(container);
        }

        internal void InvalidateLightingPreviewLights()
        {
            _isLightingPreviewLightsInitialized = false;
        }

        internal void RepaintAtrousVisualization()
        {
            _atrousCardContainer?.MarkDirtyRepaint();
        }

        internal void AddAtrousVisualizationElement(VisualElement parent)
        {
            var container = CreateVisualizationImGuiContainer(() => DrawAtrousVisualization(
                Asset.ATrousSettings.AtrousIterations,
                Asset.ATrousSettings.AtrousDepthPhi,
                Asset.ATrousSettings.AtrousNormalPhi));

            container.RegisterCallback<PointerEnterEvent>(evt => SetAtrousCardHovered(IsAtrousPointerOverVisualization(evt.localPosition)));
            container.RegisterCallback<PointerMoveEvent>(evt => SetAtrousCardHovered(IsAtrousPointerOverVisualization(evt.localPosition)));
            container.RegisterCallback<PointerLeaveEvent>(_ => SetAtrousCardHovered(false));

            container.RegisterCallback<DetachFromPanelEvent>(
                _ =>
            {
                if (ReferenceEquals(_atrousCardContainer, container))
                {
                    TeardownAtrousCardTicker();
                }
            });

            var intervalMs = Math.Max(1, Mathf.RoundToInt(1000f / Mathf.Max(.01f, AtrousVisualizationFps)));

            _atrousCardContainer = container;
            _atrousCardTicker = container.schedule.Execute(TickAtrousCard).Every(intervalMs);

            _atrousCardTicker.Pause();
            parent?.Add(container);
        }

        private void EnsureUiToolkitInfrastructure()
        {
            if (_uiToolkitView == null)
            {
                _uiToolkitView = new EditorUiToolkitView(
                    _chrome,
                    () =>
                    {
                        EnsureStyles();
                        EnsureAssetState(Asset);
                    },
                    Repaint,
                    () => Asset);
            }

            _uiToolkitFields ??= new EditorUiToolkitFields(_uiToolkitView, this);
        }

        private VisualElement CreateAssetHeaderElement()
        {
            var actionColumn = new VisualElement();

            actionColumn.style.width = 112f;
            actionColumn.style.flexShrink = 0f;

            var badge = new Label();

            badge.style.height = 24f;
            badge.style.marginBottom = 8f;
            badge.style.unityTextAlign = TextAnchor.MiddleCenter;
            badge.style.unityFontStyleAndWeight = FontStyle.Bold;
            badge.style.borderTopLeftRadius = 6f;
            badge.style.borderTopRightRadius = 6f;
            badge.style.borderBottomLeftRadius = 6f;
            badge.style.borderBottomRightRadius = 6f;

            badge.style.backgroundColor = new Color(
                _chrome.Palette.AccentColor.r,
                _chrome.Palette.AccentColor.g,
                _chrome.Palette.AccentColor.b,
                .20f);

            badge.style.color = _chrome.Palette.AccentColor;

            var pingButton = new Button(
                () =>
            {
                var currentAsset = Asset;

                if (currentAsset != null)
                {
                    EditorGUIUtility.PingObject(currentAsset);
                }
            })
            {
                text = "Ping"
            };

            pingButton.style.height = 24f;

            actionColumn.Add(badge);
            actionColumn.Add(pingButton);

            var header = _uiToolkitView.CreateHeader(
                string.Empty,
                "Settings asset that defines how the LucentPath renderer builds, traces, and resolves a frame.",
                out Label titleLabel,
                out _,
                trailing => trailing.Add(actionColumn));

            RegisterUiToolkitRefresh(
                () =>
            {
                var currentAsset = Asset;

                titleLabel.text = "LucentPath Renderer Settings";
                badge.text = IsUsedByCurrentRenderPipeline(currentAsset) ? "Active Asset" : "Inactive";
            });

            return header;
        }

        void IEditorUiToolkitValueSink.CommitUiToolkitValueChange<T>(string cardTitle, string label, T newValue, Action<T> assign)
        {
            CommitUiToolkitValueChange(cardTitle, label, newValue, assign);
        }

        private void CommitUiToolkitValueChange<T>(string cardTitle, string label, T newValue, Action<T> assign)
        {
            if (_uiToolkitView?.IsRefreshing == true)
            {
                return;
            }

            var asset = Asset;

            if (asset == null || assign == null)
            {
                return;
            }

            Undo.RecordObject(asset, "Modify " + label);
            assign(newValue);
            asset.ValidateForEditor();
            EditorUtility.SetDirty(asset);

            RefreshUiToolkitState();
            Repaint();
        }

        private void RefreshUiToolkitState()
        {
            _uiToolkitView?.Refresh(() => EnsureAssetState(Asset));
        }

        private IMGUIContainer CreateVisualizationImGuiContainer(Action draw)
        {
            var container = _uiToolkitView.CreateVisualizationImGuiContainer(
                () =>
            {
                var asset = Asset;

                if (asset == null)
                {
                    return;
                }

                draw?.Invoke();
            });

            container.style.flexGrow = 1f;
            container.style.flexShrink = 0f;

            return container;
        }
    }
}
