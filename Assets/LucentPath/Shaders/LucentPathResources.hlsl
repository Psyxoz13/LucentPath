#ifndef LUCENT_PATH_RESOURCES_INCLUDED
#define LUCENT_PATH_RESOURCES_INCLUDED

#include "Assets/LucentPath/Shaders/LucentPathSecondaryGuideEvent.hlsl"

#if defined(LUCENT_PATH_KERNEL_INIT_PATH) || defined(LUCENT_PATH_KERNEL_DIRECT_LIGHTING) || defined(LUCENT_PATH_KERNEL_ADVANCE_PATH)
#define LUCENT_PATH_NEEDS_PATH_STATE_BUFFERS 1
#endif

#if defined(LUCENT_PATH_KERNEL_DIRECT_LIGHTING) || defined(LUCENT_PATH_KERNEL_ADVANCE_PATH)
#define LUCENT_PATH_NEEDS_PATH_STATE_RESOURCES 1
#endif

StructuredBuffer<GpuLight> _Lights;
StructuredBuffer<uint> _SobolMatricesBuffer;

#ifndef LUCENT_PATH_BLUE_NOISE_DECLARED
#define LUCENT_PATH_BLUE_NOISE_DECLARED
Texture2DArray<float> _BlueNoiseTexture;
int _BlueNoiseEnabled;
int _BlueNoiseLayerCount;
int _BlueNoiseTileWidth;
int _BlueNoiseTileHeight;
#endif

#ifndef LUCENT_PATH_ENVIRONMENT_HDRI_DECLARED
#define LUCENT_PATH_ENVIRONMENT_HDRI_DECLARED
TextureCube<float4> _EnvironmentCubemap;
SamplerState sampler_EnvironmentCubemap;
int _EnvironmentMode;
int _EnvironmentMaxMip;
float _EnvironmentRotationYawCos;
float _EnvironmentRotationYawSin;
#endif

#if defined(LUCENT_PATH_NEEDS_PATH_STATE_BUFFERS)
#if defined(LUCENT_DXR_ONLY)
#include "UnityRayQuery.cginc"
RaytracingAccelerationStructure _RayTracingAccelerationStructure;
#endif
#endif

#if defined(LUCENT_PATH_KERNEL_DIRECT_LIGHTING)
RWTexture2D<float4> _ShadowGuide0;
RWTexture2D<float4> _ShadowGuide1;
RWTexture2D<float4> _ShadowGuide2;
RWTexture2D<float4> _FrameShadowSum;

void SetFrameShadowChannel(uint2 pixel, uint channel, float value)
{
    float4 shadowSums = _FrameShadowSum[pixel];
    shadowSums[channel] = value;
    _FrameShadowSum[pixel] = shadowSums;
}
#endif

#if defined(LUCENT_PATH_KERNEL_ACCUMULATE_FRAME)
RWTexture2D<float4> _Moments;
RWTexture2D<float4> _SurfaceMoments;
#if defined(LUCENT_ACCUMULATE_SURFACE)
Texture2D<float4> _ReflectionMoments;
#else
RWTexture2D<float4> _ReflectionMoments;
#endif
#if defined(LUCENT_ACCUMULATE_SURFACE)
Texture2D<float4> _TransmissionMoments;
#else
RWTexture2D<float4> _TransmissionMoments;
#endif

Texture2D<float4> _PrevAccum;
Texture2D<float4> _PrevMoments;
Texture2D<float4> _PrevSurfaceMoments;
Texture2D<float4> _PrevReflectionMoments;
Texture2D<float4> _PrevTransmissionMoments;

Texture2D<float4> _Guide0;
Texture2D<float4> _Guide1;
Texture2D<float4> _Guide2;
Texture2D<float4> _ReflectionGuide0;
Texture2D<float4> _ReflectionGuide1;
Texture2D<float4> _ReflectionGuide2;
Texture2D<float4> _TransmissionGuide0;
Texture2D<float4> _TransmissionGuide1;
Texture2D<float4> _TransmissionGuide2;
Texture2D<float4> _PrevGuide0;
Texture2D<float4> _PrevGuide1;
Texture2D<float4> _PrevGuide2;
Texture2D<float4> _PrevReflectionGuide0;
Texture2D<float4> _PrevReflectionGuide1;
Texture2D<float4> _PrevReflectionGuide2;
Texture2D<float4> _PrevTransmissionGuide0;
Texture2D<float4> _PrevTransmissionGuide1;
Texture2D<float4> _PrevTransmissionGuide2;
Texture2D<float4> _ShadowGuide0;
Texture2D<float4> _ShadowGuide1;
Texture2D<float4> _ShadowGuide2;
Texture2D<float4> _PrevShadowGuide0;
Texture2D<float4> _PrevShadowGuide1;
Texture2D<float4> _PrevShadowGuide2;

