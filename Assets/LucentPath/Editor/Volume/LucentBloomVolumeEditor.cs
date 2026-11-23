using LucentPath.Rendering;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace LucentPath.Editor.Volume
{
    [CustomEditor(typeof(LucentBloomVolume))]
    public class LucentBloomVolumeEditor : VolumeComponentEditor
    {
        private SerializedDataParameter _intensity;
        private SerializedDataParameter _radiusScale;
        private SerializedDataParameter _chromaticSpreadScale;
        private SerializedDataParameter _rgbPsfProfile;

        public override void OnEnable()
        {
            var properties = new PropertyFetcher<LucentBloomVolume>(serializedObject);

            _intensity = Unpack(properties.Find(x => x.Intensity));
            _radiusScale = Unpack(properties.Find(x => x.RadiusScale));
            _chromaticSpreadScale = Unpack(properties.Find(x => x.ChromaticSpreadScale));
            _rgbPsfProfile = Unpack(properties.Find(x => x.RgbPsfProfile));
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "Lucent Bloom is a scene-linear spectral PSF bloom. Assign a baked 8x1 linear RGB profile texture from primary-source ocular straylight data; LucentPath uses the texels as normalized bloom band weights.",
                MessageType.Info);

            PropertyField(_intensity);
            PropertyField(_radiusScale);
            PropertyField(_chromaticSpreadScale);
            PropertyField(_rgbPsfProfile);

            if (_rgbPsfProfile.value.objectReferenceValue is Texture texture &&
                LucentBloomValidation.TryGetEditorValidationMessage(texture, out string validationMessage) == false)
            {
                EditorGUILayout.HelpBox(validationMessage, MessageType.Error);
            }
        }
    }
}
