#include "LucentPathStreamline.h"

#include <Windows.h>
#include <d3d12.h>
#include <dxgi1_6.h>
#include <wrl/client.h>

#include <sl.h>
#include <sl_dlss_d.h>
#include <sl_helpers.h>

#include <IUnityGraphics.h>
#include <IUnityGraphicsD3D12.h>
#include <IUnityInterface.h>

#include <array>
#include <cstdio>
#include <mutex>
#include <string>
#include <vector>

using Microsoft::WRL::ComPtr;

namespace
{
    constexpr int EventEvaluateDlssRR = 1;

    IUnityInterfaces* g_unity = nullptr;
    IUnityGraphics* g_graphics = nullptr;
    IUnityGraphicsD3D12v8* g_d3d12 = nullptr;
    std::mutex g_mutex;
    std::wstring g_runtimeDirectory;
    std::wstring g_logDirectory;
    std::string g_projectIdUtf8;
    std::string g_lastError;
    std::string g_lastEvaluateError;
    int g_lastEvaluateStatus = 0;
    bool g_slInitialized = false;
    bool g_deviceSet = false;
    bool g_dlssRrSupported = false;

    void SetLastError(const char* message)
    {
        g_lastError = message != nullptr ? message : "Unknown LucentPath Streamline error.";
    }

    std::string WideToUtf8(const wchar_t* text)
    {
        if (text == nullptr || text[0] == L'\0')
            return {};

        int size = WideCharToMultiByte(CP_UTF8, 0, text, -1, nullptr, 0, nullptr, nullptr);
        if (size <= 1)
            return {};

        std::string result(static_cast<size_t>(size), '\0');
        WideCharToMultiByte(CP_UTF8, 0, text, -1, result.data(), size, nullptr, nullptr);
        result.resize(static_cast<size_t>(size - 1));
        return result;
    }

    const char* ResultName(sl::Result result)
    {
        switch (result)
        {
        case sl::Result::eOk: return "Ok";
        case sl::Result::eErrorDriverOutOfDate: return "ErrorDriverOutOfDate";
        case sl::Result::eErrorOSOutOfDate: return "ErrorOSOutOfDate";
        case sl::Result::eErrorOSDisabledHWS: return "ErrorOSDisabledHWS";
        case sl::Result::eErrorDeviceNotCreated: return "ErrorDeviceNotCreated";
        case sl::Result::eErrorNoSupportedAdapterFound: return "ErrorNoSupportedAdapterFound";
        case sl::Result::eErrorAdapterNotSupported: return "ErrorAdapterNotSupported";
        case sl::Result::eErrorNoPlugins: return "ErrorNoPlugins";
        case sl::Result::eErrorD3DAPI: return "ErrorD3DAPI";
        case sl::Result::eErrorNGXFailed: return "ErrorNGXFailed";
        case sl::Result::eErrorMissingProxy: return "ErrorMissingProxy";
        case sl::Result::eErrorMissingResourceState: return "ErrorMissingResourceState";
        case sl::Result::eErrorInvalidIntegration: return "ErrorInvalidIntegration";
        case sl::Result::eErrorMissingInputParameter: return "ErrorMissingInputParameter";
        case sl::Result::eErrorNotInitialized: return "ErrorNotInitialized";
        case sl::Result::eErrorInvalidParameter: return "ErrorInvalidParameter";
        case sl::Result::eErrorMissingConstants: return "ErrorMissingConstants";
        case sl::Result::eErrorMissingOrInvalidAPI: return "ErrorMissingOrInvalidAPI";
        case sl::Result::eErrorCommonConstantsMissing: return "ErrorCommonConstantsMissing";
        case sl::Result::eErrorUnsupportedInterface: return "ErrorUnsupportedInterface";
        case sl::Result::eErrorFeatureMissing: return "ErrorFeatureMissing";
        case sl::Result::eErrorFeatureNotSupported: return "ErrorFeatureNotSupported";
        case sl::Result::eErrorFeatureFailedToLoad: return "ErrorFeatureFailedToLoad";
        case sl::Result::eErrorFeatureMissingDependency: return "ErrorFeatureMissingDependency";
        case sl::Result::eErrorInvalidState: return "ErrorInvalidState";
        case sl::Result::eWarnOutOfVRAM: return "WarnOutOfVRAM";
        default: return "Unknown";
        }
    }

