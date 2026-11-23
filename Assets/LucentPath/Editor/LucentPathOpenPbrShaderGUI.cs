using LucentPath.EditorUi.Core;
using UnityEditor;
using UnityEngine;

namespace LucentPath.EditorUi.Materials
{
    public class LucentPathOpenPbrShaderGUI : ShaderGUI
    {
        private MaterialEditor _materialEditor;
        private MaterialProperty[] _properties;

        private MaterialProperty _baseMap;
        private MaterialProperty _baseColor;
        private MaterialProperty _materialMap;
        private MaterialProperty _baseWeight;
        private MaterialProperty _baseDiffuseRoughness;
        private MaterialProperty _baseMetalness;
        private MaterialProperty _specularWeight;
        private MaterialProperty _specularColor;
        private MaterialProperty _specularRoughness;
        private MaterialProperty _specularIor;
        private MaterialProperty _specularAnisotropy;
        private MaterialProperty _transmissionWeight;
        private MaterialProperty _transmissionColor;
        private MaterialProperty _subsurfaceWeight;
        private MaterialProperty _subsurfaceColor;
        private MaterialProperty _subsurfaceRadius;
        private MaterialProperty _subsurfaceRadiusScale;
        private MaterialProperty _subsurfaceAnisotropy;
        private MaterialProperty _fuzzWeight;
        private MaterialProperty _fuzzColor;
        private MaterialProperty _fuzzRoughness;
        private MaterialProperty _coatWeight;
        private MaterialProperty _coatColor;
        private MaterialProperty _coatRoughness;
        private MaterialProperty _coatAnisotropy;
        private MaterialProperty _coatIor;
        private MaterialProperty _coatDarkening;
        private MaterialProperty _thinFilmWeight;
        private MaterialProperty _thinFilmThickness;
        private MaterialProperty _thinFilmIor;
        private MaterialProperty _emissionLuminance;
        private MaterialProperty _emissionColor;
        private MaterialProperty _emissionMap;
        private MaterialProperty _geometryOpacity;
        private MaterialProperty _geometryThinWalled;
        private MaterialProperty _bumpMap;
        private MaterialProperty _bumpScale;
        private MaterialProperty _alphaClip;
        private MaterialProperty _cutoff;
        private MaterialProperty _cull;

        private EditorChrome _chrome;

        private bool _isOpenPbrShown = true;
        private bool _isGeometryShown = true;
        private bool _isRasterShown = true;

        private static readonly RectOffset _shaderCardPadding = new(-14, 44, 0, 10);

        private static readonly GUIContent _baseMapContent = new(
            "Base Map",
            "OpenPBR base color texture. Alpha multiplies Geometry Opacity and is used by alpha clipping.");

        private static readonly GUIContent _baseColorContent = new(
            "Base Color",
            "OpenPBR base_color multiplier.");

        private static readonly GUIContent _materialMapContent = new(
            "Material Map",
            "R multiplies base_metalness. G multiplies specular_roughness.");

        private static readonly GUIContent _normalMapContent = new(
            "Normal Map",
            "Tangent-space normal map. It changes the shading frame, not geometric ray offsets.");

        private static readonly GUIContent _emissionMapContent = new(
            "Emission Map",
            "Texture multiplier for OpenPBR emission_color and emission_luminance.");

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            _materialEditor = materialEditor;
            _properties = properties;

            CacheProperties(properties);
            EnsureStyles();

            _chrome.DrawVisibleBackground();
            _chrome.DrawFullWidthHeader(GetHeaderTitle(), GetHeaderSubtitle());

            DrawOpenPbrSection();
            DrawGeometrySection();
            DrawRasterSection();
        }

        private void DrawOpenPbrSection()
        {
            DrawSection(
                "OpenPBR Surface",
                "Canonical OpenPBR controls used by traced surface response.",
                () => _isOpenPbrShown,
                value => _isOpenPbrShown = value,
                () =>
                {
                    DrawCard("Base", "Base layer weight, color, diffuse roughness, and metalness.", DrawBaseCard);
                    DrawCard("Specular", "Dielectric interface and metal Fresnel controls shared by the OpenPBR base.", DrawSpecularCard);
                    DrawCard("Transmission", "RGB-supported OpenPBR transmission controls.", DrawTransmissionCard);
                    DrawCard("Subsurface", "Random-walk subsurface controls mapped from OpenPBR parameters.", DrawSubsurfaceCard);
                    DrawCard("Fuzz", "OpenPBR fuzz layer implemented with the renderer's sheen lobe.", DrawFuzzCard);
                    DrawCard("Coat", "Top dielectric coat layer with color, roughness, IOR, anisotropy, and darkening.", DrawCoatCard);
                    DrawCard("Thin Film", "RGB thin-film modulation applied to supported reflection lobes.", DrawThinFilmCard);
                    DrawCard("Emission", "Self-emissive radiance from emission luminance, color, and texture.", DrawEmissionCard);
                });
        }

