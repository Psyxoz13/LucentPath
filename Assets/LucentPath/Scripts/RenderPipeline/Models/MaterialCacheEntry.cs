using System.Runtime.InteropServices;

namespace LucentPath.Rendering
{
    [StructLayout(LayoutKind.Sequential)]
#pragma warning disable 0649
    internal struct MaterialCacheEntry
    {
        public uint TransmissionColorPacked;
        public uint EtaTransmissionWeightPacked;
        public uint TransmissionDepthFlagsPacked;
        public uint Reserved;
    }
#pragma warning restore 0649
}
