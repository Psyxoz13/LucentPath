using System.Runtime.InteropServices;
using UnityEngine;

namespace LucentPath.Rendering
{
    [StructLayout(LayoutKind.Sequential)]
#pragma warning disable 0649
    internal struct GpuPathState
    {
        public Vector3 RayOrigin;
        public uint Flags;
        public Vector3 RayDirection;
        public float MaxDistance;
        public float PrevBsdfPdf;
        public float MediumInstanceId;
        public uint PackedCounts;
        public uint ThroughputXY;
        public uint ThroughputZTintX;
        public uint TintYZ;
        public uint RoughGuidePrim;
        public uint MediumDistDielDepth;
    }
#pragma warning restore 0649
}
