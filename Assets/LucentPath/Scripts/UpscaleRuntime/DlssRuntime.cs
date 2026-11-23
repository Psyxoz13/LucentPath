using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

#if ENABLE_NVIDIA_MODULE && (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN)
using UnityEngine.NVIDIA;
using NvidiaGraphicsDevice = UnityEngine.NVIDIA.GraphicsDevice;
#endif

namespace LucentPath.Upscale.DLSS
{
    public enum DlssUpscaleMode
    {
        Off = 0,
        Quality = 1,
        Balanced = 2,
        Performance = 3,
        UltraPerformance = 4,
        DLAA = 5
    }

    public enum DlssMotionVectorResolution
    {
        SameAsOutput = 0,
        SameAsInput = 1
    }

    public enum LucentPathDlssMotionVectorSpace
    {
        NormalizedDeviceCoordinates = 0,
        PixelSpace = 1,
        UvSpace = 2
    }

    public enum LucentPathDlssPresetMode
    {
        NvidiaDefault = 0,
        SinglePreset = 1,
        PerQualityPreset = 2
    }

    public enum LucentPathDlssModelPreset
    {
        Default = 0,
        [Obsolete("DLSS preset F is a deprecated legacy preset in the current NVIDIA DLSS guidance. Use Default, J, K, L or M.")]
        PresetF = 1,
        PresetJ = 2,
        PresetK = 3,
        PresetL = 4,
        PresetM = 5
    }

    [Serializable]
    public class DlssSettings
    {
        public bool IsRuntimeActive => Quality != DlssUpscaleMode.Off;
        public bool IsDlaaMode => Quality == DlssUpscaleMode.DLAA;
        public bool IsNvidiaOptimalSettingsUsed => IsNvidiaOptimalSettingsPreferred && IsDlaaMode == false;
        public bool IsFallbackRenderScaleUsed => IsNvidiaOptimalSettingsPreferred == false && IsDlaaMode == false;
        public bool IsNvidiaOptimalSharpnessAvailable => IsDlaaMode == false;
        public bool IsNvidiaOptimalSharpnessUsed => IsSharpeningEnabled && IsNvidiaOptimalSharpnessPreferred && IsNvidiaOptimalSharpnessAvailable;
        public bool IsManualSharpnessUsed => IsSharpeningEnabled && IsNvidiaOptimalSharpnessUsed == false;

        [Header("Runtime")]

        public DlssUpscaleMode Quality = DlssUpscaleMode.Off;

        public bool IsSceneViewAllowed = true;
        public bool IsDisableWhenDebugViewIsEnabled = true;
        public bool IsFallbackToNativeWhenUnavailable = true;

        [Header("Resolution")]
        public bool IsNvidiaOptimalSettingsPreferred = true;

        [Range(.3333333f, 1.0f)] public float FallbackRenderScale = .6666667f;
        [Min(1)] public int MinimumOutputWidth = 640;
        [Min(1)] public int MinimumOutputHeight = 360;

        [Header("Temporal jitter")]
        public bool IsDlssJitterEnabled = true;

        [HideInInspector] public float JitterScaleX = 1.0f;
        [HideInInspector] public float JitterScaleY = 1.0f;

        [Header("DLSS model presets")]
        public LucentPathDlssPresetMode PresetMode = LucentPathDlssPresetMode.NvidiaDefault;

        public LucentPathDlssModelPreset ModelPreset = LucentPathDlssModelPreset.PresetK;
        public LucentPathDlssModelPreset QualityPreset = LucentPathDlssModelPreset.PresetK;
        public LucentPathDlssModelPreset BalancedPreset = LucentPathDlssModelPreset.PresetK;
        public LucentPathDlssModelPreset PerformancePreset = LucentPathDlssModelPreset.PresetM;
        public LucentPathDlssModelPreset UltraPerformancePreset = LucentPathDlssModelPreset.PresetL;
        public LucentPathDlssModelPreset DlaaPreset = LucentPathDlssModelPreset.PresetK;
        public bool IsWarnWhenPresetIsUnsupported = true;

        [Header("Input interpretation")]
        public bool IsHdrInput = true;

        public bool IsSystemReversedZUsed = true;
        public bool IsDepthInverted;

        [Tooltip("Resolved automatically from the actual motion-vector texture and DLSS output resolution; kept serialized for legacy assets only.")]
        [HideInInspector] public DlssMotionVectorResolution MotionVectorResolution = DlssMotionVectorResolution.SameAsInput;

