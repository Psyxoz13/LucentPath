using System.Runtime.InteropServices;
using UnityEngine;

namespace LucentPath.Rendering
{
#pragma warning disable 0649
    [StructLayout(LayoutKind.Sequential)]
    internal struct GpuStoredHit
    {
        public uint Flags;
        public float T;
        public uint MaterialModel;
        public uint InstanceId;

        public Vector4 PositionPacked;

        public uint PackedNormalsRoughAnisoTangentX;
        public uint PackedNormalsRoughAnisoTangentY;
        public uint PackedNormalsRoughAnisoTangentZ;
        public uint PackedNormalsRoughAnisoTangentW;

        public Vector2 Uv;
        public uint MaterialIndex;
        public uint Reserved;
    }
#pragma warning restore 0649
}
