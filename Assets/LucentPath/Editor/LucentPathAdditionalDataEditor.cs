using LucentPath.Rendering;
using UnityEditor;
using UnityEngine;

namespace LucentPath.EditorUi
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(LucentPathAdditionalData))]
    public class LucentPathAdditionalDataEditor : UnityEditor.Editor
    {
        private static readonly Color _radiusHandleColor = new(1f, .85f, .3f, 1f);

        private void OnSceneGUI()
        {
            var additionalData = (LucentPathAdditionalData)target;

            if (additionalData.TryGetComponent<Light>(out Light light) == false)
            {
                return;
            }

            if (light.type != LightType.Point && light.type != LightType.Spot)
            {
                return;
            }

            var serializedData = new SerializedObject(additionalData);
            var radiusProperty = serializedData.FindProperty("_radius");

            using (new Handles.DrawingScope(_radiusHandleColor))
            {
                EditorGUI.BeginChangeCheck();

                float newRadius = Handles.RadiusHandle(Quaternion.identity, light.transform.position, radiusProperty.floatValue);

                if (EditorGUI.EndChangeCheck())
                {
                    radiusProperty.floatValue = Mathf.Max(0f, newRadius);

                    serializedData.ApplyModifiedProperties();
                }
            }
        }
    }
}
