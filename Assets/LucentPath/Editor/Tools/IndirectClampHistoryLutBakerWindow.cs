using UnityEditor;
using UnityEngine;

namespace LucentPath.EditorUi.Tools
{
    public class IndirectClampHistoryLutBakerWindow : EditorWindow
    {
        [SerializeField] private BakeSettings _settings = new BakeSettings();
        private Vector2 _scroll;
        private string _lastReport = string.Empty;

        private const string MenuPath = "Tools/LucentPath/Bake/Indirect Clamp History LUT";
        private const string PrefsKey = "LucentPath.IndirectClampHistoryLutBakeSettings";

        [System.Serializable]
        private class BakeSettings
        {
            public AnimationCurve Curve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
            public int Width = 256;
            public string AssetPath = "Assets/LucentPath/GeneratedIndirectClampHistoryLut.asset";
        }

        [MenuItem(MenuPath)]
        public static void Open()
        {
            var window = GetWindow<IndirectClampHistoryLutBakerWindow>("Indirect Clamp History LUT Baker");

            window.minSize = new Vector2(420, 340);
        }

        private void OnEnable()
        {
            string json = EditorPrefs.GetString(PrefsKey, string.Empty);

            if (string.IsNullOrEmpty(json) == false)
            {
                JsonUtility.FromJsonOverwrite(json, _settings);
            }

            if (_settings.Curve == null || _settings.Curve.length == 0)
            {
                _settings.Curve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
            }
        }

        private void OnDisable()
        {
            EditorPrefs.SetString(PrefsKey, JsonUtility.ToJson(_settings));
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Indirect Clamp History LUT Baker", EditorStyles.boldLabel);

            EditorGUILayout.LabelField(
                "Bakes a curve into a 1D lookup that scales the indirect firefly clamp ceiling by per-pixel history trust. X = history trust (accumulated history length / Max History Length, 0..1); Y = multiplier applied to Indirect Clamp Max. Low trust (fresh/disoccluded) should clamp hard (small multiplier ~1); high trust (converged) can relax (large multiplier, less bias). Assign the baked texture in the Firefly Suppression card.",
                EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Curve", EditorStyles.boldLabel);

            _settings.Curve = EditorGUILayout.CurveField(new GUIContent("Trust -> Multiplier",
                "Maps history trust [0,1] to a multiplier on Indirect Clamp Multiplier."), _settings.Curve, Color.cyan, new Rect(0f, 0f, 1f, 1f));

            if (GUILayout.Button("Reset Curve (flat 1)", GUILayout.Width(180)))
            {
                _settings.Curve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

                GUI.FocusControl(null);
            }

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);

            _settings.Width = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent("Width",
                "Texel count of the 1D lookup texture. 128-256 is plenty."), _settings.Width), 16, 1024);

            EditorGUILayout.BeginHorizontal();

            _settings.AssetPath = EditorGUILayout.TextField(new GUIContent("Asset Path",
                "Project-relative path of the baked R-Float lookup texture."), _settings.AssetPath);

            if (GUILayout.Button("...", GUILayout.Width(28)))
            {
                string picked = EditorUtility.SaveFilePanelInProject(
                    "Indirect Clamp History LUT",
                    "GeneratedIndirectClampHistoryLut",
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

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_settings.AssetPath)))
            {
                if (GUILayout.Button("Bake", GUILayout.Height(32)))
                {
                    Bake();
                    EditorPrefs.SetString(PrefsKey, JsonUtility.ToJson(_settings));
                }
            }

            if (string.IsNullOrEmpty(_lastReport) == false)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(_lastReport, MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }

        private void Bake()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(_settings.AssetPath);

            if (existing != null && existing.width == _settings.Width && existing.format == TextureFormat.RFloat)
            {
                Fill(existing, _settings.Curve);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();

                _lastReport = $"Re-baked {_settings.Width}-texel LUT into existing asset at {_settings.AssetPath}.";
            }
            else
            {
                var texture = CreateTexture(_settings.Width);

                Fill(texture, _settings.Curve);

                if (existing != null)
                {
                    AssetDatabase.DeleteAsset(_settings.AssetPath);
                }

                AssetDatabase.CreateAsset(texture, _settings.AssetPath);
                AssetDatabase.SaveAssets();

                _lastReport = $"Baked {_settings.Width}-texel LUT to {_settings.AssetPath}.";
            }

            AssetDatabase.Refresh();
        }

        private static Texture2D CreateTexture(int width)
        {
            return new Texture2D(Mathf.Max(2, width), 1, TextureFormat.RFloat, false, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "LucentIndirectClampHistoryLut"
            };
        }

        private static void Fill(Texture2D target, AnimationCurve curve)
        {
            int width = target.width;
            var pixels = new Color[width];
            float invMax = width > 1 ? 1f / (width - 1) : 0f;

            for (int i = 0; i < width; i++)
            {
                float t = i * invMax;
                float value = curve != null ? curve.Evaluate(t) : t;

                pixels[i] = new Color(Mathf.Max(0f, value), 0f, 0f, 0f);
            }

            target.SetPixels(pixels);
            target.Apply(false, false);
        }
    }
}
