using UnityEngine;
using UnityEngine.Rendering;

namespace LucentPath.Rendering
{
    internal class MultiScatterLut : System.IDisposable
    {
        public static readonly int DirectionalAlbedoGlobalProperty = Shader.PropertyToID("_MsLutDirectional");
        public static readonly int AverageAlbedoGlobalProperty = Shader.PropertyToID("_MsLutAverage");
        public static readonly int SheenDirectionalAlbedoGlobalProperty = Shader.PropertyToID("_MsLutSheenDirectional");
        public static readonly int LutResolutionGlobalProperty = Shader.PropertyToID("_MsLutResolution");
        public static readonly int ReadyGlobalProperty = Shader.PropertyToID("_MsLutReady");
        public static readonly int SheenReadyGlobalProperty = Shader.PropertyToID("_MsLutSheenReady");

        public bool IsReady => _isBaked;
        public RenderTexture DirectionalAlbedo => _directionalAlbedo;
        public RenderTexture SheenDirectionalAlbedo => _sheenDirectionalAlbedo;
        public ComputeBuffer AverageAlbedo => _averageAlbedo;

        private RenderTexture _directionalAlbedo;
        private RenderTexture _sheenDirectionalAlbedo;
        private ComputeBuffer _averageAlbedo;
        private ComputeShader _bakerShader;

        private int _kernelDirectional = -1;
        private int _kernelAverage = -1;
        private int _kernelSheenDirectional = -1;
        private bool _isBaked;

        private const int Resolution = 32;
        private const int SampleCount = 1024;

        private static readonly int _directionalAlbedoProperty = Shader.PropertyToID("_MsDirectionalAlbedo");
        private static readonly int _averageAlbedoProperty = Shader.PropertyToID("_MsAverageAlbedo");
        private static readonly int _directionalAlbedoInProperty = Shader.PropertyToID("_MsDirectionalAlbedoIn");
        private static readonly int _sheenDirectionalAlbedoProperty = Shader.PropertyToID("_MsSheenDirectionalAlbedo");
        private static readonly int _lutResolutionProperty = Shader.PropertyToID("_MsLutResolution");
        private static readonly int _sampleCountProperty = Shader.PropertyToID("_MsSampleCount");

        public void SetBakerShader(ComputeShader shader)
        {
            if (shader == _bakerShader && _kernelDirectional >= 0 && _kernelAverage >= 0 && _kernelSheenDirectional >= 0)
            {
                return;
            }

            _bakerShader = shader;
            _kernelDirectional = shader != null ? shader.FindKernel("CSBakeDirectionalAlbedo") : -1;
            _kernelAverage = shader != null ? shader.FindKernel("CSBakeAverageAlbedo") : -1;
            _kernelSheenDirectional = shader != null ? shader.FindKernel("CSBakeSheenDirectionalAlbedo") : -1;
            _isBaked = false;
        }

        public void EnsureResources()
        {
            if (_directionalAlbedo == null)
            {
                _directionalAlbedo = new RenderTexture(Resolution, Resolution, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear)
                {
                    enableRandomWrite = true,
                    useMipMap = false,
                    autoGenerateMips = false,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    name = "LucentMsLutDirectional",
                    hideFlags = HideFlags.HideAndDontSave
                };

                _directionalAlbedo.Create();
            }

            if (_sheenDirectionalAlbedo == null)
            {
                _sheenDirectionalAlbedo = new RenderTexture(Resolution, Resolution, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear)
                {
                    enableRandomWrite = true,
                    useMipMap = false,
                    autoGenerateMips = false,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    name = "LucentMsLutSheenDirectional",
                    hideFlags = HideFlags.HideAndDontSave
                };

                _sheenDirectionalAlbedo.Create();
            }

            if (_averageAlbedo == null || _averageAlbedo.count != Resolution)
            {
                ReleaseBuffer(ref _averageAlbedo);

                _averageAlbedo = new ComputeBuffer(Resolution, sizeof(float), ComputeBufferType.Structured);
            }
        }

