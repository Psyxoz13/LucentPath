using System.Runtime.InteropServices;
using UnityEngine;

namespace LucentPath.Rendering
{
#pragma warning disable 0649
    [StructLayout(LayoutKind.Sequential)]
    internal struct GpuMaterialRow
    {
        public Vector4 BaseColorFactor;
        public Vector4 SpecularColorWeight;
        public Vector4 TransmissionColorWeight;
        public Vector4 CoatColorWeight;
        public Vector4 EmissionFactorMetalness;
        public Vector4 FuzzColorWeight;
        public Vector4 SubsurfaceColorWeight;
        public Vector4 CoatExtrasOpacity;
        public Vector4 ScalarsA;
        public Vector4 ScalarsB;
        public Vector4 Iors;
        public Vector4 ConductorEtaThinFilmThickness;
        public Vector4 ConductorKTransmissionDepth;
        public Vector4 ConductorEdgeTintReserved;
        public Vector4 SubsurfaceRadiusScale;
        public Vector4 BaseMapST;
        public Vector4 EmissionMapST;
        public uint LayerBase;
        public uint LayerMaterialMap;
        public uint LayerEmission;
        public uint RowFlags;
        public uint MaterialModel;
        public uint Reserved0;
        public uint Reserved1;
        public uint Reserved2;
    }
#pragma warning restore 0649
}
