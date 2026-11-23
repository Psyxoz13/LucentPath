#pragma once

#include <cstdint>

#include <IUnityGraphics.h>

#if defined(_WIN32)
#define LP_STREAMLINE_API extern "C" __declspec(dllexport)
#else
#define LP_STREAMLINE_API extern "C"
#endif

enum LucentPathStreamlineStatus : int32_t
{
    LPStreamlineStatusOk = 0,
    LPStreamlineStatusInvalidArgument = 10,
    LPStreamlineStatusUnsupportedGraphicsApi = 11,
    LPStreamlineStatusRuntimeLoadFailed = 12,
    LPStreamlineStatusDeviceUnavailable = 13,
    LPStreamlineStatusFeatureUnsupported = 14,
    LPStreamlineStatusFeatureMissing = 15,
    LPStreamlineStatusStreamlineFailed = 16
};

enum LucentPathDlssMode : int32_t
{
    LucentPathDlssModeOff = 0,
    LucentPathDlssModeQuality = 1,
    LucentPathDlssModeBalanced = 2,
    LucentPathDlssModePerformance = 3,
    LucentPathDlssModeUltraPerformance = 4,
    LucentPathDlssModeDlaa = 5
};

struct LucentPathMatrix4x4
{
    float m00, m01, m02, m03;
    float m10, m11, m12, m13;
    float m20, m21, m22, m23;
    float m30, m31, m32, m33;
};

struct LucentPathStreamlineEvaluateDesc
{
    int32_t frameIndex;
    int32_t viewportId;
    int32_t inputWidth;
    int32_t inputHeight;
    int32_t outputWidth;
    int32_t outputHeight;
    int32_t dlssMode;
    int32_t alphaUpscalingEnabled;
    int32_t dlaaPreset;
    int32_t qualityPreset;
    int32_t balancedPreset;
    int32_t performancePreset;
    int32_t ultraPerformancePreset;
    int32_t resetHistory;
    int32_t depthInverted;
    int32_t orthographic;
    float jitterX;
    float jitterY;
    float motionVectorScaleX;
    float motionVectorScaleY;
    float preExposure;
    float exposureScale;
    float cameraNear;
    float cameraFar;
    float cameraFovRadians;
    float cameraAspectRatio;
    float cameraPosition[3];
    float cameraUp[3];
    float cameraRight[3];
    float cameraForward[3];
    void* colorInput;
    void* colorOutput;
    void* depth;
    void* motionVectors;
    void* diffuseAlbedo;
    void* specularAlbedo;
    void* normals;
    void* roughness;
    void* normalRoughness;
    void* specularMotionVectors;
    void* specularHitDistance;
    LucentPathMatrix4x4 cameraViewToClip;
    LucentPathMatrix4x4 clipToCameraView;
    LucentPathMatrix4x4 clipToPrevClip;
    LucentPathMatrix4x4 prevClipToClip;
    LucentPathMatrix4x4 worldToCameraView;
    LucentPathMatrix4x4 cameraViewToWorld;
};

LP_STREAMLINE_API int LPStreamline_Initialize(const wchar_t* runtimeDirectory, const wchar_t* logDirectory, uint32_t applicationId, const wchar_t* projectId, int32_t loadDownloadedPlugins, int32_t allowOverTheAirUpdates);
LP_STREAMLINE_API int LPStreamline_IsDlssRayReconstructionSupported();
LP_STREAMLINE_API void* LPStreamline_CreateEvaluateRequest(const LucentPathStreamlineEvaluateDesc* desc);
LP_STREAMLINE_API void LPStreamline_DestroyEvaluateRequest(void* request);
LP_STREAMLINE_API UnityRenderingEventAndData LPStreamline_GetRenderEventFunc();
LP_STREAMLINE_API int LPStreamline_GetLastError(char* buffer, int bufferLength);
LP_STREAMLINE_API int LPStreamline_GetLastEvaluateStatus(char* buffer, int bufferLength);
LP_STREAMLINE_API void LPStreamline_Shutdown();