        public void BakeIfNeeded(CommandBuffer cmd)
        {
            EnsureResources();

            if (_isBaked)
            {
                return;
            }

            if (_bakerShader == null || _kernelDirectional < 0 || _kernelAverage < 0 || _kernelSheenDirectional < 0)
            {
                return;
            }

            cmd.SetComputeIntParam(_bakerShader, _lutResolutionProperty, Resolution);
            cmd.SetComputeIntParam(_bakerShader, _sampleCountProperty, SampleCount);
            cmd.SetComputeTextureParam(_bakerShader, _kernelDirectional, _directionalAlbedoProperty, _directionalAlbedo);

            int groups = Mathf.CeilToInt(Resolution / 8f);

            cmd.DispatchCompute(_bakerShader, _kernelDirectional, groups, groups, 1);

            cmd.SetComputeTextureParam(_bakerShader, _kernelAverage, _directionalAlbedoInProperty, _directionalAlbedo);
            cmd.SetComputeBufferParam(_bakerShader, _kernelAverage, _averageAlbedoProperty, _averageAlbedo);

            int avgGroups = Mathf.CeilToInt(Resolution / 64f);

            cmd.DispatchCompute(_bakerShader, _kernelAverage, avgGroups, 1, 1);

            cmd.SetComputeTextureParam(_bakerShader, _kernelSheenDirectional, _sheenDirectionalAlbedoProperty, _sheenDirectionalAlbedo);
            cmd.DispatchCompute(_bakerShader, _kernelSheenDirectional, groups, groups, 1);

            _isBaked = true;
        }

        public void BindGlobals(CommandBuffer cmd)
        {
            EnsureResources();
            cmd.SetGlobalTexture(DirectionalAlbedoGlobalProperty, _directionalAlbedo);
            cmd.SetGlobalBuffer(AverageAlbedoGlobalProperty, _averageAlbedo);
            cmd.SetGlobalTexture(SheenDirectionalAlbedoGlobalProperty, _sheenDirectionalAlbedo);
            cmd.SetGlobalInt(LutResolutionGlobalProperty, Resolution);
            cmd.SetGlobalInt(ReadyGlobalProperty, _isBaked ? 1 : 0);
            cmd.SetGlobalInt(SheenReadyGlobalProperty, _isBaked ? 1 : 0);
        }

        public void BindKernel(CommandBuffer cmd, ComputeShader shader, int kernel)
        {
            EnsureResources();
            cmd.SetComputeTextureParam(shader, kernel, DirectionalAlbedoGlobalProperty, _directionalAlbedo);
            cmd.SetComputeBufferParam(shader, kernel, AverageAlbedoGlobalProperty, _averageAlbedo);
            cmd.SetComputeTextureParam(shader, kernel, SheenDirectionalAlbedoGlobalProperty, _sheenDirectionalAlbedo);
            cmd.SetComputeIntParam(shader, LutResolutionGlobalProperty, Resolution);
            cmd.SetComputeIntParam(shader, ReadyGlobalProperty, _isBaked ? 1 : 0);
            cmd.SetComputeIntParam(shader, SheenReadyGlobalProperty, _isBaked ? 1 : 0);
        }

        public void Dispose()
        {
            if (_directionalAlbedo != null)
            {
                _directionalAlbedo.Release();
                Object.DestroyImmediate(_directionalAlbedo);

                _directionalAlbedo = null;
            }

            if (_sheenDirectionalAlbedo != null)
            {
                _sheenDirectionalAlbedo.Release();
                Object.DestroyImmediate(_sheenDirectionalAlbedo);

                _sheenDirectionalAlbedo = null;
            }

            ReleaseBuffer(ref _averageAlbedo);

            _isBaked = false;
        }

        private static void ReleaseBuffer(ref ComputeBuffer buffer)
        {
            if (buffer != null)
            {
                buffer.Release();

                buffer = null;
            }
        }
    }
}
