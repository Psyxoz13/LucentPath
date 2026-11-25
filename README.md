# Lucent Path

A research renderer for real-time path tracing in Unity URP. The DXR backend uses wavefront ray and compute passes, compacts active paths, and supports OpenPBR materials, temporal accumulation, and à-trous filtering. The native C++ bridge integrates NVIDIA Streamline for DLSS Ray Reconstruction.

## Setup

1. Open the project with the Unity 6.6 version recorded in ProjectSettings/ProjectVersion.txt.
2. Run on Windows with a ray-tracing-capable GPU and Direct3D 12.
3. The project already selects the PC URP asset and the Lucent Path renderer. Set the camera renderer to Use Pipeline Settings.
4. Create a scene with your own meshes and lighting. Use the OpenPBR material library in Assets/Materials and the sky material in Assets/LucentPath/Sky. [Shader SDK](Assets/LucentPath/Shaders/Sdk/README.md)
5. For Streamline, build the native bridge and install the NVIDIA runtime. [Native build instructions](Native/LucentPathStreamline/README.md)


For the optional AgX tonemapper, download the [original contrast LUT](https://raw.githubusercontent.com/sobotka/AgX/main/LUTs/AgX_Default_Contrast.spi1d), import it into Assets without changing its bytes, and assign it to Default Contrast Lut in the Lucent Tonemap volume override.

The included [Citrus Orchard (Pure Sky)](https://polyhaven.com/a/citrus_orchard_puresky) HDRI is distributed under [Poly Haven CC0](https://polyhaven.com/license).

## License

[PolyForm Noncommercial License 1.0.0](LICENSE.md).