        public LucentPathDlssMotionVectorSpace MotionVectorSpace = LucentPathDlssMotionVectorSpace.NormalizedDeviceCoordinates;

        [Tooltip("The pipeline now emits non-jittered motion vectors and passes jitter to DLSS separately, as required by NVIDIA guidance.")]
        [HideInInspector] public bool IsMotionVectorsJittered;

        public bool IsAutoMotionVectorApiScale = true;
        public uint InvertMotionVectorX;
        public uint InvertMotionVectorY;
        public float MotionVectorScaleX = 1.0f;
        public float MotionVectorScaleY = 1.0f;

        [Header("Sharpening")]
        public bool IsSharpeningEnabled = true;

        public bool IsNvidiaOptimalSharpnessPreferred;
        [Range(.01f, 1.0f)] public float SharpnessOverride = .35f;

        [Header("DLSS runtime library")]
        [Tooltip("Optional NVIDIA NGX project id. Leave empty for local/dev integration.")]
        public string ProjectId = string.Empty;

        public void Validate()
        {
            if (IsSystemReversedZUsed)
            {
                IsDepthInverted = SystemInfo.usesReversedZBuffer;
            }

            MinimumOutputWidth = Mathf.Max(1, MinimumOutputWidth);
            MinimumOutputHeight = Mathf.Max(1, MinimumOutputHeight);
            FallbackRenderScale = Mathf.Clamp(FallbackRenderScale, .3333333f, 1.0f);
            JitterScaleX = 1.0f;
            JitterScaleY = 1.0f;
            IsMotionVectorsJittered = false;

            SanitizeDeprecatedPresets();

            InvertMotionVectorX = (uint)(InvertMotionVectorX != 0 ? 1u : 0);
            InvertMotionVectorY = (uint)(InvertMotionVectorY != 0 ? 1u : 0);
            MotionVectorScaleX = Mathf.Abs(MotionVectorScaleX) < 1e-6f ? 1.0f : MotionVectorScaleX;
            MotionVectorScaleY = Mathf.Abs(MotionVectorScaleY) < 1e-6f ? 1.0f : MotionVectorScaleY;

            if (SharpnessOverride < .01f)
            {
                SharpnessOverride = .35f;
            }

            SharpnessOverride = Mathf.Clamp(SharpnessOverride, .01f, 1.0f);
        }

        public float ResolveEffectiveSharpness(int outputWidth, int outputHeight)
        {
            if (IsSharpeningEnabled == false)
            {
                return 0f;
            }

            if (IsNvidiaOptimalSharpnessUsed &&
                DlssBackend.TryGetOptimalSettings(outputWidth, outputHeight, Quality, out DlssOptimalSettings optimal) &&
                optimal.IsValid)
            {
                return Mathf.Clamp01(optimal.Sharpness);
            }

            return Mathf.Clamp(SharpnessOverride, .01f, 1.0f);
        }

        public int GetRuntimeConfigurationHash()
        {
            unchecked
            {
                int hash = 17;

                hash = hash * 31 + (int)Quality;
                hash = hash * 31 + IsNvidiaOptimalSettingsUsed.GetHashCode();

                if (IsFallbackRenderScaleUsed)
                {
                    hash = hash * 31 + FallbackRenderScale.GetHashCode();
                }

                hash = hash * 31 + IsDlssJitterEnabled.GetHashCode();
                hash = hash * 31 + IsHdrInput.GetHashCode();
                hash = hash * 31 + IsDepthInverted.GetHashCode();
                hash = hash * 31 + (int)MotionVectorSpace;
                hash = hash * 31 + IsAutoMotionVectorApiScale.GetHashCode();
                hash = hash * 31 + (int)InvertMotionVectorX;
                hash = hash * 31 + (int)InvertMotionVectorY;

                if (IsAutoMotionVectorApiScale == false)
                {
                    hash = hash * 31 + MotionVectorScaleX.GetHashCode();
                    hash = hash * 31 + MotionVectorScaleY.GetHashCode();
                }

                hash = hash * 31 + DlssBackend.GetPresetConfigurationHash(this);
                hash = hash * 31 + (ProjectId != null ? ProjectId.GetHashCode() : 0);

                return hash;
            }
        }

        private static LucentPathDlssModelPreset SanitizePreset(LucentPathDlssModelPreset preset, LucentPathDlssModelPreset replacement)
        {
#pragma warning disable CS0618
            return preset == LucentPathDlssModelPreset.PresetF ? replacement : preset;
#pragma warning restore CS0618
        }

