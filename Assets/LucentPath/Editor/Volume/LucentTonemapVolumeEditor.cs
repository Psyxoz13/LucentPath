using LucentPath.Rendering;
using UnityEditor;
using UnityEditor.Rendering;

namespace LucentPath.Editor.Volume
{
    [CustomEditor(typeof(LucentTonemapVolume))]
    public class LucentTonemapVolumeEditor : VolumeComponentEditor
    {
        private SerializedDataParameter _mode;
        private SerializedDataParameter _look;
        private SerializedDataParameter _defaultContrastLut;
        private SerializedDataParameter _applyInSceneView;

        public override void OnEnable()
        {
            var properties = new PropertyFetcher<LucentTonemapVolume>(serializedObject);

            _mode = Unpack(properties.Find(x => x.Mode));
            _look = Unpack(properties.Find(x => x.Look));
            _defaultContrastLut = Unpack(properties.Find(x => x.DefaultContrastLut));
            _applyInSceneView = Unpack(properties.Find(x => x.ApplyInSceneView));
        }

        public override void OnInspectorGUI()
        {
            PropertyField(_mode);

            var mode = (LucentTonemapMode)_mode.value.intValue;

            if (mode == LucentTonemapMode.None)
            {
                return;
            }

            PropertyField(_applyInSceneView);

            switch (mode)
            {
                case LucentTonemapMode.AgX:
                    EditorGUILayout.HelpBox(
                        "AgX uses the official AgX Default Contrast .spi1d LUT and documented Base/Golden/Punchy looks.",
                        MessageType.Info);

                    PropertyField(_look);
                    PropertyField(_defaultContrastLut);
                    break;

                case LucentTonemapMode.Aces:
                    EditorGUILayout.HelpBox(
                        "ACES uses Unity Core RP's ACES tonemapper path for a filmic game-ready display transform.",
                        MessageType.Info);

                    break;

                case LucentTonemapMode.Neutral:
                    EditorGUILayout.HelpBox(
                        "Neutral uses Unity Core RP's neutral range remap with minimal hue and saturation impact.",
                        MessageType.Info);

                    break;

                case LucentTonemapMode.KhronosPbrNeutral:
                    EditorGUILayout.HelpBox(
                        "Khronos PBR Neutral uses the official Khronos reference operator for faithful PBR base-color reproduction.",
                        MessageType.Info);

                    break;
            }
        }
    }
}
