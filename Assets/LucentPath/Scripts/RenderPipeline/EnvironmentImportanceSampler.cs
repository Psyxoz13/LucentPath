using UnityEngine;
using UnityEngine.Rendering;

namespace LucentPath.Rendering
{
    internal class EnvironmentImportanceSampler : System.IDisposable
    {
        private const int SamplingResolution = 256;

        private static readonly int _sourceCubemapProperty = Shader.PropertyToID("_SourceCubemap");
        private static readonly int _envLuminanceProperty = Shader.PropertyToID("_EnvLuminance");
        private static readonly int _envCdfProperty = Shader.PropertyToID("_EnvCdf");
        private static readonly int _envTotalLuminanceProperty = Shader.PropertyToID("_EnvTotalLuminance");
        private static readonly int _envAliasProbProperty = Shader.PropertyToID("_EnvAliasProb");
        private static readonly int _envAliasIndexProperty = Shader.PropertyToID("_EnvAliasIndex");
        private static readonly int _envAliasSmallProperty = Shader.PropertyToID("_EnvAliasSmall");
        private static readonly int _envAliasLargeProperty = Shader.PropertyToID("_EnvAliasLarge");
        private static readonly int _envSamplingResolutionProperty = Shader.PropertyToID("_EnvSamplingResolution");

        public static readonly int EnvCdfBufferProperty = Shader.PropertyToID("_EnvCdfBuffer");
        public static readonly int EnvLuminanceBufferProperty = Shader.PropertyToID("_EnvLuminanceBuffer");
        public static readonly int EnvTotalLuminanceBufferProperty = Shader.PropertyToID("_EnvTotalLuminanceBuffer");
        public static readonly int EnvAliasProbBufferProperty = Shader.PropertyToID("_EnvAliasProbBuffer");
        public static readonly int EnvAliasIndexBufferProperty = Shader.PropertyToID("_EnvAliasIndexBuffer");
        public static readonly int EnvSamplingReadyProperty = Shader.PropertyToID("_EnvSamplingReady");
        public static readonly int EnvSamplingResolutionProperty = _envSamplingResolutionProperty;

        private ComputeBuffer _luminanceBuffer;
        private ComputeBuffer _cdfBuffer;
        private ComputeBuffer _totalLuminanceBuffer;
        private ComputeBuffer _aliasProbBuffer;
        private ComputeBuffer _aliasIndexBuffer;
        private ComputeBuffer _aliasSmallBuffer;
        private ComputeBuffer _aliasLargeBuffer;
        private ComputeShader _buildShader;

        private int _kernelReduce = -1;
        private int _kernelBuildCdf = -1;
        private int _kernelBuildAlias = -1;
        private bool _isReady;

        public bool IsReady => _isReady;
        public int Resolution => SamplingResolution;
        public ComputeBuffer CdfBuffer => _cdfBuffer;
        public ComputeBuffer LuminanceBuffer => _luminanceBuffer;
        public ComputeBuffer TotalLuminanceBuffer => _totalLuminanceBuffer;

        public void SetBuildShader(ComputeShader shader)
        {
            if (shader == _buildShader && _kernelReduce >= 0 && _kernelBuildCdf >= 0 && _kernelBuildAlias >= 0)
            {
                return;
            }

            _buildShader = shader;
            _kernelReduce = shader != null ? shader.FindKernel("CSReduceLuminance") : -1;
            _kernelBuildCdf = shader != null ? shader.FindKernel("CSBuildCdf") : -1;
            _kernelBuildAlias = shader != null ? shader.FindKernel("CSBuildAlias") : -1;
        }

        public void EnsureBuffers()
        {
            int total = SamplingResolution * SamplingResolution * 6;

            if (_luminanceBuffer == null || _luminanceBuffer.count != total)
            {
                ReleaseBuffer(ref _luminanceBuffer);

                _luminanceBuffer = new ComputeBuffer(total, sizeof(float), ComputeBufferType.Structured);
            }

            if (_cdfBuffer == null || _cdfBuffer.count != total)
            {
                ReleaseBuffer(ref _cdfBuffer);

                _cdfBuffer = new ComputeBuffer(total, sizeof(float), ComputeBufferType.Structured);
            }

            if (_totalLuminanceBuffer == null || _totalLuminanceBuffer.count != 1)
            {
                ReleaseBuffer(ref _totalLuminanceBuffer);

                _totalLuminanceBuffer = new ComputeBuffer(1, sizeof(float), ComputeBufferType.Structured);
            }

            if (_aliasProbBuffer == null || _aliasProbBuffer.count != total)
            {
                ReleaseBuffer(ref _aliasProbBuffer);

                _aliasProbBuffer = new ComputeBuffer(total, sizeof(float), ComputeBufferType.Structured);
            }

            if (_aliasIndexBuffer == null || _aliasIndexBuffer.count != total)
            {
                ReleaseBuffer(ref _aliasIndexBuffer);

                _aliasIndexBuffer = new ComputeBuffer(total, sizeof(uint), ComputeBufferType.Structured);
            }

            if (_aliasSmallBuffer == null || _aliasSmallBuffer.count != total)
            {
                ReleaseBuffer(ref _aliasSmallBuffer);

                _aliasSmallBuffer = new ComputeBuffer(total, sizeof(uint), ComputeBufferType.Structured);
            }

            if (_aliasLargeBuffer == null || _aliasLargeBuffer.count != total)
            {
                ReleaseBuffer(ref _aliasLargeBuffer);

                _aliasLargeBuffer = new ComputeBuffer(total, sizeof(uint), ComputeBufferType.Structured);
            }
        }