        private void SanitizeDeprecatedPresets()
        {
            ModelPreset = SanitizePreset(ModelPreset, LucentPathDlssModelPreset.PresetK);
            QualityPreset = SanitizePreset(QualityPreset, LucentPathDlssModelPreset.PresetK);
            BalancedPreset = SanitizePreset(BalancedPreset, LucentPathDlssModelPreset.PresetK);
            PerformancePreset = SanitizePreset(PerformancePreset, LucentPathDlssModelPreset.PresetM);
            UltraPerformancePreset = SanitizePreset(UltraPerformancePreset, LucentPathDlssModelPreset.PresetL);
            DlaaPreset = SanitizePreset(DlaaPreset, LucentPathDlssModelPreset.PresetK);
        }
    }

    public readonly struct DlssOptimalSettings
    {
        public readonly bool IsValid;
        public readonly int RenderWidth;
        public readonly int RenderHeight;
        public readonly int MinWidth;
        public readonly int MinHeight;
        public readonly int MaxWidth;
        public readonly int MaxHeight;
        public readonly float Sharpness;

        public static DlssOptimalSettings Invalid => new DlssOptimalSettings(false, 0, 0, 0, 0, 0, 0, 0f);

        public DlssOptimalSettings(bool valid, int renderWidth, int renderHeight, int minWidth, int minHeight, int maxWidth, int maxHeight, float sharpness)
        {
            IsValid = valid;
            RenderWidth = renderWidth;
            RenderHeight = renderHeight;
            MinWidth = minWidth;
            MinHeight = minHeight;
            MaxWidth = maxWidth;
            MaxHeight = maxHeight;
            Sharpness = sharpness;
        }
    }

    public readonly struct DlssFrameInputs
    {
        public readonly RenderTexture ColorInput;
        public readonly RenderTexture ColorOutput;
        public readonly RenderTexture Depth;
        public readonly RenderTexture MotionVectors;
        public readonly RenderTexture Exposure1x1;
        public readonly RenderTexture TransparencyMask;
        public readonly RenderTexture BiasColorMask;
        public readonly int InputWidth;
        public readonly int InputHeight;
        public readonly int OutputWidth;
        public readonly int OutputHeight;
        public readonly Vector2 JitterDeviceOffset;

        public bool IsMotionVectorsLowResolution =>
            InputWidth != OutputWidth ||
            InputHeight != OutputHeight ||
            (MotionVectors != null && (MotionVectors.width < OutputWidth || MotionVectors.height < OutputHeight));

        public DlssFrameInputs(
            RenderTexture colorInput,
            RenderTexture colorOutput,
            RenderTexture depth,
            RenderTexture motionVectors,
            int inputWidth,
            int inputHeight,
            int outputWidth,
            int outputHeight,
            Vector2 jitterDeviceOffset,
            RenderTexture exposure1x1 = null,
            RenderTexture transparencyMask = null,
            RenderTexture biasColorMask = null)
        {
            ColorInput = colorInput;
            ColorOutput = colorOutput;
            Depth = depth;
            MotionVectors = motionVectors;
            InputWidth = inputWidth;
            InputHeight = inputHeight;
            OutputWidth = outputWidth;
            OutputHeight = outputHeight;
            JitterDeviceOffset = jitterDeviceOffset;
            Exposure1x1 = exposure1x1;
            TransparencyMask = transparencyMask;
            BiasColorMask = biasColorMask;
        }

        public bool IsValid(out string error)
        {
            if (ColorInput == null)
            {
                error = "DLSS colorInput is null.";

                return false;
            }

            if (ColorOutput == null)
            {
                error = "DLSS colorOutput is null.";

                return false;
            }

            if (Depth == null)
            {
                error = "DLSS depth is null.";

                return false;
            }

            if (MotionVectors == null)
            {
                error = "DLSS motionVectors is null.";

                return false;
            }

            if (InputWidth <= 0 || InputHeight <= 0 || OutputWidth <= 0 || OutputHeight <= 0)
            {
                error = $"Invalid DLSS dimensions: input={InputWidth}x{InputHeight}, output={OutputWidth}x{OutputHeight}.";

                return false;
            }

            if (ColorInput.width < InputWidth || ColorInput.height < InputHeight)
            {
                error = $"colorInput is smaller than input rect: texture={ColorInput.width}x{ColorInput.height}, rect={InputWidth}x{InputHeight}.";

                return false;
            }

            if (ColorOutput.width < OutputWidth || ColorOutput.height < OutputHeight)
            {
                error = $"colorOutput is smaller than output rect: texture={ColorOutput.width}x{ColorOutput.height}, rect={OutputWidth}x{OutputHeight}.";

                return false;
            }

            if (Depth.width < InputWidth || Depth.height < InputHeight)
            {
                error = $"depth must be at least input resolution: texture={Depth.width}x{Depth.height}, input={InputWidth}x{InputHeight}.";

                return false;
            }

            if (MotionVectors.width < InputWidth || MotionVectors.height < InputHeight)
            {
                error = $"motionVectors must be at least input resolution: texture={MotionVectors.width}x{MotionVectors.height}, input={InputWidth}x{InputHeight}.";

                return false;
            }

            if (TransparencyMask != null && (TransparencyMask.width < InputWidth || TransparencyMask.height < InputHeight))
            {
                error = $"transparencyMask must be at least input resolution: texture={TransparencyMask.width}x{TransparencyMask.height}, input={InputWidth}x{InputHeight}.";

                return false;
            }

            if (BiasColorMask != null && (BiasColorMask.width < InputWidth || BiasColorMask.height < InputHeight))
            {
                error = $"biasColorMask must be at least input resolution: texture={BiasColorMask.width}x{BiasColorMask.height}, input={InputWidth}x{InputHeight}.";

                return false;
            }

            error = null;

            return true;
        }
    }

