# Lucent Path

A research renderer for real-time path tracing in Unity URP. The DXR backend uses wavefront ray and compute passes, compacts active paths, and supports OpenPBR materials, temporal accumulation, and à-trous filtering. The native C++ bridge integrates NVIDIA Streamline for DLSS Ray Reconstruction.

## Setup

1. Open the project with the Unity 6.6 version recorded in ProjectSettings/ProjectVersion.txt.
2. Run on Windows with a ray-tracing-capable GPU and Direct3D 12.
3. Assign Assets/Settings/PC_RPAsset.asset in Graphics settings and select the Lucent Path renderer for the camera.
4. Create a scene with your own meshes, sky and lighting. Assign Lucent Path materials. [Shader SDK](Assets/LucentPath/Shaders/Sdk/README.md)
5. For Streamline, build the native bridge and install the NVIDIA runtime. [Native build instructions](Native/LucentPathStreamline/README.md)


## License

[PolyForm Noncommercial License 1.0.0](LICENSE.md).
