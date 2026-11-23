using System.Runtime.InteropServices;
using UnityEngine;

namespace LucentPath.Rendering
{
    [StructLayout(LayoutKind.Sequential)]
    public struct GpuLight
    {
        public Vector4 PositionRange;
        public Vector4 DirectionType;
        public Vector4 ColorIntensity;
        public Vector4 SpotAngles;
        public Vector4 AreaData;
        public Vector4 Right;
        public Vector4 Up;
    }
}
