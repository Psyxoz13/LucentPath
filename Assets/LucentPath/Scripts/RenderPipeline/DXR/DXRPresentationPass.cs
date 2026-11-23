using UnityEngine;
using UnityEngine.Rendering;
using static LucentPath.Rendering.PipelineShaderProperties;

namespace LucentPath.Rendering
{
    internal class DXRPresentationPass
    {
        private readonly DXRPipelineResources _context;

        public DXRPresentationPass(DXRPipelineResources context)
        {
            _context = context;
        }

        public Material ResolveMaterial(bool needsScaledCopy)
        {
            if (needsScaledCopy)
            {
                return HasPass(_context.CopyMaterial, CompositeScaleCopyPass) ? _context.CopyMaterial : null;
            }

            if (HasPass(_context.CompositeMaterial, CompositeCopyPass))
            {
                return _context.CompositeMaterial;
            }

            return HasPass(_context.CopyMaterial, CompositeCopyPass) ? _context.CopyMaterial : null;
        }

        public Material ResolveTonemapMaterial()
        {
            return HasPass(_context.CompositeMaterial, CompositeTonemapPass) ? _context.CompositeMaterial : null;
        }

        public static bool HasPass(Material material, int pass)
        {
            return material != null && pass >= 0 && pass < material.passCount;
        }

        public static void GetClipSpaceZRange(out float nearZ, out float farZ)
        {
            if (SystemInfo.usesReversedZBuffer)
            {
                nearZ = 1f;
                farZ = 0f;

                return;
            }

            switch (SystemInfo.graphicsDeviceType)
            {
                case GraphicsDeviceType.OpenGLCore:
                case GraphicsDeviceType.OpenGLES3:
                    nearZ = -1f;
                    farZ = 1f;
                    break;

                default:
                    nearZ = 0f;
                    farZ = 1f;
                    break;
            }
        }

        public void Dispose()
        {
            if (_context.CompositeMaterial != null)
            {
                CoreUtils.Destroy(_context.CompositeMaterial);
            }

            if (_context.IsCopyMaterialOwned && _context.CopyMaterial != null)
            {
                CoreUtils.Destroy(_context.CopyMaterial);
            }

            if (_context.BloomMaterial != null)
            {
                CoreUtils.Destroy(_context.BloomMaterial);
            }

            if (_context.PostSharpenMaterial != null)
            {
                CoreUtils.Destroy(_context.PostSharpenMaterial);
            }
        }
    }
}
