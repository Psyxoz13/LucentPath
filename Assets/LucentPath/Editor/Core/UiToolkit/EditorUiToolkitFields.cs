using System;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace LucentPath.EditorUi.Core
{
    internal class EditorUiToolkitFields
    {
        private readonly EditorUiToolkitView _view;
        private readonly IEditorUiToolkitValueSink _valueSink;

        public EditorUiToolkitFields(EditorUiToolkitView view, IEditorUiToolkitValueSink valueSink)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _valueSink = valueSink ?? throw new ArgumentNullException(nameof(valueSink));
        }

        public VisualElement AddToggleField(VisualElement parent, string cardTitle, string label, Func<bool> getValue, Action<bool> assign, string tooltip = null)
        {
            var field = _view.CreateToggleControl(
                getValue(),
                value => _valueSink.CommitUiToolkitValueChange(cardTitle, label, value, assign),
                out Action<bool> setValueWithoutNotify,
                out Action toggleValue);

            _view.ConfigureControl(field, false);

            var row = _view.CreateFieldRow(cardTitle, label, field, tooltip, false, out Label labelElement);

            EditorUiToolkitView.ConfigureToggleControlSize(field);
            EditorUiToolkitView.ConfigureToggleLabelOffset(labelElement);
            EditorUiToolkitView.RegisterToggleLabelClick(labelElement, field, toggleValue);
            _view.RegisterRefresh(() => setValueWithoutNotify(getValue()));
            parent?.Add(row);

            return row;
        }

        public VisualElement AddMinIntField(VisualElement parent, string cardTitle, string label, Func<int> getValue, Action<int> assign, int minValue, string tooltip = null)
        {
            return AddIntField(parent, cardTitle, label, getValue, assign, value => Mathf.Max(minValue, value), tooltip);
        }

        public VisualElement AddIntField(VisualElement parent, string cardTitle, string label, Func<int> getValue, Action<int> assign, Func<int, int> normalize = null, string tooltip = null)
        {
            var field = new IntegerField { label = label, isDelayed = true };

            return AddNativeField(parent, cardTitle, label, field, tooltip, getValue, assign, normalize, true, true, true, true);
        }

        public VisualElement AddIntSliderField(VisualElement parent, string cardTitle, string label, Func<int> getValue, Action<int> assign, int minValue, int maxValue, string tooltip = null)
        {
            return AddIntSliderField(parent, cardTitle, label, getValue, assign, () => minValue, () => maxValue, tooltip);
        }

        public VisualElement AddIntSliderField(VisualElement parent, string cardTitle, string label, Func<int> getValue, Action<int> assign, Func<int> getMinValue, Func<int> getMaxValue, string tooltip = null)
        {
            var initialMin = getMinValue();
            var initialMax = Mathf.Max(initialMin, getMaxValue());

            var field = new SliderInt(initialMin, initialMax)
            {
                label = label,
                showInputField = true
            };

            var row = AddNativeField(
                parent,
                cardTitle,
                label,
                field,
                tooltip,
                getValue,
                assign,
                value =>
            {
                var min = getMinValue();
                var max = Mathf.Max(min, getMaxValue());

                return Mathf.Clamp(value, min, max);
            },
                false,
                true,
                true,
                false,
                slider =>
            {
                var min = getMinValue();
                var max = Mathf.Max(min, getMaxValue());

                slider.lowValue = min;
                slider.highValue = max;
            });

            _view.RegisterSliderUndoGroup(field, label);

            return row;
        }

        public VisualElement AddFloatField(VisualElement parent, string cardTitle, string label, Func<float> getValue, Action<float> assign, string tooltip = null)
        {
            return AddFloatField(parent, cardTitle, label, getValue, assign, value => value, tooltip);
        }

        public VisualElement AddMinFloatField(VisualElement parent, string cardTitle, string label, Func<float> getValue, Action<float> assign, float minValue, string tooltip = null)
        {
            return AddFloatField(parent, cardTitle, label, getValue, assign, value => Mathf.Max(minValue, value), tooltip);
        }

        public VisualElement AddLiveMinFloatField(VisualElement parent, string cardTitle, string label, Func<float> getValue, Action<float> assign, float minValue, string tooltip = null)
        {
            return AddLiveFloatField(parent, cardTitle, label, getValue, assign, value => Mathf.Max(minValue, value), tooltip);
        }

        public VisualElement AddClampedFloatField(VisualElement parent, string cardTitle, string label, Func<float> getValue, Action<float> assign, float minValue, Func<float> getMaxValue, string tooltip = null)
        {
            return AddFloatField(
                parent,
                cardTitle,
                label,
                getValue,
                assign,
                value =>
            {
                var maxValue = getMaxValue();

                return float.IsPositiveInfinity(maxValue)
                    ? Mathf.Max(minValue, value)
                    : Mathf.Clamp(value, minValue, maxValue);
            },
                tooltip);
        }

        public VisualElement AddFloatField(VisualElement parent, string cardTitle, string label, Func<float> getValue, Action<float> assign, Func<float, float> normalize, string tooltip = null)
        {
            var field = new FloatField { label = label, isDelayed = true };

            return AddNativeField(parent, cardTitle, label, field, tooltip, getValue, assign, normalize, true, true, true, true);
        }

        public VisualElement AddLiveFloatField(VisualElement parent, string cardTitle, string label, Func<float> getValue, Action<float> assign, Func<float, float> normalize, string tooltip = null)
        {
            var field = new FloatField { label = label, isDelayed = false };

            return AddNativeField(parent, cardTitle, label, field, tooltip, getValue, assign, normalize, true, true, true, true);
        }

        public VisualElement AddFloatSliderField(VisualElement parent, string cardTitle, string label, Func<float> getValue, Action<float> assign, float minValue, float maxValue, string tooltip = null)
        {
            var field = new Slider(minValue, maxValue)
            {
                label = label,
                showInputField = true
            };

            var row = AddNativeField(
                parent,
                cardTitle,
                label,
                field,
                tooltip,
                getValue,
                assign,
                value => Mathf.Clamp(value, minValue, maxValue),
                false,
                true,
                true,
                false);

            _view.RegisterSliderUndoGroup(field, label);

            return row;
        }

        public VisualElement AddColorField(VisualElement parent, string cardTitle, string label, Func<Color> getValue, Action<Color> assign, string tooltip = null)
        {
            var field = new ColorField { label = label };

            return AddNativeField(parent, cardTitle, label, field, tooltip, getValue, assign);
        }

        public VisualElement AddObjectField<T>(VisualElement parent, string cardTitle, string label, Func<T> getValue, Action<T> assign, string tooltip = null)
            where T : UnityEngine.Object
        {
            var field = new ObjectField
            {
                label = label,
                objectType = typeof(T),
                allowSceneObjects = false
            };

            return AddNativeField<UnityEngine.Object, ObjectField>(parent, cardTitle, label, field, tooltip, getValue, value => assign(value as T));
        }

        public VisualElement AddTextField(VisualElement parent, string cardTitle, string label, Func<string> getValue, Action<string> assign, string tooltip = null)
        {
            var field = new TextField { label = label, isDelayed = true };

            return AddNativeField(parent, cardTitle, label, field, tooltip, getValue, assign, value => value ?? string.Empty);
        }

        public VisualElement AddEnumField<TEnum>(VisualElement parent, string cardTitle, string label, Func<TEnum> getValue, Action<TEnum> assign, string tooltip = null)
            where TEnum : Enum
        {
            var field = new EnumField(label, getValue());

            return AddNativeField<Enum, EnumField>(parent, cardTitle, label, field, tooltip, () => getValue(), value => assign((TEnum)(object)value));
        }

        private VisualElement AddNativeField<TValue, TField>(
            VisualElement parent,
            string cardTitle,
            string label,
            TField field,
            string tooltip,
            Func<TValue> getValue,
            Action<TValue> assign,
            Func<TValue, TValue> normalize = null,
            bool compactControl = false,
            bool centerText = false,
            bool numericInput = false,
            bool tintBaseInput = true,
            Action<TField> refreshField = null)
            where TField : BaseField<TValue>
        {
            normalize ??= value => value;

            _view.ConfigureControl(field, false);
            _view.ApplyInputStyling(field, centerText, numericInput, tintBaseInput);

            var row = _view.CreateNativeFieldRow(cardTitle, label, field, tooltip, compactControl);

            field.RegisterValueChangedCallback(
                evt =>
            {
                var normalizedValue = normalize(evt.newValue);

                _valueSink.CommitUiToolkitValueChange(cardTitle, label, normalizedValue, assign);
            });

            _view.RegisterRefresh(
                () =>
            {
                refreshField?.Invoke(field);
                field.SetValueWithoutNotify(normalize(getValue()));
            });

            parent?.Add(row);

            return row;
        }
    }
}
