using LucentPath.Upscale.DLSS;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace LucentPath.Rendering
{
    internal class CameraState
    {
        public CameraSignal SurfaceSignal = new();
        public CameraSignal ReflectionSignal = new();
        public CameraSignal TransmissionSignal = new();
        public CameraSignal ShadowSignal = new();
        public CameraSecondaryGuideEvent ReflectionGuideEvent = new();
        public CameraSecondaryGuideEvent TransmissionGuideEvent = new();

        public int ViewportId;

        public Vector2 LastCameraJitterUv;

        public RTHandle Result;
        public RTHandle PrevResult;
        public RTHandle Moments;
        public RTHandle PrevMoments;
        public RTHandle FilterPing;
        public RTHandle FilterPong;
        public RTHandle DirectLighting;
        public RTHandle IndirectLighting;
        public RTHandle HitMask;
        public RTHandle PrevHitMask;
        public RTHandle ShadowHitMask;
        public RTHandle PrevShadowHitMask;
        public RTHandle FrameShadowSum;
        public RTHandle ReflectionShadowHitMask;
        public RTHandle PrevReflectionShadowHitMask;
        public RTHandle TransmissionShadowHitMask;
        public RTHandle PrevTransmissionShadowHitMask;
        public RTHandle FrameSurfaceSignalSum;
        public RTHandle SurfaceFastHistory;
        public RTHandle PrevSurfaceFastHistory;
        public RTHandle FrameReflectionSignalSum;
        public RTHandle ReflectionPrePassSignal;
        public RTHandle FrameTransmissionSignalSum;
        public RTHandle FrameEmissionSum;
        public RTHandle PrimaryDiffuseAlbedo;
        public RTHandle PrimarySpecularF0;
        public RTHandle PrimaryTransmissionTint;
        public RTHandle PrimaryHitDistance;
        public RTHandle PrevPrimaryHitDistance;
        public RTHandle ReflectionHitDistance;
        public RTHandle PrevReflectionHitDistance;
        public RTHandle TransmissionHitDistance;
        public RTHandle PrevTransmissionHitDistance;
        public RTHandle PrimaryRoughness;
        public RTHandle PrevPrimaryRoughness;

        public RTHandle PreparedDepth;
        public RTHandle PreparedMotionVectors;
        public RTHandle DlssOutput;
        public RTHandle DlssTransparencyMask;
        public RTHandle DlssBiasColorMask;

        public ComputeBuffer PathStateBuffer;
        public ComputeBuffer PathHitBuffer;
        public ComputeBuffer PathDistanceBuffer;
        public ComputeBuffer HitGBuffer;
        public ComputeBuffer PathFlagsBuffer;
        public ComputeBuffer ActivePathIndicesBuffer;
        public ComputeBuffer SimplePathIndicesBuffer;
        public ComputeBuffer ComplexPathIndicesBuffer;
        public ComputeBuffer BootstrapActiveIndicesBuffer;
        public ComputeBuffer RadianceCacheBuffer;
        public ComputeBuffer DeltaReflectionPathIndicesBuffer;
        public GraphicsBuffer DispatchArgsBuffer;
        public GraphicsBuffer DeltaReflectionArgsBuffer;
        public readonly DlssUpscaler DlssUpscaler = new();
        public int SampleCount;
        public int Width;
        public int Height;
        public int OutputWidth;
        public int OutputHeight;
        public bool IsLastDlssActive;
        public int LastDlssQuality;
        public int LastDlssSettingsHash;
        public bool IsLastPresentationUsedDlss;
        public Matrix4x4 LastViewProj;
        public Matrix4x4 LastViewMatrix;
        public Vector3 LastCameraWorldPos;
        public int LastOrthographic;
        public int LastRenderSettingsHash;
#if UNITY_EDITOR
        public int LastEditorCameraHash;
        public int LastEditorInvalidationVersion;
#endif
        public bool IsValid;

        internal class CameraSignal
        {
            public RTHandle Signal;
            public RTHandle PrevSignal;
            public RTHandle Moments;
            public RTHandle PrevMoments;
            public RTHandle Guide0;
            public RTHandle PrevGuide0;
            public RTHandle Guide1;
            public RTHandle PrevGuide1;
            public RTHandle Guide2;
            public RTHandle PrevGuide2;
            public RTHandle DenoisedSignal;
            public RTHandle HistoryFixedSignal;
            public RTHandle YoungHistorySignal;
        }

        internal class CameraSecondaryGuideEvent
        {
            public RTHandle Event0;
            public RTHandle PrevEvent0;
            public RTHandle Event1;
            public RTHandle PrevEvent1;
            public RTHandle Event2;
            public RTHandle PrevEvent2;
            public RTHandle Event3;
            public RTHandle PrevEvent3;

            private bool _hasHistory;

            public void Allocate(RenderTextureDescriptor desc, string namePrefix, int width, int height, bool hasHistory)
            {
                Event0 = Create(desc, $"{namePrefix}Event0_{width}x{height}");
                Event1 = Create(desc, $"{namePrefix}Event1_{width}x{height}");
                Event2 = Create(desc, $"{namePrefix}Event2_{width}x{height}");
                Event3 = Create(desc, $"{namePrefix}Event3_{width}x{height}");

                _hasHistory = hasHistory;
                if (_hasHistory)
                {
                    PrevEvent0 = Create(desc, $"{namePrefix}Event0Prev_{width}x{height}");
                    PrevEvent1 = Create(desc, $"{namePrefix}Event1Prev_{width}x{height}");
                    PrevEvent2 = Create(desc, $"{namePrefix}Event2Prev_{width}x{height}");
                    PrevEvent3 = Create(desc, $"{namePrefix}Event3Prev_{width}x{height}");
                }
            }

            public bool IsValid(int width, int height, GraphicsFormat format)
            {
                return IsValid(Event0, width, height, format) &&
                       IsValid(Event1, width, height, format) &&
                       IsValid(Event2, width, height, format) &&
                       IsValid(Event3, width, height, format) &&
                       (!_hasHistory ||
                        IsValid(PrevEvent0, width, height, format) &&
                        IsValid(PrevEvent1, width, height, format) &&
                        IsValid(PrevEvent2, width, height, format) &&
                        IsValid(PrevEvent3, width, height, format));
            }

            public void SwapHistory()
            {
                Swap(ref Event0, ref PrevEvent0);
                Swap(ref Event1, ref PrevEvent1);
                Swap(ref Event2, ref PrevEvent2);
                Swap(ref Event3, ref PrevEvent3);
            }

            public void Release()
            {
                Release(ref Event0);
                Release(ref PrevEvent0);
                Release(ref Event1);
                Release(ref PrevEvent1);
                Release(ref Event2);
                Release(ref PrevEvent2);
                Release(ref Event3);
                Release(ref PrevEvent3);
            }

            private static RTHandle Create(RenderTextureDescriptor desc, string name)
            {
                return RTHandles.Alloc(desc, FilterMode.Point, TextureWrapMode.Clamp, name: name);
            }

            private static bool IsValid(RTHandle handle, int width, int height, GraphicsFormat format)
            {
                return handle != null &&
                       handle.rt != null &&
                       handle.rt.width == width &&
                       handle.rt.height == height &&
                       handle.rt.graphicsFormat == format;
            }

            private static void Swap(ref RTHandle a, ref RTHandle b)
            {
                (a, b) = (b, a);
            }

            private static void Release(ref RTHandle handle)
            {
                if (handle == null)
                {
                    return;
                }

                handle.Release();

                handle = null;
            }
        }
    }
}
