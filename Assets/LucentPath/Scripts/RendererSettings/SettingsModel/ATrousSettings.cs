using System;

namespace LucentPath.Rendering.Settings
{
    [Serializable]
    public class ATrousSettings
    {
        public int AtrousIterations = 3;
        public int SurfaceAtrousIterations = 5;
        public int ReflectionAtrousIterations = 5;
        public int TransmissionAtrousIterations = 5;
        public float AtrousDepthPhi = 10f;
        public float AtrousNormalPhi = 32f;
        public float ReflectionPrePassRadius = 50f;
    }
}