        private void DrawGeometrySection()
        {
            DrawSection(
                "Geometry",
                "Opacity, thin-walled transport, normal mapping, and ray-facing controls.",
                () => _isGeometryShown,
                value => _isGeometryShown = value,
                () =>
                {
                    DrawCard("Opacity", "OpenPBR geometry opacity and alpha clipping.", DrawOpacityCard);
                    DrawCard("Normal", "Tangent-space normal perturbation for the shading frame.", DrawNormalCard);
                });
        }

        private void DrawRasterSection()
        {
            DrawSection(
                "Raster Render",
                "Viewport fallback controls that do not alter the OpenPBR BSDF definition.",
                () => _isRasterShown,
                value => _isRasterShown = value,
                () => DrawCard("Advanced", "Low-level preview and culling flags.", DrawAdvancedCard));
        }

        private void DrawBaseCard()
        {
            if (_baseMap != null)
            {
                if (_baseColor != null)
                {
                    _chrome.DrawTexturePropertySingleLine(_materialEditor, _baseMapContent, _baseMap, _baseColor);
                }
                else
                {
                    _chrome.DrawTexturePropertySingleLine(_materialEditor, _baseMapContent, _baseMap);
                }

                _materialEditor.TextureScaleOffsetProperty(_baseMap);
            }
            else
            {
                DrawProperty(_baseColor, _baseColorContent);
            }

            if (_materialMap != null)
            {
                _chrome.DrawTexturePropertySingleLine(_materialEditor, _materialMapContent, _materialMap);
            }

            DrawProperty(_baseWeight);
            DrawProperty(_baseDiffuseRoughness);
            DrawProperty(_baseMetalness);
        }

        private void DrawSpecularCard()
        {
            DrawProperty(_specularWeight);
            DrawProperty(_specularColor);
            DrawProperty(_specularRoughness);
            DrawProperty(_specularIor);
            DrawProperty(_specularAnisotropy);
            _chrome.DrawNote("Dispersion is intentionally absent: this renderer is RGB, not spectral.");
        }

        private void DrawTransmissionCard()
        {
            DrawProperty(_transmissionWeight);
            DrawProperty(_transmissionColor);
            _chrome.DrawNote("Transmission uses RGB tint and thin-walled/thick behavior from Geometry Thin Walled.");
        }

        private void DrawSubsurfaceCard()
        {
            DrawProperty(_subsurfaceWeight);
            DrawProperty(_subsurfaceColor);
            DrawProperty(_subsurfaceRadius);
            DrawProperty(_subsurfaceRadiusScale);
            DrawProperty(_subsurfaceAnisotropy);
            _chrome.DrawNote("Subsurface requires pipeline Subsurface Scattering to be enabled.");
        }

        private void DrawFuzzCard()
        {
            DrawProperty(_fuzzWeight);
            DrawProperty(_fuzzColor);
            DrawProperty(_fuzzRoughness);
        }

        private void DrawCoatCard()
        {
            DrawProperty(_coatWeight);
            DrawProperty(_coatColor);
            DrawProperty(_coatRoughness);
            DrawProperty(_coatAnisotropy);
            DrawProperty(_coatIor);
            DrawProperty(_coatDarkening);
        }

        private void DrawThinFilmCard()
        {
            DrawProperty(_thinFilmWeight);
            DrawProperty(_thinFilmThickness);
            DrawProperty(_thinFilmIor);
        }

        private void DrawEmissionCard()
        {
            DrawProperty(_emissionLuminance);
            DrawProperty(_emissionColor);

            if (_emissionMap != null)
            {
                _chrome.DrawTexturePropertySingleLine(_materialEditor, _emissionMapContent, _emissionMap);
            }
        }

        private void DrawOpacityCard()
        {
            DrawProperty(_geometryOpacity);
            DrawProperty(_geometryThinWalled);
            DrawProperty(_alphaClip);

            if (IsEnabled(_alphaClip))
            {
                EditorGUI.indentLevel++;
                DrawProperty(_cutoff);
                EditorGUI.indentLevel--;
            }
        }

        private void DrawNormalCard()
        {
            if (_bumpMap != null)
            {
                _chrome.DrawTexturePropertySingleLine(_materialEditor, _normalMapContent, _bumpMap);
            }

            DrawProperty(_bumpScale);
            _chrome.DrawNote("Normal mapping only affects shading normals; geometric normals still drive ray offsets.");
        }

        private void DrawAdvancedCard()
        {
            DrawProperty(_cull);
        }

        private void DrawSection(string title, string hint, System.Func<bool> getExpanded, System.Action<bool> setExpanded, System.Action drawContent)
        {
            var current = getExpanded?.Invoke() ?? true;

            _chrome.DrawSectionShell(title, hint, current, () => setExpanded?.Invoke(current == false), drawContent);
        }

        private void DrawCard(string title, string hint, System.Action drawContent)
        {
            _chrome.DrawCard(title, hint, _shaderCardPadding, () => _chrome.DrawWithPipelineFieldStyling(drawContent));
        }

