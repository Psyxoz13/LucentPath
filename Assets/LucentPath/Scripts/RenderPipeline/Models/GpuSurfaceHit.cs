using System.Runtime.InteropServices;
using UnityEngine;

namespace LucentPath.Rendering
{
#pragma warning disable 0649
    [StructLayout(LayoutKind.Sequential)]
    internal struct GpuSurfaceHit
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

        public Vector4 PrevPositionFlags;
        public Vector4 UvData;

        public uint MatSlot0X;
        public uint MatSlot0Y;
        public uint MatSlot0Z;
        public uint MatSlot0W;

        public uint MatSlot1X;
        public uint MatSlot1Y;
        public uint MatSlot1Z;
        public uint MatSlot1W;

        public uint MatSlot2X;
        public uint MatSlot2Y;
        public uint MatSlot2Z;
        public uint MatSlot2W;

        public uint MatSlot3X;
        public uint MatSlot3Y;
        public uint MatSlot3Z;
        public uint MatSlot3W;
    }
#pragma warning restore 0649
}
