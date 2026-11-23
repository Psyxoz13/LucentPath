using UnityEngine;
using UnityEngine.Rendering;

namespace LucentPath.Rendering
{
    internal class SkyboxCubemapBaker : System.IDisposable
    {
        public RenderTexture BakedCubemap => _cubemap;
        public bool IsContentAvailable => _isBaked && _cubemap != null;

        private RenderTexture _cubemap;
        private Mesh _cubeMesh;
        private Matrix4x4[] _faceViewMatrices;

        private Matrix4x4 _faceProj;

        private EntityId _lastMaterialEntityId = EntityId.None;
        private int _lastMaterialCrc = 0;
        private int _lastTextureInstanceID = 0;
        private int _lastTextureImageHash = 0;
        private bool _isBaked;

        private const int BakeResolution = 1024;

        private static readonly int[] _skyboxTextureProperties =
        {
            Shader.PropertyToID("_Tex"),
            Shader.PropertyToID("_MainTex"),
            Shader.PropertyToID("_BaseMap"),
            Shader.PropertyToID("_FrontTex"),
            Shader.PropertyToID("_BackTex"),
            Shader.PropertyToID("_LeftTex"),
            Shader.PropertyToID("_RightTex"),
            Shader.PropertyToID("_UpTex"),
            Shader.PropertyToID("_DownTex"),
        };

        public void EnsureResources()
        {
            if (_cubemap == null)
            {
                _cubemap = new RenderTexture(BakeResolution, BakeResolution, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
                {
                    dimension = TextureDimension.Cube,
                    useMipMap = true,
                    autoGenerateMips = false,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Trilinear,
                    anisoLevel = 0,
                    name = "LucentBakedSkyCubemap",
                    hideFlags = HideFlags.HideAndDontSave
                };

                _cubemap.Create();
            }

            if (_cubeMesh == null)
            {
                _cubeMesh = new Mesh { hideFlags = HideFlags.HideAndDontSave, name = "LucentSkyboxCubeMesh" };

                Vector3[] v = {
                    new(-1, -1, -1), new( 1, -1, -1), new( 1,  1, -1), new(-1,  1, -1),
                    new(-1, -1,  1), new( 1, -1,  1), new( 1,  1,  1), new(-1,  1,  1)
                };

                int[] t = {
                    0, 2, 1,  0, 3, 2,
                    4, 5, 6,  4, 6, 7,
                    0, 1, 5,  0, 5, 4,
                    2, 3, 7,  2, 7, 6,
                    1, 2, 6,  1, 6, 5,
                    0, 4, 7,  0, 7, 3
                };

                _cubeMesh.vertices = v;
                _cubeMesh.triangles = t;

                _cubeMesh.RecalculateBounds();
            }

            if (_faceViewMatrices == null)
            {
                _faceViewMatrices = new Matrix4x4[6];

                Vector3[] forwards = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
                Vector3[] ups = { Vector3.up, Vector3.up, new Vector3(0f, 0f, -1f), Vector3.forward, Vector3.up, Vector3.up };

                for (int i = 0; i < 6; i++)
                {
                    Matrix4x4 lookAt = Matrix4x4.LookAt(Vector3.zero, forwards[i], ups[i]);
                    Matrix4x4 worldToView = lookAt.inverse;

                    worldToView = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * worldToView;
                    _faceViewMatrices[i] = worldToView;
                }

                Matrix4x4 proj = Matrix4x4.Perspective(90f, 1f, .01f, 100f);

                _faceProj = GL.GetGPUProjectionMatrix(proj, true);
            }
        }

        public bool RequiresBake(Material skybox)
        {
            if (skybox == null)
            {
                return false;
            }

            var id = skybox.GetEntityId();
            int crc = skybox.ComputeCRC();
            int texId;
            int texHash;

            ResolveSkyboxTextureSignature(skybox, out texId, out texHash);

            return _isBaked == false
                || id != _lastMaterialEntityId
                || crc != _lastMaterialCrc
                || texId != _lastTextureInstanceID
                || texHash != _lastTextureImageHash;
        }

        public void Bake(CommandBuffer cmd, Matrix4x4 restoreView, Matrix4x4 restoreProjection)
        {
            var sky = RenderSettings.skybox;

            if (sky == null || _cubemap == null || _cubeMesh == null)
            {
                return;
            }

            if (RequiresBake(sky) == false)
            {
                return;
            }

            for (int face = 0; face < 6; face++)
            {
                cmd.SetRenderTarget(_cubemap, 0, (CubemapFace)face);
                cmd.SetViewProjectionMatrices(_faceViewMatrices[face], _faceProj);
                cmd.DrawMesh(_cubeMesh, Matrix4x4.identity, sky, 0, 0);
            }

            cmd.GenerateMips(_cubemap);
            cmd.SetViewProjectionMatrices(restoreView, restoreProjection);

            _lastMaterialEntityId = sky.GetEntityId();
            _lastMaterialCrc = sky.ComputeCRC();

            ResolveSkyboxTextureSignature(sky, out _lastTextureInstanceID, out _lastTextureImageHash);

            _isBaked = true;
        }

        public void InvalidateCache()
        {
            _isBaked = false;
            _lastMaterialEntityId = EntityId.None;
            _lastMaterialCrc = 0;
        }

        public void Dispose()
        {
            if (_cubemap != null)
            {
                _cubemap.Release();
                Object.DestroyImmediate(_cubemap);

                _cubemap = null;
            }

            if (_cubeMesh != null)
            {
                Object.DestroyImmediate(_cubeMesh);

                _cubeMesh = null;
            }

            _isBaked = false;
        }

        private static void ResolveSkyboxTextureSignature(Material skybox, out int textureInstanceID, out int imageHash)
        {
            textureInstanceID = 0;
            imageHash = 0;

            for (var i = 0; i < _skyboxTextureProperties.Length; i++)
            {
                int propId = _skyboxTextureProperties[i];

                if (skybox.HasProperty(propId) == false)
                {
                    continue;
                }

                Texture tex = skybox.GetTexture(propId);

                if (tex == null)
                {
                    continue;
                }

                textureInstanceID ^= tex.GetEntityId().GetHashCode();
                imageHash ^= GetTextureContentVersion(tex);
            }
        }

        private static int GetTextureContentVersion(Texture texture)
        {
#if UNITY_EDITOR
            return texture.imageContentsHash.GetHashCode();
#else
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (int)texture.updateCount;
                hash = hash * 31 + texture.width;
                hash = hash * 31 + texture.height;
                hash = hash * 31 + (int)texture.dimension;
                hash = hash * 31 + texture.mipmapCount;
                return hash;
            }
#endif
        }
    }
}