    public static class DlssJitter
    {
        private const int SequenceLength = 1024;
        private const float UnitJitterScale = 1.0f;

        public static Vector2 GetHaltonJitterPixels(int frameIndex)
        {
            int index = PositiveModulo(frameIndex, SequenceLength) + 1;

            return new Vector2(Halton(index, 2) - .5f, Halton(index, 3) - .5f);
        }

        public static Matrix4x4 ApplyJitterToProjection(Matrix4x4 projection, Vector2 jitterPixels, int renderWidth, int renderHeight)
        {
            return ApplyJitterToProjection(projection, jitterPixels, renderWidth, renderHeight, UnitJitterScale, UnitJitterScale);
        }

        public static Matrix4x4 ApplyJitterToProjection(Matrix4x4 projection, Vector2 jitterPixels, int renderWidth, int renderHeight, float scaleX, float scaleY)
        {
            Vector2 device = ToDeviceCoordinates(jitterPixels, renderWidth, renderHeight, scaleX, scaleY);

            projection.m02 += device.x;
            projection.m12 += device.y;

            return projection;
        }

        public static Vector2 ToDeviceCoordinates(Vector2 jitterPixels, int renderWidth, int renderHeight)
        {
            return ToDeviceCoordinates(jitterPixels, renderWidth, renderHeight, UnitJitterScale, UnitJitterScale);
        }

        public static Vector2 ToDeviceCoordinates(Vector2 jitterPixels, int renderWidth, int renderHeight, float scaleX, float scaleY)
        {
            return new Vector2(
                (jitterPixels.x * scaleX * 2.0f) / Mathf.Max(1, renderWidth),
                (jitterPixels.y * scaleY * 2.0f) / Mathf.Max(1, renderHeight));
        }

        private static float Halton(int index, int radix)
        {
            float result = 0f;
            float fraction = 1.0f / radix;

            while (index > 0)
            {
                result += (index % radix) * fraction;
                index /= radix;
                fraction /= radix;
            }

            return result;
        }

        private static int PositiveModulo(int value, int divisor)
        {
            int result = value % divisor;

            return result < 0 ? result + divisor : result;
        }
    }

    public static class DlssBackend
    {
        private static bool _isInitialized;
        private static string _lastError;
        private static string _lastPresetWarning;

        public static bool IsCompiled
        {
            get
            {
#if ENABLE_NVIDIA_MODULE && (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN)
                return true;
#else
                return false;
#endif
            }
        }

        public static string LastError => _lastError;

        public static bool TryInitialize(DlssSettings settings, out string error)
        {
            string projectId = settings != null ? settings.ProjectId : string.Empty;

            return TryInitialize(projectId, out error);
        }

        public static bool TryInitialize(string projectId, out string error)
        {
#if ENABLE_NVIDIA_MODULE && (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN)
            if (_isInitialized)
            {
                error = null;

                return true;
            }

            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D11 &&
                SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D12 &&
                SystemInfo.graphicsDeviceType != GraphicsDeviceType.Vulkan)
            {
                error = $"DLSS requires Windows D3D11, D3D12, or Vulkan. Current API: {SystemInfo.graphicsDeviceType}.";
                _lastError = error;

                return false;
            }

