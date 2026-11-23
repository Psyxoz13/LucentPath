using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LucentPath.Rendering
{
    public static class LucentBloomValidation
    {
        public const int PsfBandCount = 8;
        public const int ProfileWidth = PsfBandCount;
        public const int ProfileHeight = 1;

        public static bool TryValidateProfile(Texture texture, out string message)
        {
            if (texture == null)
            {
                message = "no RGB PSF profile texture is assigned.";

                return false;
            }

            if (texture.dimension != TextureDimension.Tex2D || texture is not Texture2D texture2D)
            {
                message = "the RGB PSF profile must be a 2D Texture2D asset.";

                return false;
            }

            if (texture2D.width != ProfileWidth || texture2D.height != ProfileHeight)
            {
                message = $"the RGB PSF profile must be {ProfileWidth}x{ProfileHeight}.";

                return false;
            }

            if (texture2D.mipmapCount != 1)
            {
                message = "the RGB PSF profile must have mipmaps disabled.";

                return false;
            }

            if (GraphicsFormatUtility.IsSRGBFormat(texture2D.graphicsFormat))
            {
                message = "the RGB PSF profile must import as linear data, not sRGB.";

                return false;
            }

            if (texture2D.wrapMode != TextureWrapMode.Clamp ||
                texture2D.wrapModeU != TextureWrapMode.Clamp ||
                texture2D.wrapModeV != TextureWrapMode.Clamp)
            {
                message = "the RGB PSF profile must use Clamp wrap mode.";

                return false;
            }

            if (texture2D.isReadable == false)
            {
                message = "the RGB PSF profile must have Read/Write enabled so LucentPath can normalize spectral band weights at runtime.";

                return false;
            }

            message = null;

            return true;
        }

#if UNITY_EDITOR
        public static bool TryGetEditorValidationMessage(Texture texture, out string message)
        {
            if (TryValidateProfile(texture, out message) == false)
            {
                return false;
            }

            var assetPath = AssetDatabase.GetAssetPath(texture);

            if (string.IsNullOrEmpty(assetPath))
            {
                return true;
            }

            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
            {
                return true;
            }

            if (importer.textureShape != TextureImporterShape.Texture2D)
            {
                message = "Import the RGB PSF profile as a 2D texture.";

                return false;
            }

            if (importer.sRGBTexture)
            {
                message = "Disable sRGB (Color Texture) on the RGB PSF profile import settings.";

                return false;
            }

            if (importer.mipmapEnabled)
            {
                message = "Disable Generate Mip Maps on the RGB PSF profile import settings.";

                return false;
            }

            if (importer.isReadable == false)
            {
                message = "Enable Read/Write on the RGB PSF profile import settings.";

                return false;
            }

            if (importer.wrapMode != TextureWrapMode.Clamp ||
                importer.wrapModeU != TextureWrapMode.Clamp ||
                importer.wrapModeV != TextureWrapMode.Clamp)
            {
                message = "Set Wrap Mode to Clamp on the RGB PSF profile import settings.";

                return false;
            }

            message = null;

            return true;
        }
#endif
    }
}
