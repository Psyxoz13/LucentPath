using UnityEngine.Rendering;

namespace LucentPath.Rendering
{
    internal class DXRDenoisePass
    {
        private readonly DXRPipelineResources _context;
        private readonly DXRKernelLibrary _kernels;

        public DXRDenoisePass(DXRPipelineResources context, DXRKernelLibrary kernels)
        {
            _context = context;
            _kernels = kernels;
        }

        public void Dispatch(CommandBuffer cmd, CameraState state, int groupsX, int groupsY)
        {
            DenoisePassCore.Dispatch(
                cmd,
                state,
                _context.Asset,
                _context.HistoryFix,
                _context.AtrousFilter,
                _context.BuildLightingAliases,
                _kernels.BindLightingAliasResources,
                groupsX,
                groupsY);
        }
    }
}