Texture2D<float4> _FrameSurfaceSignalSum;
Texture2D<float4> _FrameReflectionSignalSum;
Texture2D<float4> _FrameTransmissionSignalSum;
Texture2D<float4> _FrameEmissionSum;
Texture2D<float4> _PrimaryDiffuseAlbedo;
Texture2D<float4> _PrimarySpecularF0;
Texture2D<float4> _PrimaryTransmissionTint;
#define LUCENT_PATH_FRAME_SIGNAL_RESOURCES_DECLARED 1

RWTexture2D<float4> _SurfaceSignal;
#if defined(LUCENT_ACCUMULATE_SURFACE)
Texture2D<float4> _ReflectionSignal;
#else
RWTexture2D<float4> _ReflectionSignal;
#endif
#if defined(LUCENT_ACCUMULATE_SURFACE)
Texture2D<float4> _TransmissionSignal;
#else
RWTexture2D<float4> _TransmissionSignal;
#endif
RWTexture2D<float4> _SurfaceYoungHistorySignal;
#if defined(LUCENT_ACCUMULATE_SURFACE)
Texture2D<float4> _ReflectionYoungHistorySignal;
#else
RWTexture2D<float4> _ReflectionYoungHistorySignal;
#endif
#if defined(LUCENT_ACCUMULATE_SURFACE)
Texture2D<float4> _TransmissionYoungHistorySignal;
#else
RWTexture2D<float4> _TransmissionYoungHistorySignal;
#endif
Texture2D<float4> _PrevSurfaceSignal;
Texture2D<float4> _PrevReflectionSignal;
Texture2D<float4> _PrevTransmissionSignal;
Texture2D<float> _PrevHitMask;
Texture2D<float> _HitMask;
Texture2D<float> _PrevShadowHitMask;
RWTexture2D<float> _ShadowHitMask;
Texture2D<float4> _FrameShadowSum;
Texture2D<float> _PrevReflectionShadowHitMask;
#if defined(LUCENT_ACCUMULATE_SURFACE)
Texture2D<float> _ReflectionShadowHitMask;
#else
RWTexture2D<float> _ReflectionShadowHitMask;
#endif
Texture2D<float> _PrevTransmissionShadowHitMask;
#if defined(LUCENT_ACCUMULATE_SURFACE)
Texture2D<float> _TransmissionShadowHitMask;
#else
RWTexture2D<float> _TransmissionShadowHitMask;
#endif

Texture2D<float> _PrimaryHitDistance;
Texture2D<float> _PrevPrimaryHitDistance;
Texture2D<float> _ReflectionHitDistance;
Texture2D<float> _PrevReflectionHitDistance;
Texture2D<float> _TransmissionHitDistance;
Texture2D<float> _PrevTransmissionHitDistance;
Texture2D<float> _PrimaryRoughness;
Texture2D<float> _PrevPrimaryRoughness;
#endif

#if defined(LUCENT_PATH_KERNEL_ATROUS_FILTER) || defined(LUCENT_PATH_KERNEL_HISTORY_FIX)
Texture2D<float4> _Moments;
Texture2D<float4> _SurfaceMoments;
Texture2D<float4> _ReflectionMoments;
Texture2D<float4> _TransmissionMoments;
Texture2D<float4> _Guide0;
Texture2D<float4> _Guide1;
Texture2D<float4> _Guide2;
Texture2D<float> _PrimaryRoughness;
Texture2D<float4> _YoungHistorySignal;
#endif

#if defined(LUCENT_PATH_KERNEL_HISTORY_FIX) || defined(LUCENT_PATH_KERNEL_ATROUS_FILTER)
Texture2D<float4> _HistoryFixInput;
RWTexture2D<float4> _HistoryFixOutput;
Texture2D<float> _HistoryFixHitDistance;
#endif

#if defined(LUCENT_PATH_KERNEL_ATROUS_FILTER) || defined(LUCENT_PATH_KERNEL_HISTORY_FIX)
Texture2D<float4> _AtrousInput;
RWTexture2D<float4> _AtrousOutput;
#endif

#if defined(LUCENT_PATH_SECONDARY_GUIDE_EVENT_WRITE)
RWTexture2D<uint4> _ReflectionGuideEvent0;
RWTexture2D<uint4> _ReflectionGuideEvent1;
RWTexture2D<uint4> _ReflectionGuideEvent2;
RWTexture2D<uint4> _ReflectionGuideEvent3;
RWTexture2D<uint4> _TransmissionGuideEvent0;
RWTexture2D<uint4> _TransmissionGuideEvent1;
RWTexture2D<uint4> _TransmissionGuideEvent2;
RWTexture2D<uint4> _TransmissionGuideEvent3;
#endif

