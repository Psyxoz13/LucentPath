using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LucentPath.EditorUi.Materials
{
    internal static class LucentPathOpenPbrMaterialMigration
    {
        private const string OpenPbrShaderName = "Path Tracing RP/Default";

        private static readonly string[] _baseMapNames =
        {
            "_BaseMap", "_MainTex", "_BaseColorMap", "_BaseColorTexture", "_AlbedoMap", "_DiffuseMap", "_Diffuse"
        };

        private static readonly string[] _baseColorNames =
        {
            "_BaseColor", "_Color", "_BaseTint", "_TintColor", "_DiffuseColor", "_AlbedoColor"
        };

        private static readonly string[] _materialMapNames =
        {
            "_MetallicRoughnessMap", "_MetallicGlossMap", "_MaskMap", "_MetallicMap", "_MetalnessMap", "_SpecGlossMap", "_RoughnessMap"
        };

        private static readonly string[] _normalMapNames =
        {
            "_BumpMap", "_NormalMap", "_NormalTex", "_Normal", "_Bump", "_MainNormalMap"
        };

        private static readonly string[] _normalScaleNames =
        {
            "_BumpScale", "_NormalScale", "_NormalStrength", "_BumpStrength"
        };

        private static readonly string[] _emissionMapNames =
        {
            "_EmissionMap", "_EmissiveColorMap", "_EmissiveMap", "_EmissionTexture"
        };

        private static readonly string[] _emissionColorNames =
        {
            "_EmissionColor", "_EmissiveColor"
        };

        private static readonly string[] _metallicNames =
        {
            "_Metallic", "_Metalness", "_OpenPbrBaseMetalness"
        };

        private static readonly string[] _roughnessNames =
        {
            "_Roughness", "_OpenPbrSpecularRoughness", "_OpenPbrBaseDiffuseRoughness"
        };

        private static readonly string[] _smoothnessNames =
        {
            "_Glossiness", "_Smoothness", "_GlossMapScale", "_SmoothnessRemapMax"
        };

        private enum SourceMaterialKind
        {
            Legacy,
            UniversalRenderPipeline,
            HighDefinitionRenderPipeline,
            LucentPath
        }

        private class SourceMaterial
        {
            public SourceMaterialKind SourceKind;
            public Texture BaseMap;
            public Vector2 BaseMapScale;
            public Vector2 BaseMapOffset;
            public Color BaseColor;
            public Texture MaterialMap;
            public float Metallic;
            public float Roughness;
            public float SpecularWeight;
            public Color SpecularColor;
            public float Ior;
            public float Anisotropy;
            public float TransmissionWeight;
            public Color TransmissionColor;
            public float Opacity;
            public bool IsThinWalled;
            public float CoatWeight;
            public Color CoatColor;
            public float CoatRoughness;
            public float CoatIor;
            public Texture EmissionMap;
            public Vector2 EmissionMapScale;
            public Vector2 EmissionMapOffset;
            public float EmissionLuminance;
            public Color EmissionColor;
            public Texture NormalMap;
            public float NormalScale;
            public bool IsAlphaClip;
            public float Cutoff;
            public float Cull;
            public int RenderQueue;
            public bool IsInstancingEnabled;
            public bool IsDoubleSidedGI;

            public static SourceMaterial Read(Material material)
            {
                var source = new SourceMaterial
                {
                    SourceKind = GetSourceKind(material),
                    BaseMap = GetTexture(material, _baseMapNames),
                    BaseColor = GetColor(material, Color.white, _baseColorNames),
                    MaterialMap = GetTexture(material, _materialMapNames),
                    Metallic = Clamp01Finite(GetFloat(material, 0f, _metallicNames), 0f),
                    SpecularWeight = Clamp01Finite(GetFloat(material, 1f, "_SpecularHighlights", "_OpenPbrSpecularWeight"), 1f),
                    SpecularColor = GetColor(material, Color.white, "_SpecColor", "_SpecularColor", "_OpenPbrSpecularColor"),
                    Ior = Mathf.Clamp(GetFloat(material, 1.5f, "_IOR", "_Ior", "_OpenPbrSpecularIOR"), 1f, 2.5f),
                    Anisotropy = Clamp01Finite(GetFloat(material, 0f, "_Anisotropy", "_OpenPbrSpecularRoughnessAnisotropy"), 0f),
                    TransmissionWeight = Clamp01Finite(
                        GetFloat(material, 0f, "_Transmission", "_TransmissionWeight", "_OpenPbrTransmissionWeight", "_Transparency"),
                        0f),
                    TransmissionColor = GetColor(material, Color.white, "_TransmissionColor", "_OpenPbrTransmissionColor"),
                    CoatWeight = Clamp01Finite(GetFloat(material, 0f, "_Clearcoat", "_CoatMask", "_ClearCoatMask", "_OpenPbrCoatWeight"), 0f),
                    CoatColor = GetColor(material, Color.white, "_CoatColor", "_OpenPbrCoatColor"),
                    CoatRoughness = ResolveCoatRoughness(material),
                    CoatIor = Mathf.Clamp(GetFloat(material, 1.6f, "_ClearcoatIOR", "_OpenPbrCoatIOR"), 1f, 2.5f),
                    EmissionMap = GetTexture(material, _emissionMapNames),
                    NormalMap = GetTexture(material, _normalMapNames),
                    NormalScale = Mathf.Max(0f, GetFloat(material, 1f, _normalScaleNames)),
                    Cutoff = Clamp01Finite(GetFloat(material, .5f, "_Cutoff", "_AlphaCutoff", "_AlphaClipThreshold"), .5f),
                    Cull = GetFloat(material, 2f, "_Cull", "_CullMode", "_CullModeForward"),
                    RenderQueue = material.renderQueue,
                    IsInstancingEnabled = material.enableInstancing,
                    IsDoubleSidedGI = material.doubleSidedGI
                };

                TryGetTextureTransform(material, out source.BaseMapScale, out source.BaseMapOffset, _baseMapNames);
                TryGetTextureTransform(material, out source.EmissionMapScale, out source.EmissionMapOffset, _emissionMapNames);

                source.Roughness = ResolveRoughness(material);
                source.Opacity = ResolveOpacity(material, source.BaseColor);
                source.IsThinWalled = source.Opacity < .999f || GetFloat(material, 0f, "_OpenPbrGeometryThinWalled") > .5f;
                source.IsAlphaClip = ResolveAlphaClip(material, source);

                ResolveEmission(material, source);

                return source;
            }

            private static float ResolveRoughness(Material material)
            {
                if (HasAny(material, _roughnessNames))
                {
                    return Clamp01Finite(GetFloat(material, .5f, _roughnessNames), .5f);
                }

                float smoothness = Clamp01Finite(GetFloat(material, .5f, _smoothnessNames), .5f);

                return 1f - smoothness;
            }

            private static float ResolveCoatRoughness(Material material)
            {
                if (material.HasProperty("_ClearcoatRoughness"))
                {
                    return Clamp01Finite(material.GetFloat("_ClearcoatRoughness"), .1f);
                }

                if (material.HasProperty("_ClearCoatSmoothness"))
                {
                    return 1f - Clamp01Finite(material.GetFloat("_ClearCoatSmoothness"), .9f);
                }

                return Clamp01Finite(GetFloat(material, .1f, "_OpenPbrCoatRoughness"), .1f);
            }

            private static float ResolveOpacity(Material material, Color baseColor)
            {
                float opacity = Clamp01Finite(GetFloat(material, baseColor.a, "_OpenPbrGeometryOpacity", "_Opacity", "_Alpha"), baseColor.a);

                if (material.HasProperty("_Surface") && Mathf.RoundToInt(material.GetFloat("_Surface")) == 0)
                {
                    opacity = 1f;
                }

                if (material.HasProperty("_Mode") && Mathf.RoundToInt(material.GetFloat("_Mode")) == 0)
                {
                    opacity = 1f;
                }

                return opacity;
            }

            private static bool ResolveAlphaClip(Material material, SourceMaterial source)
            {
                if (GetFloat(material, 0f, "_AlphaClip", "_AlphaTest", "_EnableAlphaClip") > .5f)
                {
                    return true;
                }

                if (material.IsKeywordEnabled("_ALPHATEST_ON"))
                {
                    return true;
                }

                return source.Cutoff > 0f && source.Cutoff < .999f && source.Opacity < .999f;
            }

            private static void ResolveEmission(Material material, SourceMaterial source)
            {
                Color emission = GetColor(material, Color.black, _emissionColorNames);
                float intensity = Mathf.Max(emission.r, emission.g, emission.b);

                if (source.EmissionMap != null && intensity <= 1e-5f)
                {
                    intensity = 1f;
                }

                source.EmissionLuminance = Mathf.Max(0f, intensity);
                source.EmissionColor = intensity > 1e-5f ? emission * (1f / intensity) : Color.white;
                source.EmissionColor.a = emission.a;
            }
        }

        [MenuItem("Lucent Path/Materials/Migrate Selected Legacy Materials To OpenPBR", true)]
        private static bool CanMigrateSelected()
        {
            return CollectSelectedMaterials().Count > 0;
        }

        [MenuItem("Lucent Path/Materials/Migrate Selected Legacy Materials To OpenPBR")]
        private static void MigrateSelected()
        {
            var shader = Shader.Find(OpenPbrShaderName);

            if (shader == null)
            {
                Debug.LogError($"Cannot migrate materials: shader '{OpenPbrShaderName}' was not found.");

                return;
            }

            var materials = CollectSelectedMaterials();
            int migrated = 0;

            for (var i = 0; i < materials.Count; i++)
            {
                var material = materials[i];

                if (material == null)
                {
                    continue;
                }

                MigrateMaterial(material, shader);
                migrated++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Migrated {migrated} material(s) to '{OpenPbrShaderName}'.");
        }

        private static List<Material> CollectSelectedMaterials()
        {
            var result = new List<Material>();
            var seen = new HashSet<Material>();

            var materials = Selection.GetFiltered<Material>(SelectionMode.Editable | SelectionMode.DeepAssets);

            for (var i = 0; i < materials.Length; i++)
            {
                var material = materials[i];

                AddMaterial(material, seen, result);
            }

            var renderers = Selection.GetFiltered<Renderer>(SelectionMode.Editable | SelectionMode.Deep);

            for (var j = 0; j < renderers.Length; j++)
            {
                var renderer = renderers[j];

                if (renderer == null)
                {
                    continue;
                }

                var materialItems = renderer.sharedMaterials;

                for (var k = 0; k < materialItems.Length; k++)
                {
                    var material = materialItems[k];

                    AddMaterial(material, seen, result);
                }
            }

            return result;
        }

        private static void AddMaterial(Material material, HashSet<Material> seen, List<Material> result)
        {
            if (material == null || EditorUtility.IsPersistent(material) == false || seen.Add(material) == false)
            {
                return;
            }

            result.Add(material);
        }

        private static void MigrateMaterial(Material material, Shader openPbrShader)
        {
            Undo.RecordObject(material, "Migrate Material To OpenPBR");

            var source = SourceMaterial.Read(material);

            material.shader = openPbrShader;

            SetTexture(material, "_BaseMap", source.BaseMap);
            SetTextureScaleOffset(material, "_BaseMap", source.BaseMapScale, source.BaseMapOffset);
            SetColor(material, "_BaseColor", source.BaseColor);

            SetTexture(material, "_MetallicRoughnessMap", source.MaterialMap);
            SetFloat(material, "_OpenPbrBaseWeight", 1f);
            SetFloat(material, "_OpenPbrBaseMetalness", source.Metallic);
            SetFloat(material, "_OpenPbrBaseDiffuseRoughness", source.Roughness);
            SetFloat(material, "_OpenPbrSpecularWeight", source.SpecularWeight);
            SetColor(material, "_OpenPbrSpecularColor", source.SpecularColor);
            SetFloat(material, "_OpenPbrSpecularRoughness", source.Roughness);
            SetFloat(material, "_OpenPbrSpecularIOR", source.Ior);
            SetFloat(material, "_OpenPbrSpecularRoughnessAnisotropy", source.Anisotropy);

            SetFloat(material, "_OpenPbrTransmissionWeight", source.TransmissionWeight);
            SetColor(material, "_OpenPbrTransmissionColor", source.TransmissionColor);
            SetFloat(material, "_OpenPbrGeometryOpacity", source.Opacity);
            SetFloat(material, "_OpenPbrGeometryThinWalled", source.IsThinWalled ? 1f : 0f);

            SetFloat(material, "_OpenPbrCoatWeight", source.CoatWeight);
            SetColor(material, "_OpenPbrCoatColor", source.CoatColor);
            SetFloat(material, "_OpenPbrCoatRoughness", source.CoatRoughness);
            SetFloat(material, "_OpenPbrCoatIOR", source.CoatIor);

            SetTexture(material, "_EmissionMap", source.EmissionMap);
            SetTextureScaleOffset(material, "_EmissionMap", source.EmissionMapScale, source.EmissionMapOffset);
            SetFloat(material, "_OpenPbrEmissionLuminance", source.EmissionLuminance);
            SetColor(material, "_OpenPbrEmissionColor", source.EmissionColor);

            SetTexture(material, "_BumpMap", source.NormalMap);
            SetFloat(material, "_BumpScale", source.NormalScale);

            SetFloat(material, "_AlphaClip", source.IsAlphaClip ? 1f : 0f);
            SetFloat(material, "_Cutoff", source.Cutoff);
            SetFloat(material, "_Cull", source.Cull);

            material.enableInstancing = source.IsInstancingEnabled;
            material.doubleSidedGI = source.IsDoubleSidedGI;
            material.renderQueue = source.RenderQueue;

            EditorUtility.SetDirty(material);
        }

        private static Texture GetTexture(Material material, params string[] names)
        {
            for (var i = 0; i < names.Length; i++)
            {
                var name = names[i];

                if (material.HasProperty(name) == false)
                {
                    continue;
                }

                var texture = material.GetTexture(name);

                if (texture != null)
                {
                    return texture;
                }
            }

            return null;
        }

        private static bool TryGetTextureTransform(Material material, out Vector2 scale, out Vector2 offset, params string[] names)
        {
            for (var i = 0; i < names.Length; i++)
            {
                var name = names[i];

                if (material.HasProperty(name) == false || material.GetTexture(name) == null)
                {
                    continue;
                }

                scale = material.GetTextureScale(name);
                offset = material.GetTextureOffset(name);

                return true;
            }

            scale = Vector2.one;
            offset = Vector2.zero;

            return false;
        }

        private static Color GetColor(Material material, Color fallback, params string[] names)
        {
            for (var i = 0; i < names.Length; i++)
            {
                var name = names[i];

                if (material.HasProperty(name))
                {
                    return material.GetColor(name);
                }
            }

            return fallback;
        }

        private static float GetFloat(Material material, float fallback, params string[] names)
        {
            for (var i = 0; i < names.Length; i++)
            {
                var name = names[i];

                if (material.HasProperty(name))
                {
                    return material.GetFloat(name);
                }
            }

            return fallback;
        }

        private static bool HasAny(Material material, params string[] names)
        {
            for (var i = 0; i < names.Length; i++)
            {
                var name = names[i];

                if (material.HasProperty(name))
                {
                    return true;
                }
            }

            return false;
        }

        private static void SetTexture(Material material, string name, Texture texture)
        {
            if (material.HasProperty(name))
            {
                material.SetTexture(name, texture);
            }
        }

        private static void SetTextureScaleOffset(Material material, string name, Vector2 scale, Vector2 offset)
        {
            if (material.HasProperty(name) == false)
            {
                return;
            }

            material.SetTextureScale(name, scale);
            material.SetTextureOffset(name, offset);
        }

        private static void SetColor(Material material, string name, Color color)
        {
            if (material.HasProperty(name))
            {
                material.SetColor(name, color);
            }
        }

        private static void SetFloat(Material material, string name, float value)
        {
            if (material.HasProperty(name))
            {
                material.SetFloat(name, value);
            }
        }

        private static float Clamp01Finite(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp01(value);
        }

        private static SourceMaterialKind GetSourceKind(Material material)
        {
            string shaderName = material.shader != null ? material.shader.name : string.Empty;

            if (shaderName == OpenPbrShaderName || shaderName.Contains("Path Tracing RP"))
            {
                return SourceMaterialKind.LucentPath;
            }

            if (shaderName.Contains("Universal Render Pipeline") || shaderName.Contains("URP"))
            {
                return SourceMaterialKind.UniversalRenderPipeline;
            }

            if (shaderName.Contains("HDRP") || shaderName.Contains("High Definition Render Pipeline"))
            {
                return SourceMaterialKind.HighDefinitionRenderPipeline;
            }

            return SourceMaterialKind.Legacy;
        }
    }
}
