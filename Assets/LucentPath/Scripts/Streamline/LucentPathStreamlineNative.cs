using LucentPath.Rendering.Settings;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace LucentPath.Streamline
{
    internal static class LucentPathStreamlineNative
    {
        private const string NativePluginName = "LucentPathStreamline";
        private const int EvaluateDlssRrEventId = 1;

        [StructLayout(LayoutKind.Sequential)]
        public struct Matrix4x4Native
        {
            public float M00, M01, M02, M03;
            public float M10, M11, M12, M13;
            public float M20, M21, M22, M23;
            public float M30, M31, M32, M33;

            public static Matrix4x4Native From(Matrix4x4 m)
            {
                return new Matrix4x4Native
                {
                    M00 = m.m00,
                    M01 = m.m01,
                    M02 = m.m02,
                    M03 = m.m03,
                    M10 = m.m10,
                    M11 = m.m11,
                    M12 = m.m12,
                    M13 = m.m13,
                    M20 = m.m20,
                    M21 = m.m21,
                    M22 = m.m22,
                    M23 = m.m23,
                    M30 = m.m30,
                    M31 = m.m31,
                    M32 = m.m32,
                    M33 = m.m33
                };
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct EvaluateDesc
        {
            public int FrameIndex;
            public int ViewportId;
            public int InputWidth;
            public int InputHeight;
            public int OutputWidth;
            public int OutputHeight;
            public int DlssMode;
            public int AlphaUpscalingEnabled;
            public int DlaaPreset;
            public int QualityPreset;
            public int BalancedPreset;
            public int PerformancePreset;
            public int UltraPerformancePreset;
            public int ResetHistory;
            public int DepthInverted;
            public int Orthographic;
            public float JitterX;
            public float JitterY;
            public float MotionVectorScaleX;
            public float MotionVectorScaleY;
            public float PreExposure;
            public float ExposureScale;
            public float CameraNear;
            public float CameraFar;
            public float CameraFovRadians;
            public float CameraAspectRatio;
            public float CameraPositionX;
            public float CameraPositionY;
            public float CameraPositionZ;
            public float CameraUpX;
            public float CameraUpY;
            public float CameraUpZ;
            public float CameraRightX;
            public float CameraRightY;
            public float CameraRightZ;
            public float CameraForwardX;
            public float CameraForwardY;
            public float CameraForwardZ;
            public IntPtr ColorInput;
            public IntPtr ColorOutput;
            public IntPtr Depth;
            public IntPtr MotionVectors;
            public IntPtr DiffuseAlbedo;
            public IntPtr SpecularAlbedo;
            public IntPtr Normals;
            public IntPtr Roughness;
            public IntPtr NormalRoughness;
            public IntPtr SpecularMotionVectors;
            public IntPtr SpecularHitDistance;
            public Matrix4x4Native CameraViewToClip;
            public Matrix4x4Native ClipToCameraView;
            public Matrix4x4Native ClipToPrevClip;
            public Matrix4x4Native PrevClipToClip;
            public Matrix4x4Native WorldToCameraView;
            public Matrix4x4Native CameraViewToWorld;

            public void ApplySettings(StreamlineSettings settings)
            {
                if (settings == null)
                {
                    return;
                }

                AlphaUpscalingEnabled = settings.IsAlphaUpscalingEnabled ? 1 : 0;
                DlaaPreset = (int)settings.DlaaPreset;
                QualityPreset = (int)settings.QualityPreset;
                BalancedPreset = (int)settings.BalancedPreset;
                PerformancePreset = (int)settings.PerformancePreset;
                UltraPerformancePreset = (int)settings.UltraPerformancePreset;
                PreExposure = settings.PreExposure;
                ExposureScale = settings.ExposureScale;
            }
        }

        public static bool TryInitialize(string runtimeDirectory, string logDirectory, string applicationId, string projectId, bool isLoadDownloadedPlugins, bool isOverTheAirUpdatesAllowed, out string error)
        {
            try
            {
                if (TryParseApplicationId(applicationId, out uint parsedApplicationId, out error) == false)
                {
                    return false;
                }

                int result = LPStreamline_Initialize(
                    runtimeDirectory ?? string.Empty,
                    logDirectory ?? string.Empty,
                    parsedApplicationId,
                    projectId ?? string.Empty,
                    isLoadDownloadedPlugins ? 1 : 0,
                    isOverTheAirUpdatesAllowed ? 1 : 0);

                error = result == 0 ? null : GetLastError();

                return result == 0;
            }
            catch (DllNotFoundException)
            {
                error = $"{NativePluginName}.dll is missing. Build Native/LucentPathStreamline/CMakeLists.txt.";

                return false;
            }
            catch (EntryPointNotFoundException ex)
            {
                error = $"{NativePluginName}.dll does not expose the expected Streamline entry point: {ex.Message}";

                return false;
            }
        }

        public static bool TryInitialize(StreamlineSettings settings, string fallbackProjectId, out string error)
        {
            if (settings == null)
            {
                error = "Streamline settings are null.";

                return false;
            }

            settings.Validate();

            string applicationId = NormalizeNgxIdentifier(settings.ApplicationId);
            string projectId = string.IsNullOrWhiteSpace(settings.ProjectId) == false ? settings.ProjectId : fallbackProjectId;

            projectId = NormalizeNgxIdentifier(projectId);

            if (string.IsNullOrWhiteSpace(applicationId) && string.IsNullOrWhiteSpace(projectId))
            {
                error = "DLSS-RR uses Streamline NGX components, which need either a NVIDIA-provided Application ID or a project ID GUID together with the engine type (sl::Preferences). Placeholder values like 0, 123, test or none are not valid.";

                return false;
            }

            string logDirectory = ResolveProjectPath(settings.LogDirectory);

            if (string.IsNullOrEmpty(logDirectory) == false)
            {
                Directory.CreateDirectory(logDirectory);
            }

            return TryInitialize(
                ResolveProjectPath(settings.RuntimeDirectory),
                logDirectory,
                applicationId,
                projectId,
                settings.IsLoadDownloadedPlugins,
                settings.IsOverTheAirUpdatesAllowed,
                out error);
        }

        public static bool IsDlssRayReconstructionSupported(out string error)
        {
            try
            {
                int result = LPStreamline_IsDlssRayReconstructionSupported();

                error = result != 0 ? null : GetLastError();

                return result != 0;
            }
            catch (DllNotFoundException)
            {
                error = $"{NativePluginName}.dll is missing.";

                return false;
            }
            catch (EntryPointNotFoundException ex)
            {
                error = $"{NativePluginName}.dll does not expose the expected support-check entry point: {ex.Message}";

                return false;
            }
        }

        public static bool IssueEvaluateDlssRayReconstructionEvent(CommandBuffer cmd, ref EvaluateDesc desc, out string error)
        {
            if (cmd == null)
            {
                error = "CommandBuffer is null.";

                return false;
            }

            IntPtr request = IntPtr.Zero;

            try
            {
                request = LPStreamline_CreateEvaluateRequest(ref desc);

                if (request == IntPtr.Zero)
                {
                    error = GetLastError();

                    return false;
                }

                IntPtr renderEvent = LPStreamline_GetRenderEventFunc();

                if (renderEvent == IntPtr.Zero)
                {
                    LPStreamline_DestroyEvaluateRequest(request);

                    error = "Streamline render event function is unavailable.";

                    return false;
                }

                cmd.IssuePluginEventAndData(renderEvent, EvaluateDlssRrEventId, request);

                error = null;

                return true;
            }
            catch (DllNotFoundException)
            {
                if (request != IntPtr.Zero)
                {
                    LPStreamline_DestroyEvaluateRequest(request);
                }

                error = $"{NativePluginName}.dll is missing.";

                return false;
            }
            catch (EntryPointNotFoundException ex)
            {
                if (request != IntPtr.Zero)
                {
                    LPStreamline_DestroyEvaluateRequest(request);
                }

                error = $"{NativePluginName}.dll does not expose the expected render-event entry point: {ex.Message}";

                return false;
            }
        }

        public static int GetLastEvaluateStatus(out string error)
        {
            error = null;

            try
            {
                var buffer = new StringBuilder(2048);
                int status = LPStreamline_GetLastEvaluateStatus(buffer, buffer.Capacity);

                if (status < 0)
                {
                    error = buffer.Length > 0 ? buffer.ToString() : GetLastError();
                }

                return status;
            }
            catch (DllNotFoundException)
            {
                error = $"{NativePluginName}.dll is missing.";

                return -1;
            }
            catch (EntryPointNotFoundException ex)
            {
                error = $"{NativePluginName}.dll does not expose the expected evaluate-status entry point: {ex.Message}";

                return -1;
            }
        }

        public static void Shutdown()
        {
            try
            {
                LPStreamline_Shutdown();
            }
            catch (DllNotFoundException)
            { }
            catch (EntryPointNotFoundException)
            { }
        }

        private static string GetLastError()
        {
            var buffer = new StringBuilder(2048);
            int length = LPStreamline_GetLastError(buffer, buffer.Capacity);

            if (length <= 0)
            {
                return "Unknown LucentPath Streamline native error.";
            }

            return buffer.ToString();
        }

        private static string ResolveProjectPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            if (Path.IsPathRooted(path))
            {
                return path;
            }

            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
        }

        private static bool TryParseApplicationId(string value, out uint applicationId, out string error)
        {
            applicationId = 0;
            error = null;

            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            string trimmed = value.Trim();

            if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (uint.TryParse(
                    trimmed.Substring(2),
                    System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out applicationId))
                {
                    return true;
                }
            }
            else if (uint.TryParse(
                trimmed,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out applicationId))
            {
                return true;
            }

            error = $"Invalid NGX Application ID '{value}'. Enter the unsigned decimal id provided by NVIDIA, or a 0x-prefixed hexadecimal id.";

            return false;
        }

        private static string NormalizeNgxIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string trimmed = value.Trim();

            if (string.Equals(trimmed, "0", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "123", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "test", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "none", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "null", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return trimmed;
        }

        [DllImport(NativePluginName, CharSet = CharSet.Unicode)]
        private static extern int LPStreamline_Initialize(string runtimeDirectory, string logDirectory, uint applicationId, string projectId, int loadDownloadedPlugins, int allowOverTheAirUpdates);

        [DllImport(NativePluginName)]
        private static extern int LPStreamline_IsDlssRayReconstructionSupported();

        [DllImport(NativePluginName)]
        private static extern IntPtr LPStreamline_CreateEvaluateRequest(ref EvaluateDesc desc);

        [DllImport(NativePluginName)]
        private static extern void LPStreamline_DestroyEvaluateRequest(IntPtr request);

        [DllImport(NativePluginName)]
        private static extern IntPtr LPStreamline_GetRenderEventFunc();

        [DllImport(NativePluginName)]
        private static extern int LPStreamline_GetLastError(StringBuilder buffer, int bufferLength);

        [DllImport(NativePluginName)]
        private static extern int LPStreamline_GetLastEvaluateStatus(StringBuilder buffer, int bufferLength);

        [DllImport(NativePluginName)]
        private static extern void LPStreamline_Shutdown();
    }
}