    void SetStreamlineError(const char* operation, sl::Result result)
    {
        char buffer[512] = {};
        std::snprintf(buffer, sizeof(buffer), "%s failed with %s (%d).", operation, ResultName(result), static_cast<int>(result));
        SetLastError(buffer);
    }

    sl::float4x4 ToSlMatrix(const LucentPathMatrix4x4& m)
    {
        sl::float4x4 result{};
        result.row[0] = { m.m00, m.m01, m.m02, m.m03 };
        result.row[1] = { m.m10, m.m11, m.m12, m.m13 };
        result.row[2] = { m.m20, m.m21, m.m22, m.m23 };
        result.row[3] = { m.m30, m.m31, m.m32, m.m33 };
        return result;
    }

    sl::DLSSMode ToDlssMode(int32_t mode)
    {
        switch (mode)
        {
        case LucentPathDlssModeQuality: return sl::DLSSMode::eMaxQuality;
        case LucentPathDlssModeBalanced: return sl::DLSSMode::eBalanced;
        case LucentPathDlssModePerformance: return sl::DLSSMode::eMaxPerformance;
        case LucentPathDlssModeUltraPerformance: return sl::DLSSMode::eUltraPerformance;
        case LucentPathDlssModeDlaa: return sl::DLSSMode::eDLAA;
        case LucentPathDlssModeOff:
        default: return sl::DLSSMode::eOff;
        }
    }

    sl::DLSSDPreset ToDlssDPreset(int32_t preset)
    {
        switch (preset)
        {
        case static_cast<int32_t>(sl::DLSSDPreset::ePresetD): return sl::DLSSDPreset::ePresetD;
        case static_cast<int32_t>(sl::DLSSDPreset::ePresetE): return sl::DLSSDPreset::ePresetE;
        case static_cast<int32_t>(sl::DLSSDPreset::eDefault):
        default: return sl::DLSSDPreset::eDefault;
        }
    }

    bool EnsureD3D12DeviceSet()
    {
        if (!g_slInitialized)
        {
            SetLastError("Streamline is not initialized.");
            return false;
        }

        if (g_deviceSet)
            return true;

        if (g_graphics == nullptr || g_graphics->GetRenderer() != kUnityGfxRendererD3D12 || g_d3d12 == nullptr)
        {
            SetLastError("LucentPath Streamline requires Unity Direct3D12.");
            return false;
        }

        ID3D12Device* device = g_d3d12->GetDevice();
        if (device == nullptr)
        {
            SetLastError("Unity D3D12 device is unavailable.");
            return false;
        }

        sl::Result result = slSetD3DDevice(device);
        if (result != sl::Result::eOk)
        {
            SetStreamlineError("slSetD3DDevice", result);
            return false;
        }

        g_deviceSet = true;
        return true;
    }

    bool RefreshDlssRrSupport()
    {
        if (!EnsureD3D12DeviceSet())
            return false;

        ID3D12Device* device = g_d3d12->GetDevice();
        LUID luid = device->GetAdapterLuid();
        sl::AdapterInfo adapterInfo{};
        adapterInfo.deviceLUID = reinterpret_cast<uint8_t*>(&luid);
        adapterInfo.deviceLUIDSizeInBytes = sizeof(luid);

        sl::Result result = slIsFeatureSupported(sl::kFeatureDLSS_RR, adapterInfo);
        if (result != sl::Result::eOk)
        {
            g_dlssRrSupported = false;
            SetStreamlineError("slIsFeatureSupported(kFeatureDLSS_RR)", result);
            return false;
        }

        bool loaded = false;
        result = slIsFeatureLoaded(sl::kFeatureDLSS_RR, loaded);
        if (result != sl::Result::eOk || !loaded)
        {
            g_dlssRrSupported = false;
            SetStreamlineError("slIsFeatureLoaded(kFeatureDLSS_RR)", result);
            return false;
        }

        g_dlssRrSupported = true;
        return true;
    }