        private void DrawProperty(MaterialProperty property, GUIContent content = null)
        {
            if (property == null)
            {
                return;
            }

            if (property.propertyType == UnityEngine.Rendering.ShaderPropertyType.Range)
            {
                DrawRangeProperty(property, content ?? new GUIContent(property.displayName));

                return;
            }

            _materialEditor.ShaderProperty(property, content ?? new GUIContent(property.displayName));
        }

        private void DrawRangeProperty(MaterialProperty property, GUIContent content)
        {
            var limits = property.rangeLimits;
            var rowRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            var previousMixedValue = EditorGUI.showMixedValue;

            try
            {
                EditorGUI.showMixedValue = property.hasMixedValue;

                EditorGUI.BeginChangeCheck();

                var newValue = EditorGUI.Slider(rowRect, content, property.floatValue, limits.x, limits.y);

                if (EditorGUI.EndChangeCheck() == false)
                {
                    return;
                }

                _materialEditor.RegisterPropertyChangeUndo(content?.text ?? property.displayName);

                property.floatValue = newValue;
            }
            finally
            {
                EditorGUI.showMixedValue = previousMixedValue;
            }
        }

        private void CacheProperties(MaterialProperty[] properties)
        {
            _baseMap = Find("_BaseMap", properties);
            _baseColor = Find("_BaseColor", properties);
            _materialMap = Find("_MetallicRoughnessMap", properties);
            _baseWeight = Find("_OpenPbrBaseWeight", properties);
            _baseDiffuseRoughness = Find("_OpenPbrBaseDiffuseRoughness", properties);
            _baseMetalness = Find("_OpenPbrBaseMetalness", properties);
            _specularWeight = Find("_OpenPbrSpecularWeight", properties);
            _specularColor = Find("_OpenPbrSpecularColor", properties);
            _specularRoughness = Find("_OpenPbrSpecularRoughness", properties);
            _specularIor = Find("_OpenPbrSpecularIOR", properties);
            _specularAnisotropy = Find("_OpenPbrSpecularRoughnessAnisotropy", properties);
            _transmissionWeight = Find("_OpenPbrTransmissionWeight", properties);
            _transmissionColor = Find("_OpenPbrTransmissionColor", properties);
            _subsurfaceWeight = Find("_OpenPbrSubsurfaceWeight", properties);
            _subsurfaceColor = Find("_OpenPbrSubsurfaceColor", properties);
            _subsurfaceRadius = Find("_OpenPbrSubsurfaceRadius", properties);
            _subsurfaceRadiusScale = Find("_OpenPbrSubsurfaceRadiusScale", properties);
            _subsurfaceAnisotropy = Find("_OpenPbrSubsurfaceScatterAnisotropy", properties);
            _fuzzWeight = Find("_OpenPbrFuzzWeight", properties);
            _fuzzColor = Find("_OpenPbrFuzzColor", properties);
            _fuzzRoughness = Find("_OpenPbrFuzzRoughness", properties);
            _coatWeight = Find("_OpenPbrCoatWeight", properties);
            _coatColor = Find("_OpenPbrCoatColor", properties);
            _coatRoughness = Find("_OpenPbrCoatRoughness", properties);
            _coatAnisotropy = Find("_OpenPbrCoatRoughnessAnisotropy", properties);
            _coatIor = Find("_OpenPbrCoatIOR", properties);
            _coatDarkening = Find("_OpenPbrCoatDarkening", properties);
            _thinFilmWeight = Find("_OpenPbrThinFilmWeight", properties);
            _thinFilmThickness = Find("_OpenPbrThinFilmThickness", properties);
            _thinFilmIor = Find("_OpenPbrThinFilmIOR", properties);
            _emissionLuminance = Find("_OpenPbrEmissionLuminance", properties);
            _emissionColor = Find("_OpenPbrEmissionColor", properties);
            _emissionMap = Find("_EmissionMap", properties);
            _geometryOpacity = Find("_OpenPbrGeometryOpacity", properties);
            _geometryThinWalled = Find("_OpenPbrGeometryThinWalled", properties);
            _bumpMap = Find("_BumpMap", properties);
            _bumpScale = Find("_BumpScale", properties);
            _alphaClip = Find("_AlphaClip", properties);
            _cutoff = Find("_Cutoff", properties);
            _cull = Find("_Cull", properties);
        }

        private MaterialProperty Find(string name, MaterialProperty[] properties)
        {
            return FindProperty(name, properties, false);
        }

        private void EnsureStyles()
        {
            _chrome ??= new EditorChrome(useSdfCardRounding: true);

            _chrome.EnsureStyles();
        }

        private string GetHeaderTitle()
        {
            if (_materialEditor != null && _materialEditor.targets != null && _materialEditor.targets.Length > 1)
            {
                return "OpenPBR Materials";
            }

            var material = _materialEditor?.target as Material;

            return material != null && material.shader != null ? material.shader.name : "OpenPBR Material";
        }

        private string GetHeaderSubtitle()
        {
            return "OpenPBR v1.1.1 RGB surface. Spectral-only dispersion controls are intentionally not exposed.";
        }

        private static bool IsEnabled(MaterialProperty property)
        {
            return property != null && (property.hasMixedValue || property.floatValue > .5f);
        }
    }
}