#if defined(LUCENT_PATH_SECONDARY_GUIDE_EVENT_READ)
Texture2D<uint4> _ReflectionGuideEvent0;
Texture2D<uint4> _ReflectionGuideEvent1;
Texture2D<uint4> _ReflectionGuideEvent2;
Texture2D<uint4> _ReflectionGuideEvent3;
Texture2D<uint4> _TransmissionGuideEvent0;
Texture2D<uint4> _TransmissionGuideEvent1;
Texture2D<uint4> _TransmissionGuideEvent2;
Texture2D<uint4> _TransmissionGuideEvent3;
Texture2D<uint4> _PrevReflectionGuideEvent0;
Texture2D<uint4> _PrevReflectionGuideEvent1;
Texture2D<uint4> _PrevReflectionGuideEvent2;
Texture2D<uint4> _PrevReflectionGuideEvent3;
Texture2D<uint4> _PrevTransmissionGuideEvent0;
Texture2D<uint4> _PrevTransmissionGuideEvent1;
Texture2D<uint4> _PrevTransmissionGuideEvent2;
Texture2D<uint4> _PrevTransmissionGuideEvent3;
#endif

#if defined(LUCENT_PATH_KERNEL_RESOLVE_SECONDARY_GUIDES)
Texture2D<float4> _FrameReflectionSignalSum;
Texture2D<float4> _FrameTransmissionSignalSum;
Texture2D<float4> _Guide0;
Texture2D<float4> _Guide1;
Texture2D<float4> _Guide2;
Texture2D<float> _PrimaryHitDistance;
Texture2D<float> _PrimaryRoughness;
RWTexture2D<float4> _ReflectionGuide0;
RWTexture2D<float4> _ReflectionGuide1;
RWTexture2D<float4> _ReflectionGuide2;
RWTexture2D<float4> _TransmissionGuide0;
RWTexture2D<float4> _TransmissionGuide1;
RWTexture2D<float4> _TransmissionGuide2;
#endif

int _Width;
int _Height;
int _LightCount;
int _AnalyticAreaLightCount;
int _SamplesPerPixel;
int _FrameIndex;
int _AccumulationSampleCount;
int _SurfaceBounces;
int _ReflectionBounces;
int _TransmissionBounces;
int _RussianRouletteStart;
int _FlipY;
int _HasValidHistory;
int _MaxHistoryLength;
int _AtrousStepSize;
float _BounceMaxDistance;
float _SurfaceBias;
float _DirectIntensity;
int _DebugVisualizationMode;
float _TemporalDepthRejectThreshold;
float _TemporalNormalRejectThreshold;
float _MotionRejectionStrength;
float _AtrousDepthPhi;
float _AtrousNormalPhi;
float _HistoryDepthConfidenceScale;
float _HistoryNormalConfidencePower;
float _HistoryConfidencePower;
float _HistoryConfidenceDitherStrength;
float _HistoryColorClipSigma;
float _LowConfidenceSpatialBlend;
int _SpatialBootstrapRadius;
float _SpatialBootstrapDepthRejectThreshold;
float _SpatialBootstrapNormalThreshold;
float _SpatialBootstrapDepthPhi;
float _SpatialBootstrapNormalPhi;
float _HistoryMissSpatialBlend;
float _SpatialBootstrapLuminancePhi;
float _SpatialBootstrapLuminanceFloor;
float _SpatialBootstrapVarianceCutoff;
float _SpatialBootstrapVarianceMinBlend;
int _YoungHistoryFrames;
int _YoungHistoryRadius;
float _YoungHistoryBlend;
float _YoungHistoryVarianceBoost;
float _GuideMinSignalWeight;
float _GuidePlanarNormalThreshold;
float _GuideMaxVirtualProjectionErrorPixels;
float _GuideMaxPlaneMotionMeters;
float _ShadowAntiLagSensitivity;
float _ShadowAntiLagWarmup;
int _ShadowAntiLagRadius;
float4x4 _InvViewProjMatrix;
float4x4 _NonJitteredViewProjMatrix;
float4x4 _ViewMatrix;
float4x4 _PrevViewProjMatrix;
float4x4 _PrevViewMatrix;
float4 _CameraWorldPos;
float4 _PrevCameraWorldPos;
float _NearClipNdc;
float _FarClipNdc;
int _IsOrthographic;
#define LUCENT_PATH_CAMERA_LENS_CONSTANTS_DECLARED 1
float _CameraApertureRadius;
float _CameraFocusDistance;
int _CameraBladeCount;
float _CameraApertureRotation;
float _CameraAnamorphism;
float4 _EnvironmentTopColor;
float4 _EnvironmentBottomColor;
float _EnvironmentIntensity;
float _EnvironmentExponent;

#endif