    void FillResource(sl::Resource& resource, void* nativeResource, uint32_t state, uint32_t width, uint32_t height)
    {
        resource = sl::Resource(sl::ResourceType::eTex2d, nativeResource, state);
        resource.width = width;
        resource.height = height;
    }

    bool AddTag(
        std::vector<sl::Resource>& resources,
        std::vector<sl::ResourceTag>& tags,
        void* nativeResource,
        sl::BufferType type,
        const sl::Extent& extent,
        uint32_t state)
    {
        if (nativeResource == nullptr)
            return false;

        resources.emplace_back(sl::ResourceType::eTex2d, nativeResource, state);
        resources.back().width = extent.width;
        resources.back().height = extent.height;
        tags.emplace_back(&resources.back(), type, sl::ResourceLifecycle::eOnlyValidNow, &extent);
        return true;
    }

    bool EvaluateDlssRR(const LucentPathStreamlineEvaluateDesc& desc)
    {
        if (!RefreshDlssRrSupport())
            return false;

        if (desc.inputWidth <= 0 || desc.inputHeight <= 0 || desc.outputWidth <= 0 || desc.outputHeight <= 0)
        {
            SetLastError("DLSS-RR evaluate received invalid dimensions.");
            return false;
        }

        bool hasPackedNormalRoughness = desc.normalRoughness != nullptr;
        bool hasUnpackedNormalRoughness = desc.normals != nullptr && desc.roughness != nullptr;
        if (desc.colorInput == nullptr || desc.colorOutput == nullptr || desc.depth == nullptr ||
            desc.motionVectors == nullptr || desc.diffuseAlbedo == nullptr || desc.specularAlbedo == nullptr ||
            (!hasPackedNormalRoughness && !hasUnpackedNormalRoughness))
        {
            SetLastError("DLSS-RR requires color input/output, depth, motion vectors, diffuse/specular albedo, and either packed normal+roughness or separate normals and roughness textures.");
            return false;
        }

        UnityGraphicsD3D12RecordingState recordingState{};
        if (g_d3d12 == nullptr || !g_d3d12->CommandRecordingState(&recordingState) || recordingState.commandList == nullptr)
        {
            SetLastError("Unity D3D12 command recording state is unavailable for Streamline evaluation.");
            return false;
        }

        uint32_t frameIndex = desc.frameIndex >= 0 ? static_cast<uint32_t>(desc.frameIndex) : 0u;
        sl::FrameToken* frameToken = nullptr;
        sl::Result result = slGetNewFrameToken(frameToken, &frameIndex);
        if (result != sl::Result::eOk || frameToken == nullptr)
        {
            SetStreamlineError("slGetNewFrameToken", result);
            return false;
        }

        sl::ViewportHandle viewport(desc.viewportId > 0 ? desc.viewportId : 1);

        sl::Constants constants{};
        constants.cameraViewToClip = ToSlMatrix(desc.cameraViewToClip);
        constants.clipToCameraView = ToSlMatrix(desc.clipToCameraView);
        constants.clipToPrevClip = ToSlMatrix(desc.clipToPrevClip);
        constants.prevClipToClip = ToSlMatrix(desc.prevClipToClip);
        constants.jitterOffset = { desc.jitterX, desc.jitterY };
        constants.mvecScale = { desc.motionVectorScaleX, desc.motionVectorScaleY };
        constants.cameraPos = { desc.cameraPosition[0], desc.cameraPosition[1], desc.cameraPosition[2] };
        constants.cameraUp = { desc.cameraUp[0], desc.cameraUp[1], desc.cameraUp[2] };
        constants.cameraRight = { desc.cameraRight[0], desc.cameraRight[1], desc.cameraRight[2] };
        constants.cameraFwd = { desc.cameraForward[0], desc.cameraForward[1], desc.cameraForward[2] };
        constants.cameraNear = desc.cameraNear;
        constants.cameraFar = desc.cameraFar;
        constants.cameraFOV = desc.cameraFovRadians;
        constants.cameraAspectRatio = desc.cameraAspectRatio;
        constants.depthInverted = desc.depthInverted != 0 ? sl::Boolean::eTrue : sl::Boolean::eFalse;
        constants.cameraMotionIncluded = sl::Boolean::eTrue;
        constants.motionVectors3D = sl::Boolean::eFalse;
        constants.reset = desc.resetHistory != 0 ? sl::Boolean::eTrue : sl::Boolean::eFalse;
        constants.orthographicProjection = desc.orthographic != 0 ? sl::Boolean::eTrue : sl::Boolean::eFalse;
        constants.motionVectorsDilated = sl::Boolean::eFalse;
        constants.motionVectorsJittered = sl::Boolean::eFalse;

        result = slSetConstants(constants, *frameToken, viewport);
        if (result != sl::Result::eOk)
        {
            SetStreamlineError("slSetConstants", result);
            return false;
        }

        sl::DLSSDOptions options{};
        options.mode = ToDlssMode(desc.dlssMode);
        options.outputWidth = static_cast<uint32_t>(desc.outputWidth);
        options.outputHeight = static_cast<uint32_t>(desc.outputHeight);
        options.preExposure = desc.preExposure > 0.0f ? desc.preExposure : 1.0f;
        options.exposureScale = desc.exposureScale > 0.0f ? desc.exposureScale : 1.0f;
        options.colorBuffersHDR = sl::Boolean::eTrue;
        options.normalRoughnessMode = hasPackedNormalRoughness ? sl::DLSSDNormalRoughnessMode::ePacked : sl::DLSSDNormalRoughnessMode::eUnpacked;
        options.alphaUpscalingEnabled = desc.alphaUpscalingEnabled != 0 ? sl::Boolean::eTrue : sl::Boolean::eFalse;
        options.dlaaPreset = ToDlssDPreset(desc.dlaaPreset);
        options.qualityPreset = ToDlssDPreset(desc.qualityPreset);
        options.balancedPreset = ToDlssDPreset(desc.balancedPreset);
        options.performancePreset = ToDlssDPreset(desc.performancePreset);
        options.ultraPerformancePreset = ToDlssDPreset(desc.ultraPerformancePreset);
        options.worldToCameraView = ToSlMatrix(desc.worldToCameraView);
        options.cameraViewToWorld = ToSlMatrix(desc.cameraViewToWorld);

        result = slDLSSDSetOptions(viewport, options);
        if (result != sl::Result::eOk)
        {
            SetStreamlineError("slDLSSDSetOptions", result);
            return false;
        }

        sl::Extent inputExtent{ 0u, 0u, static_cast<uint32_t>(desc.inputWidth), static_cast<uint32_t>(desc.inputHeight) };
        sl::Extent outputExtent{ 0u, 0u, static_cast<uint32_t>(desc.outputWidth), static_cast<uint32_t>(desc.outputHeight) };
        constexpr uint32_t state = D3D12_RESOURCE_STATE_UNORDERED_ACCESS;

        std::vector<sl::Resource> resources;
        resources.reserve(9);
        std::vector<sl::ResourceTag> tags;
        tags.reserve(9);

        AddTag(resources, tags, desc.colorInput, sl::kBufferTypeScalingInputColor, inputExtent, state);
        AddTag(resources, tags, desc.colorOutput, sl::kBufferTypeScalingOutputColor, outputExtent, state);
        AddTag(resources, tags, desc.depth, sl::kBufferTypeDepth, inputExtent, state);
        AddTag(resources, tags, desc.motionVectors, sl::kBufferTypeMotionVectors, inputExtent, state);
        AddTag(resources, tags, desc.diffuseAlbedo, sl::kBufferTypeAlbedo, inputExtent, state);
        AddTag(resources, tags, desc.specularAlbedo, sl::kBufferTypeSpecularAlbedo, inputExtent, state);
        if (hasPackedNormalRoughness)
        {
            AddTag(resources, tags, desc.normalRoughness, sl::kBufferTypeNormalRoughness, inputExtent, state);
        }
        else
        {
            AddTag(resources, tags, desc.normals, sl::kBufferTypeNormals, inputExtent, state);
            AddTag(resources, tags, desc.roughness, sl::kBufferTypeRoughness, inputExtent, state);
        }
        AddTag(resources, tags, desc.specularMotionVectors, sl::kBufferTypeSpecularMotionVectors, inputExtent, state);
        AddTag(resources, tags, desc.specularHitDistance, sl::kBufferTypeSpecularHitDistance, inputExtent, state);

        result = slSetTagForFrame(*frameToken, viewport, tags.data(), static_cast<uint32_t>(tags.size()), recordingState.commandList);
        if (result != sl::Result::eOk)
        {
            SetStreamlineError("slSetTagForFrame", result);
            return false;
        }

        const sl::BaseStructure* inputs[] = { &viewport };
        result = slEvaluateFeature(sl::kFeatureDLSS_RR, *frameToken, inputs, 1, recordingState.commandList);
        if (result != sl::Result::eOk)
        {
            SetStreamlineError("slEvaluateFeature(kFeatureDLSS_RR)", result);
            return false;
        }

        return true;
    }

