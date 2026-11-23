using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using static LucentPath.Rendering.PipelineShaderProperties;

namespace LucentPath.Rendering
{
    internal class DXRBloomPass
    {
        private const int PassPrefilterDownsample = 0;
        private const int PassDownsample = 1;
        private const int PassSeed = 2;
        private const int PassUpsampleCombine = 3;
        private const int PassComposite = 4;
        private static readonly Vector3 _luminanceWeights = new(.2126f, .7152f, .0722f);
        private static readonly int[] _bloomMipDownIds = CreateBloomIds("_MCBloomMipDown");
        private static readonly int[] _bloomMipUpIds = CreateBloomIds("_MCBloomMipUp");

        private readonly DXRPipelineResources _context;
        private readonly Vector4[] _bandWeights = new Vector4[LucentBloomValidation.PsfBandCount];
        private readonly Dictionary<EntityId, Color[]> _profileCache = new();

        public DXRBloomPass(DXRPipelineResources context)
        {
            _context = context;
        }

        public bool TryExecute(
            CommandBuffer cmd,
            Camera camera,
            RenderTargetIdentifier source,
            int width,
            int height,
            out RenderTargetIdentifier bloomedSource)
        {
            bloomedSource = source;

            if (TryGetBloomPresentation(out LucentBloomVolume bloom) == false)
            {
                return false;
            }

            if (_context.BloomMaterial == null)
            {
                LogMissingMaterialOnce(bloom);

                return false;
            }

            if (TryBuildBandWeights(bloom, width, height, out int mipCount, out string validationMessage) == false)
            {
                LogInvalidProfileOnce(validationMessage, bloom);

                return false;
            }

            var outputDesc = new RenderTextureDescriptor(width, height, GraphicsFormat.R16G16B16A16_SFloat, 0)
            {
                msaaSamples = 1,
                depthBufferBits = 0,
                enableRandomWrite = false,
                sRGB = false,
                useMipMap = false,
                autoGenerateMips = false
            };

            var mipWidths = new int[LucentBloomValidation.PsfBandCount];
            var mipHeights = new int[LucentBloomValidation.PsfBandCount];

            for (var i = 0; i < mipCount; i++)
            {
                mipWidths[i] = Mathf.Max(1, width >> (i + 1));
                mipHeights[i] = Mathf.Max(1, height >> (i + 1));

                var mipDesc = outputDesc;

                mipDesc.width = mipWidths[i];
                mipDesc.height = mipHeights[i];

                cmd.GetTemporaryRT(_bloomMipDownIds[i], mipDesc, FilterMode.Bilinear);
                cmd.GetTemporaryRT(_bloomMipUpIds[i], mipDesc, FilterMode.Bilinear);
            }

            cmd.GetTemporaryRT(BloomOutputProperty, outputDesc, FilterMode.Bilinear);

            BlitToTemporary(cmd, source, _bloomMipDownIds[0], mipWidths[0], mipHeights[0], PassPrefilterDownsample);

            for (var i = 1; i < mipCount; i++)
            {
                BlitToTemporary(cmd, _bloomMipDownIds[i - 1], _bloomMipDownIds[i], mipWidths[i], mipHeights[i], PassDownsample);
            }

            _context.BloomMaterial.SetVector(BloomBandWeightProperty, _bandWeights[mipCount - 1]);

            BlitToTemporary(
                cmd,
                _bloomMipDownIds[mipCount - 1],
                _bloomMipUpIds[mipCount - 1],
                mipWidths[mipCount - 1],
                mipHeights[mipCount - 1],
                PassSeed);

            for (var i = mipCount - 2; i >= 0; i--)
            {
                _context.BloomMaterial.SetVector(BloomBandWeightProperty, _bandWeights[i]);
                SetLowMipGlobals(cmd, _bloomMipUpIds[i + 1], mipWidths[i + 1], mipHeights[i + 1]);
                BlitToTemporary(cmd, _bloomMipDownIds[i], _bloomMipUpIds[i], mipWidths[i], mipHeights[i], PassUpsampleCombine);
            }

            SetLowMipGlobals(cmd, _bloomMipUpIds[0], mipWidths[0], mipHeights[0]);
            BlitToTemporary(cmd, source, BloomOutputProperty, width, height, PassComposite);

            for (var i = 0; i < mipCount; i++)
            {
                cmd.ReleaseTemporaryRT(_bloomMipDownIds[i]);
                cmd.ReleaseTemporaryRT(_bloomMipUpIds[i]);
            }

            bloomedSource = BloomOutputProperty;

            return true;
        }

        private bool TryGetBloomPresentation(out LucentBloomVolume bloom)
        {
            bloom = VolumeManager.instance.stack?.GetComponent<LucentBloomVolume>();

            return bloom != null && bloom.IsActive();
        }

