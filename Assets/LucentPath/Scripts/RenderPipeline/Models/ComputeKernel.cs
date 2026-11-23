using UnityEngine;

namespace LucentPath.Rendering
{
    internal readonly struct ComputeKernel
    {
        public readonly ComputeShader Compute;
        public readonly int Kernel;

        public ComputeKernel(ComputeShader compute, int kernel)
        {
            Compute = compute;
            Kernel = kernel;
        }
    }
}
