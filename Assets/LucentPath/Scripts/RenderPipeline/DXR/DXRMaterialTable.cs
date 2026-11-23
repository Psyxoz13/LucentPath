using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LucentPath.Rendering
{
    internal class DXRMaterialTable
    {
        private readonly Dictionary<EntityId, uint> _materialCrcs = new();
        private readonly List<Material> _materials = new();
        private ComputeBuffer _tableBuffer;
        private RenderTexture _baseAtlas;
        private RenderTexture _materialMapAtlas;
        private RenderTexture _emissionAtlas;
        private GpuMaterialRow[] _rows;

        private bool _isDirty;

        private const int AtlasMaxSize = 4096;
        private const int AtlasMinSize = 4;
        private const uint RowFlagOpenPbr = 1u;
        private const uint RowFlagExplicitConductor = 2u;
        private const uint RowFlagThinWalled = 4u;
        private const uint RowFlagRoughnessFloor = 8u;
        private const uint RowFlagComplex = 16u;

        private static readonly int _lucentMaterialIndexProperty = Shader.PropertyToID("_LucentMaterialIndex");
        private static readonly int _lucentMaterialTableProperty = Shader.PropertyToID("_LucentMaterialTable");
        private static readonly int _lucentMaterialTableCountProperty = Shader.PropertyToID("_LucentMaterialTableCount");
        private static readonly int _lucentBaseAtlasProperty = Shader.PropertyToID("_LucentBaseAtlas");
        private static readonly int _lucentMaterialMapAtlasProperty = Shader.PropertyToID("_LucentMaterialMapAtlas");
        private static readonly int _lucentEmissionAtlasProperty = Shader.PropertyToID("_LucentEmissionAtlas");

        private static readonly int _baseMapProperty = Shader.PropertyToID("_BaseMap");
        private static readonly int _emissionMapProperty = Shader.PropertyToID("_EmissionMap");
        private static readonly int _metallicRoughnessMapProperty = Shader.PropertyToID("_MetallicRoughnessMap");

        public void Update(RayTracingInstanceMaterialCRC[] materialsCrc)
        {
            _isDirty = _materials.Count == 0 || _baseAtlas == null || _materialMapAtlas == null || _emissionAtlas == null;

            if (materialsCrc != null)
            {
                for (var i = 0; i < materialsCrc.Length; i++)
                {
                    var entry = materialsCrc[i];

                    if (_materialCrcs.TryGetValue(entry.entityId, out uint crc) && crc == (uint)entry.crc)
                    {
                        continue;
                    }

                    _materialCrcs[entry.entityId] = (uint)entry.crc;

                    var material = Resources.EntityIdToObject(entry.entityId) as Material;

                    if (material == null)
                    {
                        continue;
                    }

                    if (_materials.Contains(material) == false)
                    {
                        _materials.Add(material);
                    }

                    _isDirty = true;
                }
            }

            if (_isDirty)
            {
                Rebuild();
            }

            Bind();
        }

        public void Dispose()
        {
            _tableBuffer?.Release();

            _tableBuffer = null;

            ReleaseAtlas(ref _baseAtlas);
            ReleaseAtlas(ref _materialMapAtlas);
            ReleaseAtlas(ref _emissionAtlas);
            _materialCrcs.Clear();
            _materials.Clear();
        }

        private void Rebuild()
        {
            _materials.RemoveAll(m => m == null);

            var count = Mathf.Max(_materials.Count, 1);

            _rows = new GpuMaterialRow[count];

            var baseTextures = new List<Texture>();
            var materialMapTextures = new List<Texture>();
            var emissionTextures = new List<Texture>();
            var baseLayers = new Dictionary<Texture, uint>();
            var materialMapLayers = new Dictionary<Texture, uint>();
            var emissionLayers = new Dictionary<Texture, uint>();

            for (var i = 0; i < _materials.Count; i++)
            {
                var material = _materials[i];

                material.SetInteger(_lucentMaterialIndexProperty, i);

                _rows[i] = BuildRow(
                    material,
                    ResolveLayer(material, _baseMapProperty, baseTextures, baseLayers),
                    ResolveLayer(material, _metallicRoughnessMapProperty, materialMapTextures, materialMapLayers),
                    ResolveLayer(material, _emissionMapProperty, emissionTextures, emissionLayers));
            }

            if (_materials.Count == 0)
            {
                _rows[0] = BuildDefaultRow();
            }

            if (_tableBuffer == null || _tableBuffer.count < count)
            {
                _tableBuffer?.Release();

                _tableBuffer = new ComputeBuffer(
                    count,
                    System.Runtime.InteropServices.Marshal.SizeOf<GpuMaterialRow>(),
                    ComputeBufferType.Structured);
            }

            _tableBuffer.SetData(_rows, 0, 0, count);

            BuildAtlas(ref _baseAtlas, baseTextures, true);
            BuildAtlas(ref _materialMapAtlas, materialMapTextures, false);
            BuildAtlas(ref _emissionAtlas, emissionTextures, true);

            _isDirty = false;
        }

        private static uint ResolveLayer(Material material, int property, List<Texture> textures, Dictionary<Texture, uint> layers)
        {
            if (material.HasProperty(property) == false)
            {
                return 0u;
            }

            var texture = material.GetTexture(property);

            if (texture == null)
            {
                return 0u;
            }

            if (layers.TryGetValue(texture, out uint layer))
            {
                return layer;
            }

            layer = (uint)(textures.Count + 1);

            textures.Add(texture);
            layers.Add(texture, layer);

            return layer;
        }

        private static float GetFloat(Material material, string name, float fallback)
        {
            return material.HasProperty(name) ? material.GetFloat(name) : fallback;
        }

        private static Vector4 GetVector(Material material, string name, Vector4 fallback)
        {
            return material.HasProperty(name) ? material.GetVector(name) : fallback;
        }

        private static Color GetColor(Material material, string name, Color fallback)
        {
            return (material.HasProperty(name) ? material.GetColor(name) : fallback).linear;
        }

        private GpuMaterialRow BuildRow(Material material, uint baseLayer, uint materialMapLayer, uint emissionLayer)
        {
            var row = new GpuMaterialRow();
            var openPbr = material.HasProperty("_OpenPbrBaseWeight");

            row.BaseColorFactor = GetColor(material, "_BaseColor", Color.white);
            row.BaseMapST = GetVector(material, "_BaseMap_ST", new Vector4(1f, 1f, 0f, 0f));
            row.EmissionMapST = GetVector(material, "_EmissionMap_ST", new Vector4(1f, 1f, 0f, 0f));
            row.LayerBase = baseLayer;
            row.LayerMaterialMap = materialMapLayer;
            row.LayerEmission = emissionLayer;

            uint flags = 0u;
            uint model;
            float metalFactor;
            float transmissionWeight;
            float coatWeight;
            float fuzzWeight;
            float thinTranslucencyWeight;

            if (openPbr)
            {
                model = 5u;

                flags |= RowFlagOpenPbr;

                if (GetFloat(material, "_OpenPbrGeometryThinWalled", 0f) > .5f)
                {
                    flags |= RowFlagThinWalled;
                }

                var emissionColor = GetColor(material, "_OpenPbrEmissionColor", Color.white);
                var emissionLuminance = GetFloat(material, "_OpenPbrEmissionLuminance", 0f);

                metalFactor = GetFloat(material, "_OpenPbrBaseMetalness", 0f);
                transmissionWeight = GetFloat(material, "_OpenPbrTransmissionWeight", 0f);
                coatWeight = GetFloat(material, "_OpenPbrCoatWeight", 0f);
                fuzzWeight = GetFloat(material, "_OpenPbrFuzzWeight", 0f);
                thinTranslucencyWeight = (flags & RowFlagThinWalled) != 0u ? GetFloat(material, "_OpenPbrSubsurfaceWeight", 0f) : 0f;

                var specularColor = GetColor(material, "_OpenPbrSpecularColor", Color.white);

                row.SpecularColorWeight = new Vector4(
                    specularColor.r,
                    specularColor.g,
                    specularColor.b,
                    GetFloat(material, "_OpenPbrSpecularWeight", 1f));

                var transmissionColor = GetColor(material, "_OpenPbrTransmissionColor", Color.white);

                row.TransmissionColorWeight = new Vector4(transmissionColor.r, transmissionColor.g, transmissionColor.b, transmissionWeight);

                var coatColor = GetColor(material, "_OpenPbrCoatColor", Color.white);

                row.CoatColorWeight = new Vector4(coatColor.r, coatColor.g, coatColor.b, coatWeight);

                row.EmissionFactorMetalness = new Vector4(
                    emissionColor.r * emissionLuminance,
                    emissionColor.g * emissionLuminance,
                    emissionColor.b * emissionLuminance,
                    metalFactor);

                var fuzzColor = GetColor(material, "_OpenPbrFuzzColor", Color.white);

                row.FuzzColorWeight = new Vector4(fuzzColor.r, fuzzColor.g, fuzzColor.b, fuzzWeight);

                var subsurfaceColor = GetColor(material, "_OpenPbrSubsurfaceColor", new Color(.8f, .8f, .8f, 1f));

                row.SubsurfaceColorWeight = new Vector4(
                    subsurfaceColor.r,
                    subsurfaceColor.g,
                    subsurfaceColor.b,
                    GetFloat(material, "_OpenPbrSubsurfaceWeight", 0f));

                row.CoatExtrasOpacity = new Vector4(
                    GetFloat(material, "_OpenPbrCoatDarkening", 1f),
                    GetFloat(material, "_OpenPbrCoatAffectColor", 0f),
                    GetFloat(material, "_OpenPbrCoatAffectRoughness", 0f),
                    GetFloat(material, "_OpenPbrGeometryOpacity", 1f));

                row.ScalarsA = new Vector4(
                    GetFloat(material, "_OpenPbrBaseWeight", 1f),
                    GetFloat(material, "_OpenPbrBaseDiffuseRoughness", 0f),
                    GetFloat(material, "_OpenPbrSpecularRoughness", .3f),
                    GetFloat(material, "_OpenPbrSpecularRoughnessAnisotropy", 0f));

                row.ScalarsB = new Vector4(
                    GetFloat(material, "_OpenPbrCoatRoughness", 0f),
                    GetFloat(material, "_OpenPbrCoatRoughnessAnisotropy", 0f),
                    GetFloat(material, "_OpenPbrThinFilmWeight", 0f),
                    GetFloat(material, "_OpenPbrFuzzRoughness", .5f));

                row.Iors = new Vector4(
                    GetFloat(material, "_OpenPbrSpecularIOR", 1.5f),
                    GetFloat(material, "_OpenPbrCoatIOR", 1.6f),
                    GetFloat(material, "_OpenPbrThinFilmIOR", 1.4f),
                    GetFloat(material, "_OpenPbrSubsurfaceScatterAnisotropy", 0f));

                row.ConductorEtaThinFilmThickness = new Vector4(1f, 1f, 1f, GetFloat(material, "_OpenPbrThinFilmThickness", .5f));
                row.ConductorKTransmissionDepth = new Vector4(0f, 0f, 0f, GetFloat(material, "_OpenPbrTransmissionDepth", 1f));
                row.ConductorEdgeTintReserved = new Vector4(specularColor.r, specularColor.g, specularColor.b, 0f);

                var subsurfaceRadius = GetVector(material, "_OpenPbrSubsurfaceRadius", new Vector4(1f, .5f, .25f, 0f));

                row.SubsurfaceRadiusScale = new Vector4(
                    subsurfaceRadius.x,
                    subsurfaceRadius.y,
                    subsurfaceRadius.z,
                    GetFloat(material, "_OpenPbrSubsurfaceRadiusScale", 1f));
            }
            else
            {
                model = (uint)Mathf.Max(0f, GetFloat(material, "_MaterialModel", 0f) + .5f);

                flags |= RowFlagRoughnessFloor;

                var conductorK = GetVector(material, "_K", Vector4.zero);

                if (Mathf.Max(Mathf.Abs(conductorK.x), Mathf.Max(Mathf.Abs(conductorK.y), Mathf.Abs(conductorK.z))) > 1e-5f)
                {
                    flags |= RowFlagExplicitConductor;
                }

                metalFactor = GetFloat(material, "_Metallic", 0f);
                transmissionWeight = GetFloat(material, "_Transparency", 0f);
                coatWeight = GetFloat(material, "_Clearcoat", 0f);
                fuzzWeight = 0f;
                thinTranslucencyWeight = 0f;

                var emissionColor = GetColor(material, "_EmissionColor", Color.black);

                row.SpecularColorWeight = new Vector4(1f, 1f, 1f, 1f);
                row.TransmissionColorWeight = new Vector4(1f, 1f, 1f, transmissionWeight);
                row.CoatColorWeight = new Vector4(1f, 1f, 1f, coatWeight);
                row.EmissionFactorMetalness = new Vector4(emissionColor.r, emissionColor.g, emissionColor.b, metalFactor);
                row.FuzzColorWeight = new Vector4(1f, 1f, 1f, 0f);

                var subsurfaceColor = GetColor(material, "_SubsurfaceColor", Color.black);

                row.SubsurfaceColorWeight = new Vector4(
                    subsurfaceColor.r,
                    subsurfaceColor.g,
                    subsurfaceColor.b,
                    GetFloat(material, "_SubsurfaceWeight", 0f));

                row.CoatExtrasOpacity = new Vector4(1f, 0f, 0f, 1f);
                row.ScalarsA = new Vector4(1f, 0f, GetFloat(material, "_Roughness", .3f), GetFloat(material, "_Anisotropy", 0f));
                row.ScalarsB = new Vector4(GetFloat(material, "_ClearcoatRoughness", 0f), 0f, 0f, .3f);

                row.Iors = new Vector4(
                    GetFloat(material, "_IOR", 1.5f),
                    GetFloat(material, "_ClearcoatIOR", 1.5f),
                    1.5f,
                    GetFloat(material, "_SubsurfaceAnisotropy", 0f));

                var conductorEta = GetVector(material, "_Eta", Vector4.one);

                row.ConductorEtaThinFilmThickness = new Vector4(conductorEta.x, conductorEta.y, conductorEta.z, .5f);
                row.ConductorKTransmissionDepth = new Vector4(conductorK.x, conductorK.y, conductorK.z, 1f);

                var edgeTint = GetColor(material, "_ConductorEdgeTint", Color.white);

                row.ConductorEdgeTintReserved = new Vector4(edgeTint.r, edgeTint.g, edgeTint.b, 0f);

                var subsurfaceRadius = GetVector(material, "_SubsurfaceRadius", new Vector4(1f, .5f, .25f, 0f));

                row.SubsurfaceRadiusScale = new Vector4(
                    subsurfaceRadius.x,
                    subsurfaceRadius.y,
                    subsurfaceRadius.z,
                    GetFloat(material, "_SubsurfaceScale", 1f));
            }

            var complex =
                model == 2u || model == 3u || model == 4u ||
                metalFactor > 1e-4f ||
                transmissionWeight > 1e-4f ||
                coatWeight > 1e-4f ||
                fuzzWeight > 1e-4f ||
                thinTranslucencyWeight > 1e-4f;

            if (complex)
            {
                flags |= RowFlagComplex;
            }

            row.RowFlags = flags;
            row.MaterialModel = model;

            return row;
        }

        private static GpuMaterialRow BuildDefaultRow()
        {
            var row = new GpuMaterialRow()
            {
                BaseColorFactor = Color.white,
                SpecularColorWeight = new Vector4(
                1f,
                1f,
                1f,
                1f),
                TransmissionColorWeight = new Vector4(
                1f,
                1f,
                1f,
                0f),
                CoatColorWeight = new Vector4(
                1f,
                1f,
                1f,
                0f),
                EmissionFactorMetalness = Vector4.zero,
                FuzzColorWeight = new Vector4(
                1f,
                1f,
                1f,
                0f),
                SubsurfaceColorWeight = Vector4.zero,
                CoatExtrasOpacity = new Vector4(
                1f,
                0f,
                0f,
                1f),
                ScalarsA = new Vector4(
                1f,
                0f,
                .3f,
                0f),
                ScalarsB = new Vector4(
                0f,
                0f,
                0f,
                .3f),
                Iors = new Vector4(
                1.5f,
                1.5f,
                1.5f,
                0f),
                ConductorEtaThinFilmThickness = new Vector4(
                1f,
                1f,
                1f,
                .5f),
                ConductorKTransmissionDepth = new Vector4(
                0f,
                0f,
                0f,
                1f),
                ConductorEdgeTintReserved = new Vector4(
                1f,
                1f,
                1f,
                0f),
                SubsurfaceRadiusScale = new Vector4(
                1f,
                .5f,
                .25f,
                1f),
                BaseMapST = new Vector4(
                1f,
                1f,
                0f,
                0f),
                EmissionMapST = new Vector4(
                1f,
                1f,
                0f,
                0f),
                RowFlags = RowFlagRoughnessFloor,
                MaterialModel = 0u
            };

            return row;
        }

        private static void BuildAtlas(ref RenderTexture atlas, List<Texture> textures, bool srgb)
        {
            var size = AtlasMinSize;

            for (var j = 0; j < textures.Count; j++)
            {
                var texture = textures[j];

                size = Mathf.Max(size, Mathf.Max(texture.width, texture.height));
            }

            size = Mathf.Min(size, AtlasMaxSize);

            var depth = textures.Count + 1;

            if (atlas != null && (atlas.width != size || atlas.volumeDepth != depth))
            {
                atlas.Release();
                Object.DestroyImmediate(atlas);

                atlas = null;
            }

            if (atlas == null)
            {
                var descriptor = new RenderTextureDescriptor(size, size, RenderTextureFormat.ARGB32, 0)
                {
                    dimension = TextureDimension.Tex2DArray,
                    volumeDepth = depth,
                    sRGB = srgb,
                    useMipMap = false,
                    autoGenerateMips = false
                };

                atlas = new RenderTexture(descriptor)
                {
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave
                };

                atlas.Create();
            }

            var cmd = CommandBufferPool.Get("LucentPath Material Atlas");

            cmd.Blit(Texture2D.whiteTexture, atlas, Vector2.one, Vector2.zero, 0, 0);

            for (var i = 0; i < textures.Count; i++)
            {
                cmd.Blit(textures[i], atlas, Vector2.one, Vector2.zero, 0, i + 1);
            }

            Graphics.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private void Bind()
        {
            if (_tableBuffer == null)
            {
                Rebuild();
            }

            Shader.SetGlobalBuffer(_lucentMaterialTableProperty, _tableBuffer);
            Shader.SetGlobalInt(_lucentMaterialTableCountProperty, Mathf.Max(_materials.Count, 1));

            if (_baseAtlas != null)
            {
                Shader.SetGlobalTexture(_lucentBaseAtlasProperty, _baseAtlas);
            }

            if (_materialMapAtlas != null)
            {
                Shader.SetGlobalTexture(_lucentMaterialMapAtlasProperty, _materialMapAtlas);
            }

            if (_emissionAtlas != null)
            {
                Shader.SetGlobalTexture(_lucentEmissionAtlasProperty, _emissionAtlas);
            }
        }

        private static void ReleaseAtlas(ref RenderTexture atlas)
        {
            if (atlas == null)
            {
                return;
            }

            atlas.Release();
            Object.DestroyImmediate(atlas);

            atlas = null;
        }
    }
}
