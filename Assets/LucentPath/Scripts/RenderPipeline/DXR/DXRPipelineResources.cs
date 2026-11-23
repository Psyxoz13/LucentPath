using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LucentPath.Rendering
{
    internal class DXRPipelineResources
    {
        public LucentPathRendererSettings Asset { get; }

        public ComputeKernel ResolveSecondaryGuides;
        public ComputeKernel AccumulateFrame;
        public ComputeKernel AccumulateFrameSpecular;
        public ComputeKernel CheckerboardResolve;
        public ComputeKernel PrepareBuffers;
        public ComputeKernel DecayGuidingField;
        public ComputeKernel BuildLightingAliases;
        public ComputeKernel TaaResolve;
        public ComputeKernel AntiFirefly;
        public ComputeKernel HistoryFix;
        public ComputeKernel AtrousFilter;
        public ComputeKernel ShadeNee;
        public ComputeKernel ShadeAdvance;
        public ComputeKernel ClearPathArgs;
        public ComputeKernel CompactPaths;
        public ComputeKernel BuildShadeArgs;
        public ComputeKernel AccumulateSecondary;
        public ComputeKernel ReflectionPrePass;
        public ComputeKernel ClearSecondaryArgs;
        public ComputeKernel CompactSecondary;
        public ComputeKernel FinalizeSecondaryArgs;
        public ComputeKernel BootstrapSurface;
        public ComputeKernel ClearBootstrapArgs;
        public ComputeKernel FinalizeBootstrapArgs;
        public ComputeKernel[] FrameComputes = Array.Empty<ComputeKernel>();

        public RayTracingShader PathTracingRayGenShader;

        public Material CompositeMaterial;
        public Material CopyMaterial;
        public bool IsCopyMaterialOwned;
        public Material BloomMaterial;
        public Material PostSharpenMaterial;
        public bool IsAgxLutWarningLogged;
        public bool IsAgxPresentationWarningLogged;
        public bool IsBloomProfileWarningLogged;
        public bool IsBloomMaterialWarningLogged;

        public ComputeBuffer LightBuffer;
        public ComputeBuffer SobolMatricesBuffer;
        public ComputeBuffer MaterialCache;
        public ComputeBuffer EmissiveSurfelBuffer;
        public ComputeBuffer EmissiveSurfelCounter;
        public ComputeBuffer EmissiveSurfelOccupancy;

        public RayTracingAccelerationStructure RTAS;

        public int FrameIndex;
        public string LastDlssWarning;
        public bool IsDlssRuntimeFailed;
        public int LastDlssRuntimeConfigurationHash;
        public readonly Dictionary<EntityId, CameraState> CameraStates = new();
        public readonly GpuLight[] DummyLight = new GpuLight[1];

        public DXRPipelineResources(LucentPathRendererSettings asset)
        {
            Asset = asset;
        }
    }
}
