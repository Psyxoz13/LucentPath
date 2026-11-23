using System.Runtime.InteropServices;
using UnityEngine;

namespace LucentPath.Rendering
{
    [StructLayout(LayoutKind.Sequential)]
    public struct GpuEmissiveSurfel
    {
        public Vector4 PositionPower;
        public Vector4 Normal;
        public Vector4 Emission;
    }
}