    void UNITY_INTERFACE_API OnRenderEvent(int eventId, void* data)
    {
        if (eventId != EventEvaluateDlssRR)
            return;

        std::lock_guard<std::mutex> lock(g_mutex);
        g_lastError.clear();

        LucentPathStreamlineEvaluateDesc* request = static_cast<LucentPathStreamlineEvaluateDesc*>(data);
        if (request == nullptr)
        {
            SetLastError("Streamline render event received a null request.");
            return;
        }

        bool evaluated = EvaluateDlssRR(*request);
        g_lastEvaluateStatus = evaluated ? 1 : -1;
        g_lastEvaluateError = evaluated ? std::string{} : g_lastError;
        delete request;
    }

    void UNITY_INTERFACE_API OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType)
    {
        if (eventType == kUnityGfxDeviceEventInitialize)
        {
            if (g_graphics != nullptr && g_graphics->GetRenderer() == kUnityGfxRendererD3D12 && g_unity != nullptr)
            {
                g_d3d12 = g_unity->Get<IUnityGraphicsD3D12v8>();
                if (g_d3d12 != nullptr)
                {
                    UnityD3D12PluginEventConfig config{};
                    config.graphicsQueueAccess = kUnityD3D12GraphicsQueueAccess_DontCare;
                    config.flags = kUnityD3D12EventConfigFlag_FlushCommandBuffers | kUnityD3D12EventConfigFlag_ModifiesCommandBuffersState;
                    config.ensureActiveRenderTextureIsBound = false;
                    g_d3d12->ConfigureEvent(EventEvaluateDlssRR, &config);
                }
            }
        }
        else if (eventType == kUnityGfxDeviceEventShutdown)
        {
            std::lock_guard<std::mutex> lock(g_mutex);
            g_deviceSet = false;
            g_dlssRrSupported = false;
            g_d3d12 = nullptr;
        }
    }
}

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API UnityPluginLoad(IUnityInterfaces* unity)
{
    g_unity = unity;
    g_graphics = unity != nullptr ? unity->Get<IUnityGraphics>() : nullptr;
    if (g_graphics != nullptr)
    {
        g_graphics->RegisterDeviceEventCallback(OnGraphicsDeviceEvent);
        OnGraphicsDeviceEvent(kUnityGfxDeviceEventInitialize);
    }
}

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API UnityPluginUnload()
{
    if (g_graphics != nullptr)
        g_graphics->UnregisterDeviceEventCallback(OnGraphicsDeviceEvent);

    std::lock_guard<std::mutex> lock(g_mutex);
    if (g_slInitialized)
        slShutdown();

    g_slInitialized = false;
    g_deviceSet = false;
    g_dlssRrSupported = false;
    g_d3d12 = nullptr;
    g_graphics = nullptr;
    g_unity = nullptr;
}

