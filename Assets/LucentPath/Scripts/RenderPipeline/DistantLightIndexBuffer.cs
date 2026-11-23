using UnityEngine;
using UnityEngine.Rendering;

namespace LucentPath.Rendering
{
    internal class DistantLightIndexBuffer : System.IDisposable
    {
        private ComputeBuffer _indexBuffer;
        private uint[] _indices;

        private int _distantLightCount;

        public void Build(GpuLight[] lights)
        {
            int count = lights != null ? lights.Length : 0;

            EnsureCapacity(count);

            _distantLightCount = CollectDistantLightIndices(lights, _indices);

            _indexBuffer.SetData(_indices);
        }

        public void BindGlobals(CommandBuffer cmd)
        {
            if (_indexBuffer == null)
            {
                EnsureCapacity(0);
            }

            cmd.SetGlobalBuffer(PipelineShaderProperties.DistantLightIndicesProperty, _indexBuffer);
            cmd.SetGlobalInt(PipelineShaderProperties.DistantLightCountProperty, _distantLightCount);
        }

        public void Dispose()
        {
            if (_indexBuffer == null)
            {
                return;
            }

            _indexBuffer.Release();

            _indexBuffer = null;
        }

        internal static int CollectDistantLightIndices(GpuLight[] lights, uint[] indices)
        {
            int count = lights != null ? lights.Length : 0;

            if (indices == null || indices.Length < Mathf.Max(count, 1))
            {
                throw new System.ArgumentException("Index array must be at least max(lightCount, 1) elements long.", nameof(indices));
            }

            System.Array.Clear(indices, 0, indices.Length);

            int distantLightCount = 0;

            for (int i = 0; i < count; i++)
            {
                if (IsDistantLight(lights[i]) == false || Lighting.LightPowerEstimator.ComputeSingleLightPower(lights[i]) <= 0f)
                {
                    continue;
                }

                indices[distantLightCount] = (uint)i;

                distantLightCount++;
            }

            return distantLightCount;
        }

        private static bool IsDistantLight(GpuLight light)
        {
            return Mathf.Approximately(light.DirectionType.w, PipelineShaderProperties.LightTypeDirectional);
        }

        private void EnsureCapacity(int lightCount)
        {
            int count = Mathf.Max(lightCount, 1);

            if (_indices == null || _indices.Length != count)
            {
                _indices = new uint[count];
            }

            if (_indexBuffer != null && _indexBuffer.count == count)
            {
                return;
            }

            Dispose();

            _indexBuffer = new ComputeBuffer(count, sizeof(uint), ComputeBufferType.Structured);
        }
    }
}