        private bool TryBuildBandWeights(LucentBloomVolume bloom, int outputWidth, int outputHeight, out int mipCount, out string validationMessage)
        {
            mipCount = ComputeMipCount(outputWidth, outputHeight);
            validationMessage = null;

            if (bloom.RgbPsfProfile.value is not Texture2D profileTexture)
            {
                validationMessage = "the RGB PSF profile must be assigned as a Texture2D.";
                mipCount = 0;

                return false;
            }

            if (LucentBloomValidation.TryValidateProfile(profileTexture, out validationMessage) == false)
            {
                mipCount = 0;

                return false;
            }

            if (_profileCache.TryGetValue(profileTexture.GetEntityId(), out Color[] texels) == false)
            {
                texels = profileTexture.GetPixels(0);
                _profileCache[profileTexture.GetEntityId()] = texels;
            }

            var radiusScale = Mathf.Max(1e-4f, bloom.RadiusScale.value);
            var chromaticSpreadScale = Mathf.Clamp01(bloom.ChromaticSpreadScale.value);
            var energyScale = Mathf.Max(0f, bloom.Intensity.value);

            mipCount = Mathf.Max(1, Mathf.Min(LucentBloomValidation.PsfBandCount, mipCount));

            var bandSum = Vector3.zero;

            for (var i = 0; i < mipCount; i++)
            {
                var t = mipCount > 1 ? i / (float)(mipCount - 1) : 0f;
                var scaledT = Mathf.Clamp01(t / radiusScale);
                var rgbWeight = SampleProfile(texels, scaledT);
                var lumaWeight = Vector3.Dot(rgbWeight, _luminanceWeights);
                var mixedWeight = Vector3.Lerp(new Vector3(lumaWeight, lumaWeight, lumaWeight), rgbWeight, chromaticSpreadScale);

                _bandWeights[i] = new Vector4(mixedWeight.x, mixedWeight.y, mixedWeight.z, 0f);

                bandSum += mixedWeight;
            }

            var safeSum = new Vector3(
                Mathf.Max(bandSum.x, 1e-6f),
                Mathf.Max(bandSum.y, 1e-6f),
                Mathf.Max(bandSum.z, 1e-6f));

            for (var i = 0; i < mipCount; i++)
            {
                var weight = _bandWeights[i];

                weight.x = weight.x * energyScale / safeSum.x;
                weight.y = weight.y * energyScale / safeSum.y;
                weight.z = weight.z * energyScale / safeSum.z;
                _bandWeights[i] = weight;
            }

            for (var i = mipCount; i < _bandWeights.Length; i++)
            {
                _bandWeights[i] = Vector4.zero;
            }

            return true;
        }

        private static int ComputeMipCount(int width, int height)
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);

            var mipCount = 0;

            while (mipCount < LucentBloomValidation.PsfBandCount)
            {
                var mipWidth = Mathf.Max(1, width >> (mipCount + 1));
                var mipHeight = Mathf.Max(1, height >> (mipCount + 1));

                mipCount++;

                if (mipWidth == 1 && mipHeight == 1)
                {
                    break;
                }
            }

            return mipCount;
        }

        private static Vector3 SampleProfile(Color[] texels, float t)
        {
            var clampedT = Mathf.Clamp01(t) * (texels.Length - 1);
            var leftIndex = Mathf.Clamp(Mathf.FloorToInt(clampedT), 0, texels.Length - 1);
            var rightIndex = Mathf.Min(leftIndex + 1, texels.Length - 1);
            var lerpT = clampedT - leftIndex;
            var sampled = Color.Lerp(texels[leftIndex], texels[rightIndex], lerpT);

            return new Vector3(
                Mathf.Max(sampled.r, 0f),
                Mathf.Max(sampled.g, 0f),
                Mathf.Max(sampled.b, 0f));
        }

        private void BlitToTemporary(CommandBuffer cmd, RenderTargetIdentifier source, int destination, int width, int height, int pass)
        {
            cmd.SetRenderTarget(destination);
            cmd.SetViewport(new Rect(0f, 0f, width, height));
            Blitter.BlitTexture(cmd, source, FullScreenBlitScaleBias, _context.BloomMaterial, pass);
        }

        private void SetLowMipGlobals(CommandBuffer cmd, int lowMip, int width, int height)
        {
            cmd.SetGlobalTexture(SourceTexLowMipProperty, lowMip);

            cmd.SetGlobalVector(SourceTexLowMipTexelSizeProperty, new Vector4(
                1f / Mathf.Max(1, width),
                1f / Mathf.Max(1, height),
                width,
                height));
        }

        private void LogInvalidProfileOnce(string validationMessage, LucentBloomVolume bloom)
        {
            if (_context.IsBloomProfileWarningLogged)
            {
                return;
            }

            Debug.LogError(
                $"LucentPath bloom is active, but the RGB PSF profile is invalid because {validationMessage} Assign a linear {LucentBloomValidation.ProfileWidth}x{LucentBloomValidation.ProfileHeight} readable Texture2D with Clamp wrap mode and no mipmaps.",
                bloom != null ? (Object)bloom : _context.Asset);

            _context.IsBloomProfileWarningLogged = true;
        }

        private void LogMissingMaterialOnce(LucentBloomVolume bloom)
        {
            if (_context.IsBloomMaterialWarningLogged)
            {
                return;
            }

            Debug.LogError(
                "LucentPath bloom is active, but no bloom presentation shader is available. Assign Bloom Shader on the LucentPath renderer settings or ensure Hidden/LucentPath/Bloom is present.",
                bloom != null ? (Object)bloom : _context.Asset);

            _context.IsBloomMaterialWarningLogged = true;
        }

        private static int[] CreateBloomIds(string prefix)
        {
            var ids = new int[LucentBloomValidation.PsfBandCount];

            for (var i = 0; i < ids.Length; i++)
            {
                ids[i] = Shader.PropertyToID(prefix + i);
            }

            return ids;
        }
    }
}
