using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using static LucentPath.Rendering.PipelineShaderProperties;

namespace LucentPath.Rendering
{
    internal class DXRSceneGpuUploader
    {
        private readonly DXRPipelineResources _context;

        private static uint[] _sobolMatrices;

        public DXRSceneGpuUploader(DXRPipelineResources context)
        {
            _context = context;
        }

        public void BuildLightsFromCullingResults(CullingResults cullingResults, out GpuLight[] lights, out int analyticAreaLightCount)
        {
            GpuLightBuilder.BuildFromCullingResults(
                cullingResults,
                _context.Asset.PathTracingSettings.MaxLights,
                out lights,
                out analyticAreaLightCount);
        }

        public void UploadLights(GpuLight[] lights)
        {
            var lightData = lights != null && lights.Length > 0 ? lights : _context.DummyLight;

            EnsureBuffer(ref _context.LightBuffer, lightData.Length, Marshal.SizeOf<GpuLight>());
            _context.LightBuffer.SetData(lightData);
        }

        public void EnsureSobolMatricesBuffer()
        {
            if (_context.SobolMatricesBuffer != null)
            {
                return;
            }

            var sobolMatrices = LoadSobolMatrices();

            _context.SobolMatricesBuffer = new ComputeBuffer(sobolMatrices.Length, sizeof(uint), ComputeBufferType.Structured);

            _context.SobolMatricesBuffer.SetData(sobolMatrices);
        }

        public void Dispose()
        {
            ReleaseBuffer(ref _context.LightBuffer);
            ReleaseBuffer(ref _context.SobolMatricesBuffer);
        }

        private static bool EnsureBuffer(ref ComputeBuffer buffer, int count, int stride)
        {
            if (buffer != null && buffer.count == count && buffer.stride == stride)
            {
                return false;
            }

            ReleaseBuffer(ref buffer);

            buffer = new ComputeBuffer(count, stride, ComputeBufferType.Structured);

            return true;
        }

        private static void ReleaseBuffer(ref ComputeBuffer buffer)
        {
            if (buffer == null)
            {
                return;
            }

            buffer.Release();

            buffer = null;
        }

        private static uint[] LoadSobolMatrices()
        {
            var requiredCount = SobolDimensionCount * SobolMatrixSize;

            if (_sobolMatrices != null && _sobolMatrices.Length == requiredCount)
            {
                return _sobolMatrices;
            }

            _sobolMatrices = Sampling.LucentPathSampling.GetSobolMatrices(SobolDimensionCount, SobolMatrixSize);

            return _sobolMatrices;
        }
    }
}