LP_STREAMLINE_API int LPStreamline_Initialize(const wchar_t* runtimeDirectory, const wchar_t* logDirectory, uint32_t applicationId, const wchar_t* projectId, int32_t loadDownloadedPlugins, int32_t allowOverTheAirUpdates)
{
    std::lock_guard<std::mutex> lock(g_mutex);
    g_lastError.clear();

    if (g_slInitialized)
        return LPStreamlineStatusOk;

    g_runtimeDirectory = runtimeDirectory != nullptr ? runtimeDirectory : L"";
    g_logDirectory = logDirectory != nullptr ? logDirectory : L"";
    g_projectIdUtf8 = WideToUtf8(projectId);

    const sl::Feature features[] = { sl::kFeatureDLSS_RR };
    const wchar_t* pluginPaths[] = { g_runtimeDirectory.c_str() };

    sl::Preferences preferences{};
    preferences.logLevel = sl::LogLevel::eVerbose;
    preferences.pathsToPlugins = g_runtimeDirectory.empty() ? nullptr : pluginPaths;
    preferences.numPathsToPlugins = g_runtimeDirectory.empty() ? 0u : 1u;
    preferences.pathToLogsAndData = g_logDirectory.empty() ? nullptr : g_logDirectory.c_str();
    preferences.flags = sl::PreferenceFlags::eDisableCLStateTracking |
        sl::PreferenceFlags::eUseManualHooking |
        sl::PreferenceFlags::eUseFrameBasedResourceTagging;

    if (allowOverTheAirUpdates != 0)
        preferences.flags |= sl::PreferenceFlags::eAllowOTA;

    if (loadDownloadedPlugins != 0)
        preferences.flags |= sl::PreferenceFlags::eLoadDownloadedPlugins;
    preferences.featuresToLoad = features;
    preferences.numFeaturesToLoad = 1;
    preferences.applicationId = applicationId;
    preferences.projectId = g_projectIdUtf8.empty() ? nullptr : g_projectIdUtf8.c_str();
    preferences.engine = sl::EngineType::eUnity;
    preferences.engineVersion = "Unity 6000";
    preferences.renderAPI = sl::RenderAPI::eD3D12;

    sl::Result result = slInit(preferences, sl::kSDKVersion);
    if (result != sl::Result::eOk)
    {
        SetStreamlineError("slInit", result);
        return LPStreamlineStatusRuntimeLoadFailed;
    }

    g_slInitialized = true;
    return RefreshDlssRrSupport() ? LPStreamlineStatusOk : LPStreamlineStatusFeatureUnsupported;
}