            if (NVUnityPlugin.IsLoaded() == false && NVUnityPlugin.Load() == false)
            {
                error = "NVUnityPlugin.Load() failed. Enable Unity Built-in package 'NVIDIA' and include ENABLE_NVIDIA_MODULE.";
                _lastError = error;

                return false;
            }

            NvidiaGraphicsDevice device;

            if (string.IsNullOrEmpty(projectId) == false)
            {
                device = NvidiaGraphicsDevice.device ?? NvidiaGraphicsDevice.CreateGraphicsDevice(projectId);
            }
            else
            {
                device = NvidiaGraphicsDevice.device ?? NvidiaGraphicsDevice.CreateGraphicsDevice();
            }

            if (device == null)
            {
                error = "NVIDIA GraphicsDevice.CreateGraphicsDevice() returned null.";
                _lastError = error;

                return false;
            }

            if (device.IsFeatureAvailable(GraphicsDeviceFeature.DLSS) == false)
            {
                error = "DLSS is not available on this GPU/driver/runtime. Falling back to native rendering.";
                _lastError = error;

                return false;
            }

            _isInitialized = true;

            error = null;

            return true;
#else
            error = "DLSS backend is not compiled. Enable com.unity.modules.nvidia and add ENABLE_NVIDIA_MODULE to Standalone scripting defines.";
            s_LastError = error;
            return false;
#endif
        }

        public static bool TryGetOptimalSettings(int outputWidth, int outputHeight, DlssUpscaleMode mode, out DlssOptimalSettings settings)
        {
#if ENABLE_NVIDIA_MODULE && (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN)
            settings = DlssOptimalSettings.Invalid;

            NvidiaGraphicsDevice device = NvidiaGraphicsDevice.device;

            if (device == null || mode == DlssUpscaleMode.Off || mode == DlssUpscaleMode.DLAA)
            {
                return false;
            }

            if (device.GetOptimalSettings((uint)outputWidth, (uint)outputHeight, ToNvidiaQuality(mode), out OptimalDLSSSettingsData optimal) == false)
            {
                return false;
            }

            settings = new DlssOptimalSettings(
                true,
                Mathf.Max(1, (int)optimal.outRenderWidth),
                Mathf.Max(1, (int)optimal.outRenderHeight),
                Mathf.Max(1, (int)optimal.minWidth),
                Mathf.Max(1, (int)optimal.minHeight),
                Mathf.Max(1, (int)optimal.maxWidth),
                Mathf.Max(1, (int)optimal.maxHeight),
                optimal.sharpness);

            return true;
#else
            settings = DlssOptimalSettings.Invalid;
            return false;
#endif
        }

        public static int GetPresetConfigurationHash(DlssSettings settings)
        {
            if (settings == null || settings.PresetMode == LucentPathDlssPresetMode.NvidiaDefault)
            {
                return 0;
            }

            unchecked
            {
                int hash = 17;

                hash = hash * 31 + (int)settings.PresetMode;

                if (settings.PresetMode == LucentPathDlssPresetMode.SinglePreset)
                {
                    hash = hash * 31 + (int)settings.ModelPreset;

                    return hash;
                }

                hash = hash * 31 + (int)settings.QualityPreset;
                hash = hash * 31 + (int)settings.BalancedPreset;
                hash = hash * 31 + (int)settings.PerformancePreset;
                hash = hash * 31 + (int)settings.UltraPerformancePreset;
                hash = hash * 31 + (int)settings.DlaaPreset;

                return hash;
            }
        }

#if ENABLE_NVIDIA_MODULE && (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN)
        public static DLSSQuality ToNvidiaQuality(DlssUpscaleMode mode)
        {
            switch (mode)
            {
                case DlssUpscaleMode.Quality:
                case DlssUpscaleMode.DLAA:
                    return DLSSQuality.MaximumQuality;
                case DlssUpscaleMode.Balanced:
                    return DLSSQuality.Balanced;
                case DlssUpscaleMode.Performance:
                    return DLSSQuality.MaximumPerformance;
                case DlssUpscaleMode.UltraPerformance:
                    return DLSSQuality.UltraPerformance;
                default:
                    return DLSSQuality.Balanced;
            }
        }

