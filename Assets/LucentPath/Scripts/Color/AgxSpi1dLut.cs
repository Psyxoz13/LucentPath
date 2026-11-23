using System;
using UnityEngine;

namespace LucentPath.Rendering
{
    public class AgxSpi1dLut : ScriptableObject
    {
        public const string OfficialSourceUrl = "https://raw.githubusercontent.com/sobotka/AgX/main/LUTs/AgX_Default_Contrast.spi1d";
        public const string OfficialBlobSha = "fa86d5eeaf73381fa2a4f8381dd69879ecefb311";
        public const int OfficialLength = 4096;
        public const int OfficialComponents = 1;

        public string SourceUrl => _sourceUrl;
        public string SourceBlobSha => _sourceBlobSha;
        public int Version => _version;
        public float From => _from;
        public float To => _to;
        public int Length => _length;
        public int Components => _components;
        public bool IsValidOfficialDefaultContrast => _isValidOfficialDefaultContrast;
        public string ValidationMessage => _validationMessage;
        public float[] Values => _values;

        [SerializeField] private string _sourceUrl;
        [SerializeField] private string _sourceBlobSha;
        [SerializeField] private int _version;
        [SerializeField] private float _from;
        [SerializeField] private float _to = 1f;
        [SerializeField] private int _length;
        [SerializeField] private int _components;
        [SerializeField] private bool _isValidOfficialDefaultContrast;
        [SerializeField] private string _validationMessage;
        [SerializeField] private float[] _values = Array.Empty<float>();

        [NonSerialized] private Texture2D _runtimeTexture;

        public Texture2D GetOrCreateTexture()
        {
            if (_isValidOfficialDefaultContrast == false || _values == null || _values.Length != _length * _components)
            {
                return null;
            }

            if (_runtimeTexture != null && _runtimeTexture.width == _length)
            {
                return _runtimeTexture;
            }

            _runtimeTexture = new Texture2D(_length, 1, TextureFormat.RFloat, false, true)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0
            };

            _runtimeTexture.SetPixelData(_values, 0);
            _runtimeTexture.Apply(false, true);

            return _runtimeTexture;
        }

        public void SetImportedData(
            string importedSourceUrl,
            string importedBlobSha,
            int importedVersion,
            float importedFrom,
            float importedTo,
            int importedLength,
            int importedComponents,
            float[] importedValues,
            bool isValidOfficialDefaultContrast,
            string importedValidationMessage)
        {
            _sourceUrl = importedSourceUrl;
            _sourceBlobSha = importedBlobSha;
            _version = importedVersion;
            _from = importedFrom;
            _to = importedTo;
            _length = importedLength;
            _components = importedComponents;
            _values = importedValues ?? Array.Empty<float>();
            _isValidOfficialDefaultContrast = isValidOfficialDefaultContrast;
            _validationMessage = importedValidationMessage;

            ReleaseRuntimeTexture();
        }

        private void OnDisable()
        {
            ReleaseRuntimeTexture();
        }

        private void ReleaseRuntimeTexture()
        {
            if (_runtimeTexture == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_runtimeTexture);
            }
            else
            {
                DestroyImmediate(_runtimeTexture);
            }

            _runtimeTexture = null;
        }
    }
}