LP_STREAMLINE_API int LPStreamline_IsDlssRayReconstructionSupported()
{
    std::lock_guard<std::mutex> lock(g_mutex);
    g_lastError.clear();
    return RefreshDlssRrSupport() ? 1 : 0;
}

LP_STREAMLINE_API void* LPStreamline_CreateEvaluateRequest(const LucentPathStreamlineEvaluateDesc* desc)
{
    if (desc == nullptr)
    {
        SetLastError("LPStreamline_CreateEvaluateRequest received null desc.");
        return nullptr;
    }

    return new LucentPathStreamlineEvaluateDesc(*desc);
}

LP_STREAMLINE_API void LPStreamline_DestroyEvaluateRequest(void* request)
{
    delete static_cast<LucentPathStreamlineEvaluateDesc*>(request);
}

LP_STREAMLINE_API UnityRenderingEventAndData LPStreamline_GetRenderEventFunc()
{
    return OnRenderEvent;
}

LP_STREAMLINE_API int LPStreamline_GetLastError(char* buffer, int bufferLength)
{
    if (buffer == nullptr || bufferLength <= 0)
        return static_cast<int>(g_lastError.size());

    if (g_lastError.empty())
    {
        buffer[0] = '\0';
        return 0;
    }

    std::snprintf(buffer, static_cast<size_t>(bufferLength), "%s", g_lastError.c_str());
    return static_cast<int>(g_lastError.size());
}

LP_STREAMLINE_API int LPStreamline_GetLastEvaluateStatus(char* buffer, int bufferLength)
{
    std::lock_guard<std::mutex> lock(g_mutex);
    if (buffer != nullptr && bufferLength > 0)
    {
        if (g_lastEvaluateError.empty())
            buffer[0] = '\0';
        else
            std::snprintf(buffer, static_cast<size_t>(bufferLength), "%s", g_lastEvaluateError.c_str());
    }

    return g_lastEvaluateStatus;
}

LP_STREAMLINE_API void LPStreamline_Shutdown()
{
    std::lock_guard<std::mutex> lock(g_mutex);
    if (g_slInitialized)
        slShutdown();

    g_slInitialized = false;
    g_deviceSet = false;
    g_dlssRrSupported = false;
    g_lastEvaluateStatus = 0;
    g_lastEvaluateError.clear();
}