        public static void ApplyPresetOverrides(ref DLSSCommandInitializationData init, DlssSettings settings)
        {
            if (settings == null || settings.PresetMode == LucentPathDlssPresetMode.NvidiaDefault)
            {
                return;
            }

            Type initType = typeof(DLSSCommandInitializationData);

            if (HasAnyPresetMember(initType) == false)
            {
                return;
            }

            if (settings.PresetMode == LucentPathDlssPresetMode.SinglePreset)
            {
                TrySetPresetMember(ref init, "presetQualityMode", settings.ModelPreset, settings.IsWarnWhenPresetIsUnsupported);
                TrySetPresetMember(ref init, "presetBalancedMode", settings.ModelPreset, settings.IsWarnWhenPresetIsUnsupported);
                TrySetPresetMember(ref init, "presetPerformanceMode", settings.ModelPreset, settings.IsWarnWhenPresetIsUnsupported);
                TrySetPresetMember(ref init, "presetUltraPerformanceMode", settings.ModelPreset, settings.IsWarnWhenPresetIsUnsupported);
                TrySetPresetMember(ref init, "presetDlaaMode", settings.ModelPreset, settings.IsWarnWhenPresetIsUnsupported);

                return;
            }

            var qualityPreset = settings.IsDlaaMode ? settings.DlaaPreset : settings.QualityPreset;

            TrySetPresetMember(ref init, "presetQualityMode", qualityPreset, settings.IsWarnWhenPresetIsUnsupported);
            TrySetPresetMember(ref init, "presetBalancedMode", settings.BalancedPreset, settings.IsWarnWhenPresetIsUnsupported);
            TrySetPresetMember(ref init, "presetPerformanceMode", settings.PerformancePreset, settings.IsWarnWhenPresetIsUnsupported);
            TrySetPresetMember(ref init, "presetUltraPerformanceMode", settings.UltraPerformancePreset, settings.IsWarnWhenPresetIsUnsupported);
            TrySetPresetMember(ref init, "presetDlaaMode", settings.DlaaPreset, settings.IsWarnWhenPresetIsUnsupported);
        }

        private static void TrySetPresetMember(ref DLSSCommandInitializationData init, string memberName, LucentPathDlssModelPreset preset, bool warnWhenUnsupported)
        {
            Type initType = typeof(DLSSCommandInitializationData);
            MemberInfo member = GetPresetMember(initType, memberName);

            if (member == null)
            {
                WarnPresetOnce(
                    "Unity NVIDIA module does not expose DLSS preset API. Update Unity to a version with DLSSPreset support if you need model selection.",
                    warnWhenUnsupported);

                return;
            }

            Type memberType = member is PropertyInfo propertyInfo ? propertyInfo.PropertyType : ((FieldInfo)member).FieldType;
            object presetValue = TryResolvePresetValue(memberType, preset);

            if (presetValue == null)
            {
                WarnPresetOnce(
                    $"DLSS preset {preset} is not available in this Unity/NVIDIA module. Keeping NVIDIA default for {memberName}.",
                    warnWhenUnsupported);

                return;
            }

            object boxed = init;

            if (member is PropertyInfo property)
            {
                property.SetValue(boxed, presetValue, null);
            }
            else
            {
                ((FieldInfo)member).SetValue(boxed, presetValue);
            }

            init = (DLSSCommandInitializationData)boxed;
        }

        private static bool HasPresetMember(Type initType, string memberName)
        {
            return GetPresetMember(initType, memberName) != null;
        }

        private static bool HasAnyPresetMember(Type initType)
        {
            return HasPresetMember(initType, "presetQualityMode") ||
                   HasPresetMember(initType, "presetBalancedMode") ||
                   HasPresetMember(initType, "presetPerformanceMode") ||
                   HasPresetMember(initType, "presetUltraPerformanceMode") ||
                   HasPresetMember(initType, "presetDlaaMode");
        }

        private static MemberInfo GetPresetMember(Type initType, string memberName)
        {
            if (initType == null)
            {
                return null;
            }

            MemberInfo member = initType.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public);

