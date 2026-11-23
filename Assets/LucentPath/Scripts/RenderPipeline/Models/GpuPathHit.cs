using System.Runtime.InteropServices;
using UnityEngine;

namespace LucentPath.Rendering
{
    [StructLayout(LayoutKind.Sequential)]
#pragma warning disable 0649
    internal struct GpuPathHit
    {
        public uint DidHit;
        public float T;
        public int TriangleIndex;
        public int InstanceIndex;
        public Vector2 Bary;
    }
#pragma warning restore 0649
}
