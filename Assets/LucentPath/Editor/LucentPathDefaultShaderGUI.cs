using LucentPath.EditorUi.Core;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LucentPath.EditorUi.Materials
{
    public class LucentPathDefaultShaderGUI : ShaderGUI
    {
        private MaterialEditor _materialEditor;
        private MaterialProperty _baseMap;
        private MaterialProperty _baseColor;
        private MaterialProperty _metallic;
        private MaterialProperty _roughness;
        private MaterialProperty _anisotropy;
        private MaterialProperty _metallicRoughnessMap;
        private MaterialProperty _materialModel;
        private MaterialProperty _ior;
        private MaterialProperty _transparency;
        private MaterialProperty _eta;
        private MaterialProperty _k;
        private MaterialProperty _conductorEdgeTint;
        private MaterialProperty _clearcoat;
        private MaterialProperty _clearcoatRoughness;
        private MaterialProperty _clearcoatIor;
        private MaterialProperty _subsurfaceWeight;
        private MaterialProperty _subsurfaceColor;
        private MaterialProperty _subsurfaceRadius;
        private MaterialProperty _subsurfaceScale;
        private MaterialProperty _subsurfaceAnisotropy;
        private MaterialProperty _bumpMap;
        private MaterialProperty _bumpScale;
        private MaterialProperty _emissionColor;
        private MaterialProperty _emissionMap;
        private MaterialProperty _occlusionMap;
        private MaterialProperty _occlusionStrength;
        private MaterialProperty _alphaClip;
        private MaterialProperty _cutoff;
        private MaterialProperty _cull;

        private EditorChrome _chrome;
        private MaterialEditorContext _uiContext;
        private IReadOnlyList<MaterialEditorSection> _sections;

        private bool _isPathTracingRenderShown = true;
        private bool _isRasterRenderShown = true;

        private static readonly GUIContent _baseMapStandardContent = new(
            "Base Map",
            "Base color texture for the surface. Its alpha channel is used by alpha clipping when cutout is enabled.");

        private static readonly GUIContent _baseMapConductorContent = new(
            "Reflectance Map",
            "Fallback reflectance texture for conductor authoring. It is used when measured eta/k is not provided and still drives alpha clipping.");

        private static readonly GUIContent _baseMapDielectricContent = new(
            "Tint Map",
            "Transmission tint texture for dielectric models. RGB tints transmitted light, alpha only participates in alpha clipping.");

        private static readonly GUIContent _baseColorStandardContent = new(
            "Base Color",
            "Overall albedo tint for Standard and Diffuse models.");

        private static readonly GUIContent _baseColorConductorContent = new(
            "Reflectance Color",
            "Fallback conductor reflectance tint. It remains useful when eta/k is left at zero.");

        private static readonly GUIContent _baseColorDielectricContent = new(
            "Tint Color",
            "Overall transmission tint for dielectric models.");

        private static readonly GUIContent _materialModelContent = new(
            "Material Model",
            "Selects which BSDF path the path tracer will use for this material.");

        private static readonly GUIContent _surfaceMapStandardContent = new(
            "Material Map",
            "In Standard mode, R multiplies metallic and G multiplies roughness.");

        private static readonly GUIContent _surfaceMapConductorContent = new(
            "Surface Map",
            "In Conductor mode, G multiplies roughness. The R channel is currently reserved.");

        private static readonly GUIContent _surfaceMapDielectricContent = new(
            "Transmission Map",
            "In Dielectric modes, R multiplies transmission and G multiplies roughness.");

        private static readonly GUIContent _metallicContent = new(
            "Metallic",
            "Blends Standard mode between dielectric-like diffuse response and metallic reflection.");

        private static readonly GUIContent _roughnessContent = new(
            "Roughness",
            "Microfacet roughness. Zero is effectively smooth, one is fully rough.");

        private static readonly GUIContent _anisotropyContent = new(
            "Anisotropy",
            "Stretches GGX roughness along tangent or bitangent. Positive values favor tangent, negative values favor bitangent.");

        private static readonly GUIContent _iorContent = new(
            "IOR",
            "Index of refraction for the dielectric specular interface. In Standard mode this affects the non-metallic portion; in Dielectric modes it also affects transmission.");

        private static readonly GUIContent _transparencyContent = new(
            "Transmission",
            "Overall transmission multiplier for dielectric models before the texture mask is applied.");

        private static readonly GUIContent _etaContent = new(
            "Eta",
            "Measured conductor eta spectrum. Leave K at zero to fall back to artistic reflectance tint authoring.");

        private static readonly GUIContent _content = new(
            "K",
            "Measured conductor absorption spectrum. Any non-zero value enables the physical eta/k path.");

        private static readonly GUIContent _conductorEdgeTintContent = new(
            "F82 Edge Tint",
            "Near-grazing conductor tint used by the F82 Fresnel model. White preserves the standard Schlick edge response.");

        private static readonly GUIContent _clearcoatContent = new(
            "Clearcoat",
            "Strength of the dielectric coating layer above the base BSDF.");

        private static readonly GUIContent _clearcoatRoughnessContent = new(
            "Clearcoat Roughness",
            "Isotropic GGX roughness for the coating layer.");

        private static readonly GUIContent _clearcoatIorContent = new(
            "Clearcoat IOR",
            "Index of refraction for the coating layer.");

        private static readonly GUIContent _subsurfaceWeightContent = new(
            "Subsurface Weight",
            "OpenPBR mix weight between the opaque diffuse base and the subsurface dielectric base. 1.0 = diffuse base is replaced by SSS for the non-metallic portion. Requires pipeline Subsurface Scattering enabled.");

        private static readonly GUIContent _subsurfaceColorContent = new(
            "Subsurface Color",
            "User-facing surface tint (Chiang 2016 reparameterization → single-scattering albedo). White = maximum scattering (skin-like), darker = more absorption.");

        private static readonly GUIContent _subsurfaceRadiusContent = new(
            "Subsurface Radius",
            "Per-channel scattering radius in millimeters. The shader converts this to Unity scene units using HDRP's metric convention (1 unit = 1 meter). Skin ~ (3.67, 1.37, 0.68), marble ~ (8.5, 5.4, 4.0), milk ~ (3.5, 5.8, 14.4).");

        private static readonly GUIContent _subsurfaceScaleContent = new(
            "Subsurface Scale",
            "Uniform multiplier on the radius. Useful for object-scale tweaks without re-editing per-channel radius.");

        private static readonly GUIContent _subsurfaceAnisotropyContent = new(
            "Subsurface Anisotropy",
            "Henyey-Greenstein g parameter. 0 = isotropic, > 0 forward-scattering (typical for milk/marble), < 0 back-scattering.");

        private static readonly GUIContent _bumpMapContent = new(
            "Normal Map",
            "Tangent-space normal map. It only modifies shading geometry and does not alter geometric normals.");

        private static readonly GUIContent _bumpScaleContent = new(
            "Normal Scale",
            "Scales tangent-space normal deviation before it is transformed into the shading frame.");

        private static readonly GUIContent _emissionColorContent = new(
            "Emission",
            "Self-emissive radiance tint. Supported on Standard, Diffuse and Conductor models.");

        private static readonly GUIContent _alphaClipContent = new(
            "Alpha Clip",
            "Performs binary cutout from the base map alpha at intersection time.");

        private static readonly GUIContent _cutoffContent = new(
            "Cutoff",
            "Alpha threshold used when alpha clip is enabled.");

        private static readonly GUIContent _cullContent = new(
            "Cull",
            "Front/back face culling mode for the viewport raster preview.");

        private static readonly GUIContent _emissionMapPreviewContent = new(
            "Emission Map",
            "Preview-only raster fallback texture. It does not currently feed the path tracer material data.");

        private static readonly GUIContent _occlusionMapPreviewContent = new(
            "Occlusion Map",
            "Preview-only raster fallback occlusion texture. It does not currently feed the path tracer material data.");

        private static readonly GUIContent _occlusionStrengthPreviewContent = new(
            "Occlusion Strength",
            "Preview-only multiplier for the raster fallback occlusion map.");

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            _materialEditor = materialEditor;

            CacheProperties(properties);
            EnsureStyles();
            EnsureUiComposition();

            var model = GetModelKind();

            _uiContext.Update(materialEditor, properties, model);

            _chrome.DrawVisibleBackground();
            DrawHeader(model);

            for (var i = 0; i < _sections.Count; i++)
            {
                var section = _sections[i];

                DrawSection(section);
            }
        }

        internal void DrawModelSection(MaterialModelKind model)
        {
            if (_materialModel != null)
            {
                DrawShaderProperty(_materialModel, _materialModelContent);
            }

            if (model == MaterialModelKind.Mixed)
            {
                EditorGUILayout.HelpBox(
                    "Multiple materials with different models are selected. The inspector shows the union of relevant parameters.",
                    MessageType.Info);
            }
            else
            {
                LabelNote(GetModelNote(model));
            }
        }

        internal void DrawBaseSection(MaterialModelKind model)
        {
            var mapContent = GetBaseMapContent(model);
            var colorContent = GetBaseColorContent(model);

            if (_baseMap != null)
            {
                if (_baseColor != null)
                {
                    _chrome.DrawTexturePropertySingleLine(_materialEditor, mapContent, _baseMap, _baseColor);
                }
                else
                {
                    _chrome.DrawTexturePropertySingleLine(_materialEditor, mapContent, _baseMap);
                }
            }
            else if (_baseColor != null)
            {
                DrawShaderProperty(_baseColor, colorContent);
            }

            if (_baseMap != null)
            {
                _materialEditor.TextureScaleOffsetProperty(_baseMap);
            }

            if (model == MaterialModelKind.Dielectric || model == MaterialModelKind.ThinDielectric)
            {
                LabelNote("RGB tint controls transmission color. Alpha only matters when alpha clipping is enabled.");
            }
            else if (model == MaterialModelKind.Conductor)
            {
                LabelNote("Reflectance tint is used as the artistic fallback when measured eta/k is not provided.");
            }
        }

        internal void DrawScatteringSection(MaterialModelKind model)
        {
            if (ShouldShowSurfaceMap(model) && _metallicRoughnessMap != null)
            {
                _chrome.DrawTexturePropertySingleLine(_materialEditor, GetSurfaceMapContent(model), _metallicRoughnessMap);
            }

            if (ShouldShowMetallic(model) && _metallic != null)
            {
                DrawShaderProperty(_metallic, _metallicContent);
            }

            if (ShouldShowRoughness(model) && _roughness != null)
            {
                DrawShaderProperty(_roughness, _roughnessContent);
            }

            if (ShouldShowAnisotropy(model) && _anisotropy != null)
            {
                DrawShaderProperty(_anisotropy, _anisotropyContent);
            }

            if (ShouldShowIor(model) && _ior != null)
            {
                DrawShaderProperty(_ior, _iorContent);
            }

            if (ShouldShowTransparency(model) && _transparency != null)
            {
                DrawShaderProperty(_transparency, _transparencyContent);
            }

            if (ShouldShowConductorParameters(model))
            {
                if (_eta != null)
                {
                    DrawShaderProperty(_eta, _etaContent);
                }

                if (_k != null)
                {
                    DrawShaderProperty(_k, _content);
                }

                if (_conductorEdgeTint != null)
                {
                    DrawShaderProperty(_conductorEdgeTint, _conductorEdgeTintContent);
                }

                if (ShouldShowConductorFallbackHint())
                {
                    EditorGUILayout.HelpBox(
                        "K is zero, so the backend falls back to artistic conductor tint from Base Color / Base Map.",
                        MessageType.Info);
                }
            }

            if (_clearcoat != null)
            {
                DrawShaderProperty(_clearcoat, _clearcoatContent);
            }

            if (IsClearcoatEnabled())
            {
                EditorGUI.indentLevel++;

                if (_clearcoatRoughness != null)
                {
                    DrawShaderProperty(_clearcoatRoughness, _clearcoatRoughnessContent);
                }

                if (_clearcoatIor != null)
                {
                    DrawShaderProperty(_clearcoatIor, _clearcoatIorContent);
                }

                EditorGUI.indentLevel--;
            }

            if (_subsurfaceWeight != null)
            {
                DrawShaderProperty(_subsurfaceWeight, _subsurfaceWeightContent);
            }

            if (IsSubsurfaceEnabled())
            {
                EditorGUI.indentLevel++;

                if (_subsurfaceColor != null)
                {
                    DrawShaderProperty(_subsurfaceColor, _subsurfaceColorContent);
                }

                if (_subsurfaceRadius != null)
                {
                    DrawShaderProperty(_subsurfaceRadius, _subsurfaceRadiusContent);
                }

                if (_subsurfaceScale != null)
                {
                    DrawShaderProperty(_subsurfaceScale, _subsurfaceScaleContent);
                }

                if (_subsurfaceAnisotropy != null)
                {
                    DrawShaderProperty(_subsurfaceAnisotropy, _subsurfaceAnisotropyContent);
                }

                LabelNote("Subsurface only fires if pipeline Subsurface Scattering is enabled.");
                EditorGUI.indentLevel--;
            }

            if (model == MaterialModelKind.Diffuse)
            {
                LabelNote("Diffuse mode removes glossy and specular response from the base layer. Clearcoat can still add a top-layer reflection.");
            }
            else if (model == MaterialModelKind.Standard)
            {
                LabelNote("Standard mode uses metallic workflow: R drives metalness and G drives roughness.");
            }
            else if (model == MaterialModelKind.Conductor)
            {
                LabelNote("Conductor mode uses physical eta/k when K is non-zero. Otherwise it falls back to the reflectance tint.");
            }
            else if (model == MaterialModelKind.Dielectric)
            {
                LabelNote("Dielectric mode is a thick refractive interface with IOR, transmission mask and GGX roughness.");
            }
            else if (model == MaterialModelKind.ThinDielectric)
            {
                LabelNote("Thin dielectric is intended for sheets and panes without interior thickness.");
            }
        }

        internal void DrawNormalSection()
        {
            if (_bumpMap != null)
            {
                _chrome.DrawTexturePropertySingleLine(_materialEditor, _bumpMapContent, _bumpMap);
            }

            if (_bumpScale != null)
            {
                DrawShaderProperty(_bumpScale, _bumpScaleContent);
            }

            LabelNote("Normal mapping only affects the shading frame. Geometric normals remain unchanged for tracing and ray offsets.");
        }

        internal void DrawEmissionSection(MaterialModelKind model)
        {
            if (model == MaterialModelKind.Dielectric || model == MaterialModelKind.ThinDielectric)
            {
                EditorGUILayout.HelpBox("Emission is currently ignored for dielectric models in this pipeline.", MessageType.None);
            }
            else if (_emissionColor != null)
            {
                DrawShaderProperty(_emissionColor, _emissionColorContent);
            }
        }

        internal void DrawOpacitySection(MaterialModelKind model)
        {
            if (_alphaClip != null)
            {
                DrawShaderProperty(_alphaClip, _alphaClipContent);
            }

            if (IsAlphaClipEnabled())
            {
                EditorGUI.indentLevel++;

                if (_cutoff != null)
                {
                    DrawShaderProperty(_cutoff, _cutoffContent);
                }

                EditorGUI.indentLevel--;
            }

            if (model == MaterialModelKind.Dielectric || model == MaterialModelKind.ThinDielectric)
            {
                LabelNote("For dielectric models, alpha does not dim transmission. It only controls cutout when alpha clip is enabled.");
            }
        }

        internal void DrawPreviewSection()
        {
            EditorGUILayout.HelpBox(
                "These slots are only used by the shader's raster fallback path. The path tracer currently ignores them.",
                MessageType.Info);

            if (_emissionMap != null)
            {
                _chrome.DrawTexturePropertySingleLine(_materialEditor, _emissionMapPreviewContent, _emissionMap);
            }

            if (_occlusionMap != null)
            {
                _chrome.DrawTexturePropertySingleLine(_materialEditor, _occlusionMapPreviewContent, _occlusionMap);
            }

            if (_occlusionStrength != null)
            {
                DrawShaderProperty(_occlusionStrength, _occlusionStrengthPreviewContent);
            }
        }

        internal void DrawAdvancedSection()
        {
            DrawShaderProperty(_cull, _cullContent);
        }

        internal bool HasPreviewSection()
        {
            return _emissionMap != null || _occlusionMap != null || _occlusionStrength != null;
        }

        internal bool HasAdvancedSection()
        {
            return _cull != null;
        }

        private void EnsureUiComposition()
        {
            _uiContext ??= new MaterialEditorContext(this);
            _sections ??= CreateSections();
        }

        private IReadOnlyList<MaterialEditorSection> CreateSections()
        {
            return new MaterialEditorSection[]
            {
            new MaterialEditorSection(
                "Path Tracing Render",
                "Controls the BSDF the path tracer uses for surface response, scattering, emission, and cutout.",
                () => _isPathTracingRenderShown,
                value => _isPathTracingRenderShown = value,
                new EditorCardBase<MaterialEditorContext>[]
                {
                    new MaterialEditorCard(
                        "Model",
                        "Chooses the BRDF or BSDF model and exposes only the inputs that model actually uses.",
                        context => context.Editor.DrawModelSection(context.Model)),
                    new MaterialEditorCard(
                        "Surface",
                        "Defines the base color and texture inputs the shader samples before lighting is evaluated.",
                        context => context.Editor.DrawBaseSection(context.Model)),
                    new MaterialEditorCard(
                        "Scattering",
                        "Shapes how light reflects, refracts, spreads, or transmits through the surface.",
                        context => context.Editor.DrawScatteringSection(context.Model)),
                    new MaterialEditorCard(
                        "Normal",
                        "Perturbs the shading normal used for lighting without changing the mesh silhouette.",
                        context => context.Editor.DrawNormalSection()),
                    new MaterialEditorCard(
                        "Emission",
                        "Makes the material emit its own light and adds self-lit color to the final frame.",
                        context => context.Editor.DrawEmissionSection(context.Model)),
                    new MaterialEditorCard(
                        "Opacity",
                        "Cuts pixels out of the surface when alpha falls below the clip threshold.",
                        context => context.Editor.DrawOpacitySection(context.Model))
                }),
            new MaterialEditorSection(
                "Raster Render",
                "Controls used only by the raster fallback and viewport preview, not by traced lighting.",
                () => _isRasterRenderShown,
                value => _isRasterRenderShown = value,
                new EditorCardBase<MaterialEditorContext>[]
                {
                    new MaterialEditorCard(
                        "Viewport Preview",
                        "Raster-only preview controls for Unity's non-path-traced view of the material.",
                        context => context.Editor.DrawPreviewSection()),
                    new MaterialEditorCard(
                        "Advanced",
                        "Low-level render flags that affect culling and how the material is previewed.",
                        context => context.Editor.DrawAdvancedSection())
                },
                context => context.Editor.HasPreviewSection() || context.Editor.HasAdvancedSection())
            };
        }

        private void DrawSection(MaterialEditorSection section)
        {
            if (section == null || _uiContext == null || _uiContext.Editor == null)
            {
                return;
            }

            section.DrawIMGUI(_uiContext, DrawSectionShell, DrawCardShell);
        }

        private void DrawSectionShell(EditorSectionBase<MaterialEditorContext> section, System.Action drawContent)
        {
            var visible = section.GetExpanded();

            _chrome.DrawSectionShell(section.Title, section.Hint, visible, () => section.SetExpanded(visible == false), drawContent);
        }

        private void DrawCardShell(EditorCardBase<MaterialEditorContext> card)
        {
            if (card == null)
            {
                return;
            }

            _chrome.DrawCard(card.Title, card.Hint, card.ImguiPadding, () => _chrome.DrawWithPipelineFieldStyling(() => card.DrawIMGUI(_uiContext)));
        }

        private void CacheProperties(MaterialProperty[] properties)
        {
            _baseMap = FindProperty("_BaseMap", properties, false);
            _baseColor = FindProperty("_BaseColor", properties, false);
            _metallic = FindProperty("_Metallic", properties, false);
            _roughness = FindProperty("_Roughness", properties, false);
            _anisotropy = FindProperty("_Anisotropy", properties, false);
            _metallicRoughnessMap = FindProperty("_MetallicRoughnessMap", properties, false);
            _materialModel = FindProperty("_MaterialModel", properties, false);
            _ior = FindProperty("_IOR", properties, false);
            _transparency = FindProperty("_Transparency", properties, false);
            _eta = FindProperty("_Eta", properties, false);
            _k = FindProperty("_K", properties, false);
            _conductorEdgeTint = FindProperty("_ConductorEdgeTint", properties, false);
            _clearcoat = FindProperty("_Clearcoat", properties, false);
            _clearcoatRoughness = FindProperty("_ClearcoatRoughness", properties, false);
            _clearcoatIor = FindProperty("_ClearcoatIOR", properties, false);
            _subsurfaceWeight = FindProperty("_SubsurfaceWeight", properties, false);
            _subsurfaceColor = FindProperty("_SubsurfaceColor", properties, false);
            _subsurfaceRadius = FindProperty("_SubsurfaceRadius", properties, false);
            _subsurfaceScale = FindProperty("_SubsurfaceScale", properties, false);
            _subsurfaceAnisotropy = FindProperty("_SubsurfaceAnisotropy", properties, false);
            _bumpMap = FindProperty("_BumpMap", properties, false);
            _bumpScale = FindProperty("_BumpScale", properties, false);
            _emissionColor = FindProperty("_EmissionColor", properties, false);
            _emissionMap = FindProperty("_EmissionMap", properties, false);
            _occlusionMap = FindProperty("_OcclusionMap", properties, false);
            _occlusionStrength = FindProperty("_OcclusionStrength", properties, false);
            _alphaClip = FindProperty("_AlphaClip", properties, false);
            _cutoff = FindProperty("_Cutoff", properties, false);
            _cull = FindProperty("_Cull", properties, false);
        }

        private void EnsureStyles()
        {
            _chrome ??= new EditorChrome(useSdfCardRounding: true);

            _chrome.EnsureStyles();
        }

        private void DrawHeader(MaterialModelKind model)
        {
            _chrome.DrawFullWidthHeader(GetHeaderTitle(), GetHeaderSubtitle(model));
        }

        private void DrawShaderProperty(MaterialProperty property, GUIContent content)
        {
            if (property == null)
            {
                return;
            }

            if (property.propertyType != UnityEngine.Rendering.ShaderPropertyType.Range)
            {
                _materialEditor.ShaderProperty(property, content);

                return;
            }

            DrawRangeProperty(property, content);
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

        private void LabelNote(string text)
        {
            _chrome.DrawNote(text);
        }

        private string GetHeaderTitle()
        {
            if (_materialEditor != null && _materialEditor.targets != null && _materialEditor.targets.Length > 1)
            {
                return "Path Tracing Materials";
            }

            var material = _materialEditor?.target as Material;

            if (material != null && material.shader != null)
            {
                return material.shader.name;
            }

            return "Path Tracing Material";
        }

        private string GetHeaderSubtitle(MaterialModelKind model)
        {
            if (_materialEditor != null && _materialEditor.targets != null && _materialEditor.targets.Length > 1)
            {
                return "Defines the shared surface response of the selected materials in tracing and raster preview.";
            }

            return GetBannerBody(model);
        }

        private MaterialModelKind GetModelKind()
        {
            if (_materialModel == null)
            {
                return MaterialModelKind.Standard;
            }

            if (_materialModel.hasMixedValue)
            {
                return MaterialModelKind.Mixed;
            }

            var rounded = Mathf.RoundToInt(_materialModel.floatValue);

            return rounded switch
            {
                1 => MaterialModelKind.Diffuse,
                2 => MaterialModelKind.Conductor,
                3 => MaterialModelKind.Dielectric,
                4 => MaterialModelKind.ThinDielectric,
                _ => MaterialModelKind.Standard
            };
        }

        private bool IsAlphaClipEnabled()
        {
            return _alphaClip != null && (_alphaClip.hasMixedValue == false && _alphaClip.floatValue > .5f);
        }

        private bool IsClearcoatEnabled()
        {
            return _clearcoat != null && (_clearcoat.hasMixedValue || _clearcoat.floatValue > 1e-4f);
        }

        private bool IsSubsurfaceEnabled()
        {
            return _subsurfaceWeight != null && (_subsurfaceWeight.hasMixedValue || _subsurfaceWeight.floatValue > 1e-4f);
        }

        private bool ShouldShowConductorFallbackHint()
        {
            return _k != null && _k.hasMixedValue == false && MaxRgb(_k.colorValue) <= 1e-5f;
        }

        private static float MaxRgb(Color color)
        {
            return Mathf.Max(color.r, Mathf.Max(color.g, color.b));
        }

        private static bool ShouldShowSurfaceMap(MaterialModelKind model)
        {
            return model != MaterialModelKind.Diffuse;
        }

        private static bool ShouldShowMetallic(MaterialModelKind model)
        {
            return model == MaterialModelKind.Standard || model == MaterialModelKind.Mixed;
        }

        private static bool ShouldShowRoughness(MaterialModelKind model)
        {
            return model != MaterialModelKind.Diffuse;
        }

        private static bool ShouldShowAnisotropy(MaterialModelKind model)
        {
            return model != MaterialModelKind.Diffuse;
        }

        private static bool ShouldShowIor(MaterialModelKind model)
        {
            return model == MaterialModelKind.Standard || model == MaterialModelKind.Dielectric || model == MaterialModelKind.ThinDielectric || model == MaterialModelKind.Mixed;
        }

        private static bool ShouldShowTransparency(MaterialModelKind model)
        {
            return model == MaterialModelKind.Dielectric || model == MaterialModelKind.ThinDielectric || model == MaterialModelKind.Mixed;
        }

        private static bool ShouldShowConductorParameters(MaterialModelKind model)
        {
            return model == MaterialModelKind.Conductor || model == MaterialModelKind.Mixed;
        }

        private static GUIContent GetBaseMapContent(MaterialModelKind model)
        {
            return model switch
            {
                MaterialModelKind.Conductor => _baseMapConductorContent,
                MaterialModelKind.Dielectric => _baseMapDielectricContent,
                MaterialModelKind.ThinDielectric => _baseMapDielectricContent,
                _ => _baseMapStandardContent
            };
        }

        private static GUIContent GetBaseColorContent(MaterialModelKind model)
        {
            return model switch
            {
                MaterialModelKind.Conductor => _baseColorConductorContent,
                MaterialModelKind.Dielectric => _baseColorDielectricContent,
                MaterialModelKind.ThinDielectric => _baseColorDielectricContent,
                _ => _baseColorStandardContent
            };
        }

        private static GUIContent GetSurfaceMapContent(MaterialModelKind model)
        {
            return model switch
            {
                MaterialModelKind.Conductor => _surfaceMapConductorContent,
                MaterialModelKind.Dielectric => _surfaceMapDielectricContent,
                MaterialModelKind.ThinDielectric => _surfaceMapDielectricContent,
                _ => _surfaceMapStandardContent
            };
        }

        private static string GetBannerBody(MaterialModelKind model)
        {
            return model switch
            {
                MaterialModelKind.Diffuse => "Pure diffuse surface for matte materials without glossy reflection.",
                MaterialModelKind.Conductor => "Metal surface with measured eta/k or an artistic reflectance fallback.",
                MaterialModelKind.Dielectric => "Volumetric refractive surface for solid transparent materials.",
                MaterialModelKind.ThinDielectric => "Thin refractive surface for panes, sheets, and films.",
                MaterialModelKind.Mixed => "Shared controls for materials using different traced surface models.",
                _ => "General-purpose opaque surface based on the metallic workflow."
            };
        }

        private static string GetModelNote(MaterialModelKind model)
        {
            return model switch
            {
                MaterialModelKind.Diffuse => "Diffuse removes the glossy/specular branch entirely, so the inspector hides controls that would not affect traced scattering.",
                MaterialModelKind.Conductor => "Conductor exposes both artistic fallback reflectance and physical eta/k. The editor keeps both visible so the transition is explicit.",
                MaterialModelKind.Dielectric => "Dielectric reinterprets the material map as transmission and roughness. IOR controls the interface while transmission controls the amount of transmitted light.",
                MaterialModelKind.ThinDielectric => "Thin dielectric uses the same interface controls as dielectric, but the transport model assumes a zero-thickness shell.",
                MaterialModelKind.Mixed => "Model-specific controls are shown together because the current selection does not share one BSDF model.",
                _ => "Standard is the default opaque OpenPBR-style metallic workflow: IOR controls the dielectric specular interface and Metallic blends toward conductor response."
            };
        }
    }
}