            return member ?? initType.GetField(memberName, BindingFlags.Instance | BindingFlags.Public);
        }

        private static object TryResolvePresetValue(Type enumType, LucentPathDlssModelPreset preset)
        {
            if (enumType == null || enumType.IsEnum == false)
            {
                return null;
            }

            string name;

            switch (preset)
            {
#pragma warning disable CS0618
                case LucentPathDlssModelPreset.PresetF: name = "Preset_F"; break;
#pragma warning restore CS0618
                case LucentPathDlssModelPreset.PresetJ: name = "Preset_J"; break;
                case LucentPathDlssModelPreset.PresetK: name = "Preset_K"; break;
                case LucentPathDlssModelPreset.PresetL: name = "Preset_L"; break;
                case LucentPathDlssModelPreset.PresetM: name = "Preset_M"; break;
                case LucentPathDlssModelPreset.Default:
                default:
                    name = "Preset_Default";
                    break;
            }

            try
            {
                return Enum.Parse(enumType, name);
            }
            catch
            {
                return null;
            }
        }

        private static void WarnPresetOnce(string message, bool enabled)
        {
            if (enabled == false || string.IsNullOrEmpty(message) || string.Equals(message, _lastPresetWarning, StringComparison.Ordinal))
            {
                return;
            }

            _lastPresetWarning = message;

            Debug.LogWarning($"LucentPath DLSS preset selection: {message}");
        }
