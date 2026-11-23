using UnityEditor;
using UnityEngine;

namespace LucentPath.EditorUi.Tools
{
    public class BlueNoiseBakerWindow : EditorWindow
    {
        [SerializeField] private BlueNoiseBakeSettings _settings = new BlueNoiseBakeSettings();
        private Vector2 _scroll;
        private string _lastReport = string.Empty;

        private const string MenuPath = "Tools/LucentPath/Bake/Blue Noise Texture Array";
        private const string PrefsKey = "LucentPath.BlueNoiseBakeSettings";

        [MenuItem(MenuPath)]
        private static void Open()
        {
            var window = GetWindow<BlueNoiseBakerWindow>("Blue Noise Baker");

            window.minSize = new Vector2(420, 520);
        }

        private void OnEnable()
        {
            string json = EditorPrefs.GetString(PrefsKey, string.Empty);

            if (string.IsNullOrEmpty(json) == false)
            {
                JsonUtility.FromJsonOverwrite(json, _settings);
            }
        }

        private void OnDisable()
        {
            EditorPrefs.SetString(PrefsKey, JsonUtility.ToJson(_settings));
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Spatiotemporal Blue Noise Baker", EditorStyles.boldLabel);

            EditorGUILayout.LabelField(
                "Void-and-cluster (Ulichney 1993) with decoupled spatial/temporal energy (Wolfe et al. 2022).",
                EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Texture", EditorStyles.boldLabel);

            _settings.Width = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent("Width",
                "Spatial resolution of each slice. The texture tiles toroidally across the screen; 64-256 is typical."), _settings.Width), 8, 1024);

            _settings.Height = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent("Height",
                "Spatial resolution of each slice. The texture tiles toroidally across the screen; 64-256 is typical."), _settings.Height), 8, 1024);

            _settings.Layers = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent("Layers",
                "Temporal depth of the array. Each slice is an independent 2D blue noise mask and each pixel forms a 1D blue noise sequence over layers."), _settings.Layers), 1, 256);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Energy Function", EditorStyles.boldLabel);

            _settings.SpatialSigma = EditorGUILayout.Slider(new GUIContent("Spatial Sigma",
                "Gaussian sigma of the spatial energy term exp(-(dx²+dy²)/2σ²), exchanged only between pixels in the same slice. 1.9 is the standard value."), _settings.SpatialSigma, .5f, 4f);

            _settings.TemporalSigma = EditorGUILayout.Slider(new GUIContent("Temporal Sigma",
                "Gaussian sigma of the temporal energy term exp(-dt²/2σ²), exchanged only between the same pixel across slices. 1.9 is the standard value."), _settings.TemporalSigma, .5f, 4f);

            _settings.InitialOneFraction = EditorGUILayout.Slider(new GUIContent("Initial Ones Fraction",
                "Fraction of minority pixels in the random prototype pattern. Must stay below 0.5; Ulichney suggests around 0.1."), _settings.InitialOneFraction, .01f, .49f);

            _settings.RandomSeed = EditorGUILayout.IntField(new GUIContent("Random Seed",
                "Seed for the initial random binary pattern. Same seed and parameters reproduce the same texture."), _settings.RandomSeed);

            if (GUILayout.Button("Randomize Seed", GUILayout.Width(120)))
            {
                _settings.RandomSeed = (int)(EditorApplication.timeSinceStartup * 1000.0) ^ System.Environment.TickCount;
            }

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            _settings.AssetPath = EditorGUILayout.TextField(new GUIContent("Asset Path",
                "Project-relative path of the baked Texture2DArray. Re-baking over an existing asset keeps references intact."), _settings.AssetPath);

            if (GUILayout.Button("...", GUILayout.Width(28)))
            {
                string picked = EditorUtility.SaveFilePanelInProject(
                    "Blue Noise Asset",
                    "GeneratedBlueNoise",
                    "asset",
                    string.Empty,
                    "Assets/LucentPath");

                if (string.IsNullOrEmpty(picked) == false)
                {
                    _settings.AssetPath = picked;
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            int voxelCount = _settings.Width * _settings.Height * _settings.Layers;
            int spatialRadius = BlueNoiseTextureBaker.GetKernelRadius(_settings.SpatialSigma);
            int temporalRadius = BlueNoiseTextureBaker.GetKernelRadius(_settings.TemporalSigma);

            EditorGUILayout.LabelField("Statistics", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Voxels: {voxelCount:N0}   |   R8 size: {voxelCount / 1024f:F0} KiB");
            EditorGUILayout.LabelField($"Kernel truncation (3σ): spatial r={spatialRadius}, temporal r={temporalRadius}");

            if (voxelCount > 1 << 19)
            {
                EditorGUILayout.HelpBox(
                    "Bake time grows roughly quadratically with the voxel count. This configuration may take several minutes; the progress bar is cancelable.",
                    MessageType.Warning);
            }

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_settings.AssetPath)))
            {
                if (GUILayout.Button("Bake", GUILayout.Height(32)))
                {
                    BlueNoiseTextureBaker.Bake(_settings, out _lastReport);
                    EditorPrefs.SetString(PrefsKey, JsonUtility.ToJson(_settings));
                }
            }

            if (GUILayout.Button("Reset To Defaults"))
            {
                _settings = new BlueNoiseBakeSettings();

                GUI.FocusControl(null);
            }

            if (string.IsNullOrEmpty(_lastReport) == false)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(_lastReport, MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
