using LucentPath.EditorUi.Core;
using LucentPath.Rendering;
using LucentPath.Upscale.DLSS;
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace LucentPath.EditorUi.RendererSettings
{
    internal abstract class RendererSettingsEditorCardBase : EditorCardBase<RendererSettingsEditorContext>
    {
        protected readonly LucentPathRendererSettingsEditor Editor;

        protected LucentPathRendererSettings Asset => Editor.Asset;

        public override RectOffset ImguiPadding => _pipelineCardPadding;

        private static readonly RectOffset _pipelineCardPadding = new RectOffset(10, 10, 4, 10);

        protected RendererSettingsEditorCardBase(LucentPathRendererSettingsEditor editor, string title, string hint)
            : base(title, hint)
        {
            Editor = editor;
        }

        public override void DrawIMGUI(RendererSettingsEditorContext context)
        { }

        protected VisualElement AddToggleField(VisualElement parent, string label, Func<bool> getValue, Action<bool> assign, string tooltip = null)
        {
            return Editor.AddToggleField(parent, Title, label, getValue, assign, tooltip);
        }

        protected VisualElement AddMinIntField(VisualElement parent, string label, Func<int> getValue, Action<int> assign, int minValue, string tooltip = null)
        {
            return Editor.AddMinIntField(parent, Title, label, getValue, assign, minValue, tooltip);
        }

        protected VisualElement AddIntField(VisualElement parent, string label, Func<int> getValue, Action<int> assign, Func<int, int> normalize = null, string tooltip = null)
        {
            return Editor.AddIntField(parent, Title, label, getValue, assign, normalize, tooltip);
        }

        protected VisualElement AddIntSliderField(VisualElement parent, string label, Func<int> getValue, Action<int> assign, int minValue, int maxValue, string tooltip = null)
        {
            return Editor.AddIntSliderField(parent, Title, label, getValue, assign, minValue, maxValue, tooltip);
        }

        protected VisualElement AddIntSliderField(VisualElement parent, string label, Func<int> getValue, Action<int> assign, Func<int> getMinValue, Func<int> getMaxValue, string tooltip = null)
        {
            return Editor.AddIntSliderField(parent, Title, label, getValue, assign, getMinValue, getMaxValue, tooltip);
        }

        protected VisualElement AddFloatField(VisualElement parent, string label, Func<float> getValue, Action<float> assign, string tooltip = null)
        {
            return Editor.AddFloatField(parent, Title, label, getValue, assign, tooltip);
        }

        protected VisualElement AddMinFloatField(VisualElement parent, string label, Func<float> getValue, Action<float> assign, float minValue, string tooltip = null)
        {
            return Editor.AddMinFloatField(parent, Title, label, getValue, assign, minValue, tooltip);
        }

        protected VisualElement AddLiveMinFloatField(VisualElement parent, string label, Func<float> getValue, Action<float> assign, float minValue, string tooltip = null)
        {
            return Editor.AddLiveMinFloatField(parent, Title, label, getValue, assign, minValue, tooltip);
        }

        protected VisualElement AddClampedFloatField(VisualElement parent, string label, Func<float> getValue, Action<float> assign, float minValue, Func<float> getMaxValue, string tooltip = null)
        {
            return Editor.AddClampedFloatField(parent, Title, label, getValue, assign, minValue, getMaxValue, tooltip);
        }

        protected VisualElement AddFloatSliderField(VisualElement parent, string label, Func<float> getValue, Action<float> assign, float minValue, float maxValue, string tooltip = null)
        {
            return Editor.AddFloatSliderField(parent, Title, label, getValue, assign, minValue, maxValue, tooltip);
        }

        protected VisualElement AddColorField(VisualElement parent, string label, Func<Color> getValue, Action<Color> assign, string tooltip = null)
        {
            return Editor.AddColorField(parent, Title, label, getValue, assign, tooltip);
        }

        protected VisualElement AddObjectField<T>(VisualElement parent, string label, Func<T> getValue, Action<T> assign, string tooltip = null)
            where T : UnityEngine.Object
        {
            return Editor.AddObjectField(parent, Title, label, getValue, assign, tooltip);
        }

        protected VisualElement AddTextField(VisualElement parent, string label, Func<string> getValue, Action<string> assign, string tooltip = null)
        {
            return Editor.AddTextField(parent, Title, label, getValue, assign, tooltip);
        }

        protected VisualElement AddEnumField<TEnum>(VisualElement parent, string label, Func<TEnum> getValue, Action<TEnum> assign, string tooltip = null)
            where TEnum : Enum
        {
            return Editor.AddEnumField(parent, Title, label, getValue, assign, tooltip);
        }

        protected void RegisterConditionalElement(VisualElement element, Func<bool> isVisible)
        {
            Editor.RegisterConditionalElement(element, isVisible);
        }

        protected void RegisterUiToolkitRefresh(Action refresh)
        {
            Editor.RegisterUiToolkitRefresh(refresh);
        }

        protected void AddVisualizationElement(VisualElement parent, Action draw)
        {
            Editor.AddVisualizationElement(parent, draw);
        }

        protected void AddBounceDistanceVisualizationElement(VisualElement parent)
        {
            Editor.AddBounceDistanceVisualizationElement(parent);
        }

        protected void AddSamplesPerPixelVisualizationElement(VisualElement parent)
        {
            Editor.AddSamplesPerPixelVisualizationElement(parent);
        }

        protected void AddBounceCountVisualizationElement(VisualElement parent)
        {
            Editor.AddBounceCountVisualizationElement(parent);
        }

        protected void AddLightingVisualizationElement(VisualElement parent)
        {
            Editor.AddLightingVisualizationElement(parent);
        }

        protected void AddTemporalHistoryVisualizationElement(VisualElement parent)
        {
            Editor.AddTemporalHistoryVisualizationElement(parent);
        }

        protected void AddAtrousVisualizationElement(VisualElement parent)
        {
            Editor.AddAtrousVisualizationElement(parent);
        }

        protected void InvalidateLightingPreviewLights()
        {
            Editor.InvalidateLightingPreviewLights();
        }

        protected void RepaintAtrousVisualization()
        {
            Editor.RepaintAtrousVisualization();
        }

        protected void ClampBounceDistanceToCameraLimit()
        {
            Editor.ClampBounceDistanceToCameraLimit();
        }

        protected void InvalidateSampleVisualization()
        {
            Editor.InvalidateSampleVisualization();
        }

        protected float GetGameCameraFarDistance(float fallback)
        {
            return Editor.GetGameCameraFarDistance(fallback);
        }

        protected void DrawBiasVisualization(float bias)
        {
            Editor.DrawBiasVisualization(bias);
        }

        protected void DrawEnvironmentVisualization(Color top, Color bottom, float intensity, float exponent)
        {
            Editor.DrawEnvironmentVisualization(top, bottom, intensity, exponent);
        }

        protected void DrawSceneUpdateVisualization(int rendererFrames, int lightFrames, bool skinnedOffscreen)
        {
            Editor.DrawSceneUpdateVisualization(rendererFrames, lightFrames, skinnedOffscreen);
        }

        protected static bool IsDlssRuntimeActive(DlssSettings settings)
        {
            return settings != null && settings.IsRuntimeActive;
        }
    }
}