        public void Build(CommandBuffer cmd, RenderTexture sourceCubemap)
        {
            EnsureBuffers();

            if (_buildShader == null || _kernelReduce < 0 || _kernelBuildCdf < 0 || _kernelBuildAlias < 0 || sourceCubemap == null)
            {
                _isReady = false;

                return;
            }

            cmd.SetComputeIntParam(_buildShader, _envSamplingResolutionProperty, SamplingResolution);
            cmd.SetComputeTextureParam(_buildShader, _kernelReduce, _sourceCubemapProperty, sourceCubemap);
            cmd.SetComputeBufferParam(_buildShader, _kernelReduce, _envLuminanceProperty, _luminanceBuffer);

            int groups = Mathf.CeilToInt(SamplingResolution / 8f);

            cmd.DispatchCompute(_buildShader, _kernelReduce, groups, groups, 6);

            cmd.SetComputeBufferParam(_buildShader, _kernelBuildCdf, _envLuminanceProperty, _luminanceBuffer);
            cmd.SetComputeBufferParam(_buildShader, _kernelBuildCdf, _envCdfProperty, _cdfBuffer);
            cmd.SetComputeBufferParam(_buildShader, _kernelBuildCdf, _envTotalLuminanceProperty, _totalLuminanceBuffer);
            cmd.DispatchCompute(_buildShader, _kernelBuildCdf, 1, 1, 1);

            cmd.SetComputeBufferParam(_buildShader, _kernelBuildAlias, _envLuminanceProperty, _luminanceBuffer);
            cmd.SetComputeBufferParam(_buildShader, _kernelBuildAlias, _envTotalLuminanceProperty, _totalLuminanceBuffer);
            cmd.SetComputeBufferParam(_buildShader, _kernelBuildAlias, _envAliasProbProperty, _aliasProbBuffer);
            cmd.SetComputeBufferParam(_buildShader, _kernelBuildAlias, _envAliasIndexProperty, _aliasIndexBuffer);
            cmd.SetComputeBufferParam(_buildShader, _kernelBuildAlias, _envAliasSmallProperty, _aliasSmallBuffer);
            cmd.SetComputeBufferParam(_buildShader, _kernelBuildAlias, _envAliasLargeProperty, _aliasLargeBuffer);
            cmd.DispatchCompute(_buildShader, _kernelBuildAlias, 1, 1, 1);

            _isReady = true;
        }

        public void BindGlobals(CommandBuffer cmd)
        {
            EnsureBuffers();
            cmd.SetGlobalBuffer(EnvLuminanceBufferProperty, _luminanceBuffer);
            cmd.SetGlobalBuffer(EnvCdfBufferProperty, _cdfBuffer);
            cmd.SetGlobalBuffer(EnvTotalLuminanceBufferProperty, _totalLuminanceBuffer);
            cmd.SetGlobalBuffer(EnvAliasProbBufferProperty, _aliasProbBuffer);
            cmd.SetGlobalBuffer(EnvAliasIndexBufferProperty, _aliasIndexBuffer);
            cmd.SetGlobalInt(EnvSamplingResolutionProperty, SamplingResolution);
            cmd.SetGlobalInt(EnvSamplingReadyProperty, _isReady ? 1 : 0);
        }

        public void BindKernel(CommandBuffer cmd, ComputeShader shader, int kernel)
        {
            EnsureBuffers();
            cmd.SetComputeBufferParam(shader, kernel, EnvLuminanceBufferProperty, _luminanceBuffer);
            cmd.SetComputeBufferParam(shader, kernel, EnvCdfBufferProperty, _cdfBuffer);
            cmd.SetComputeBufferParam(shader, kernel, EnvTotalLuminanceBufferProperty, _totalLuminanceBuffer);
            cmd.SetComputeBufferParam(shader, kernel, EnvAliasProbBufferProperty, _aliasProbBuffer);
            cmd.SetComputeBufferParam(shader, kernel, EnvAliasIndexBufferProperty, _aliasIndexBuffer);
            cmd.SetComputeIntParam(shader, EnvSamplingResolutionProperty, SamplingResolution);
            cmd.SetComputeIntParam(shader, EnvSamplingReadyProperty, _isReady ? 1 : 0);
        }

        public void Dispose()
        {
            ReleaseBuffer(ref _luminanceBuffer);
            ReleaseBuffer(ref _cdfBuffer);
            ReleaseBuffer(ref _totalLuminanceBuffer);
            ReleaseBuffer(ref _aliasProbBuffer);
            ReleaseBuffer(ref _aliasIndexBuffer);
            ReleaseBuffer(ref _aliasSmallBuffer);
            ReleaseBuffer(ref _aliasLargeBuffer);

            _isReady = false;
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