#endif
    }

    public class DlssUpscaler : IDisposable
    {
#if ENABLE_NVIDIA_MODULE && (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN)
        private DLSSContext _context;

#endif
        private ContextKey _key;
        private bool _isContextAvailable;
        private bool _isDisposed;

        public bool IsContextAvailable => _isContextAvailable;

        public void ReleaseResources()
        {
            DestroyContextImmediate();
        }

        public bool Execute(CommandBuffer cmd, in DlssFrameInputs inputs, DlssSettings settings, bool resetHistory, out string error)
        {
            if (cmd == null)
            {
                error = "CommandBuffer is null.";

                return false;
            }

            if (settings == null || settings.IsRuntimeActive == false)
            {
                error = "DLSS is disabled.";

                return false;
            }

            if (inputs.IsValid(out error) == false)
            {
                return false;
            }

#if ENABLE_NVIDIA_MODULE && (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN)
            NvidiaGraphicsDevice device = NvidiaGraphicsDevice.device;

            if (device == null)
            {
                error = "DLSS GraphicsDevice is null. Call DlssBackend.TryInitialize() first.";

                return false;
            }

            ContextKey nextKey = ContextKey.From(inputs, settings);

            if (_isContextAvailable == false || _key.Equals(nextKey) == false)
            {
                DestroyContext(cmd, device);
                CreateContext(cmd, device, nextKey, settings);
            }

            if (_isContextAvailable == false || _context == null)
            {
                error = "DLSS context creation failed.";

                return false;
            }

            _context.executeData.invertXAxis = (uint)settings.InvertMotionVectorX;
            _context.executeData.invertYAxis = (uint)settings.InvertMotionVectorY;
            _context.executeData.jitterOffsetX = inputs.JitterDeviceOffset.x;
            _context.executeData.jitterOffsetY = inputs.JitterDeviceOffset.y;

            Vector2 mvScale = ResolveMotionVectorApiScale(settings, inputs.InputWidth, inputs.InputHeight);

            _context.executeData.mvScaleX = mvScale.x;
            _context.executeData.mvScaleY = mvScale.y;
            _context.executeData.preExposure = 1.0f;
            _context.executeData.reset = resetHistory ? 1 : 0;
            _context.executeData.sharpness = 0f;
            _context.executeData.subrectOffsetX = 0;
            _context.executeData.subrectOffsetY = 0;
            _context.executeData.subrectWidth = (uint)inputs.InputWidth;
            _context.executeData.subrectHeight = (uint)inputs.InputHeight;

            var textures = new DLSSTextureTable
            {
                colorInput = inputs.ColorInput,
                colorOutput = inputs.ColorOutput,
                depth = inputs.Depth,
                motionVectors = inputs.MotionVectors,
                exposureTexture = inputs.Exposure1x1,
                transparencyMask = inputs.TransparencyMask,
                biasColorMask = inputs.BiasColorMask
            };

            device.ExecuteDLSS(cmd, _context, in textures);

            error = null;

            return true;
#else
            error = "DLSS backend is not compiled. Define ENABLE_NVIDIA_MODULE and enable the NVIDIA built-in package.";
            return false;
#endif
        }

        private static Vector2 ResolveMotionVectorApiScale(DlssSettings settings, int inputWidth, int inputHeight)
        {
            if (settings.IsAutoMotionVectorApiScale == false)
            {
                return new Vector2(settings.MotionVectorScaleX, settings.MotionVectorScaleY);
            }

            switch (settings.MotionVectorSpace)
            {
                case LucentPathDlssMotionVectorSpace.PixelSpace:
                    return new Vector2(
                        1.0f / Mathf.Max(1, inputWidth),
                        1.0f / Mathf.Max(1, inputHeight));

                case LucentPathDlssMotionVectorSpace.UvSpace:
                    return new Vector2(2.0f, 2.0f);

                case LucentPathDlssMotionVectorSpace.NormalizedDeviceCoordinates:
                default:
                    return Vector2.one;
            }
        }

#if ENABLE_NVIDIA_MODULE && (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN)
        private void CreateContext(CommandBuffer cmd, NvidiaGraphicsDevice device, ContextKey key, DlssSettings settings)
        {
            var init = new DLSSCommandInitializationData
            {
                inputRTWidth = (uint)key.InputWidth,
                inputRTHeight = (uint)key.InputHeight,
                outputRTWidth = (uint)key.OutputWidth,
                outputRTHeight = (uint)key.OutputHeight,
                quality = DlssBackend.ToNvidiaQuality(settings.Quality)
            };

            init.SetFlag(DLSSFeatureFlags.IsHDR, settings.IsHdrInput);
            init.SetFlag(DLSSFeatureFlags.DepthInverted, settings.IsDepthInverted);
            init.SetFlag(DLSSFeatureFlags.MVLowRes, key.IsMotionVectorsLowResolution);
            init.SetFlag(DLSSFeatureFlags.MVJittered, false);

            DlssBackend.ApplyPresetOverrides(ref init, settings);

            _context = device.CreateFeature(cmd, in init);
            _key = key;
            _isContextAvailable = _context != null;
        }

        private void DestroyContext(CommandBuffer cmd, NvidiaGraphicsDevice device)
        {
            if (_isContextAvailable == false || _context == null || device == null)
            {
                return;
            }

            device.DestroyFeature(cmd, _context);

            _context = null;
            _isContextAvailable = false;
        }

#endif

        private void DestroyContextImmediate()
        {
#if ENABLE_NVIDIA_MODULE && (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN)
            if (_isContextAvailable == false || _context == null || NvidiaGraphicsDevice.device == null)
            {
                return;
            }

            CommandBuffer cmd = CommandBufferPool.Get("Destroy DLSS Context");

            NvidiaGraphicsDevice.device.DestroyFeature(cmd, _context);
            Graphics.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);

            _context = null;
            _isContextAvailable = false;
#endif
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            DestroyContextImmediate();
        }

        private readonly struct ContextKey : IEquatable<ContextKey>
        {
            public readonly int InputWidth;
            public readonly int InputHeight;
            public readonly int OutputWidth;
            public readonly int OutputHeight;
            public readonly DlssUpscaleMode Mode;
            public readonly bool IsHdr;
            public readonly bool IsDepthInverted;
            public readonly bool IsMotionVectorsLowResolution;
            public readonly int PresetHash;

            private ContextKey(
                int inputWidth,
                int inputHeight,
                int outputWidth,
                int outputHeight,
                DlssUpscaleMode mode,
                bool hdr,
                bool depthInverted,
                bool motionVectorsLowResolution,
                int presetHash)
            {
                InputWidth = inputWidth;
                InputHeight = inputHeight;
                OutputWidth = outputWidth;
                OutputHeight = outputHeight;
                Mode = mode;
                IsHdr = hdr;
                IsDepthInverted = depthInverted;
                IsMotionVectorsLowResolution = motionVectorsLowResolution;
                PresetHash = presetHash;
            }

            public static ContextKey From(in DlssFrameInputs inputs, DlssSettings settings)
            {
                return new ContextKey(
                    inputs.InputWidth,
                    inputs.InputHeight,
                    inputs.OutputWidth,
                    inputs.OutputHeight,
                    settings.Quality,
                    settings.IsHdrInput,
                    settings.IsDepthInverted,
                    inputs.IsMotionVectorsLowResolution,
                    settings.GetRuntimeConfigurationHash());
            }

            public bool Equals(ContextKey other)
            {
                return InputWidth == other.InputWidth &&
                       InputHeight == other.InputHeight &&
                       OutputWidth == other.OutputWidth &&
                       OutputHeight == other.OutputHeight &&
                       Mode == other.Mode &&
                       IsHdr == other.IsHdr &&
                       IsDepthInverted == other.IsDepthInverted &&
                       IsMotionVectorsLowResolution == other.IsMotionVectorsLowResolution &&
                       PresetHash == other.PresetHash;
            }
        }
    }
}
