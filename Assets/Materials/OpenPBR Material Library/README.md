# OpenPBR Material Library

This folder contains production-oriented OpenPBR material presets for `Path Tracing RP/Default`.
Presets are grouped by category folder so the library stays compact and easy to browse in Unity.

## Coverage

- `00_Dielectric_Base`: opaque dielectric base materials with separate base diffuse roughness and specular roughness, anisotropy, and coat examples.
- `01_Metal_Conductors`: metalness/conductor presets using OpenPBR `base_metalness`, `base_color` reflectivity, `specular_color` edge tint, roughness, and anisotropy controls.
- `02_Dielectric_Transmission`: thick transmission and thin-walled glass/film cases using OpenPBR transmission and geometry thin-walled controls.
- `03_Subsurface_Solids`: skin, marble, wax, jade, milk, soap, and food-like solid SSS presets.
- `04_Thin_TwoSided_SSS`: leaves, paper, parchment, lithophane porcelain, petals, membranes, and other thin two-sided SSS cases.
- `05_Coated_SSS_OpenPBR`: coated subsurface materials where the dielectric coat sits above scattering base media.
- `06_Special_Controls`: alpha clip, emission, fuzz, thin-film, high anisotropy, dense/large radius SSS, partial metallic, and cull-mode diagnostics.

## Authoring Notes

- Every preset in this folder is normalized against OpenPBR Surface v1.1.1 defaults and then authored from the official ASWF `examples/open_pbr_*.mtlx` files where the project shader exposes the corresponding input.
- `_OpenPbrSubsurfaceRadius` is authored in millimeters. The shader converts it to Unity scene units internally, so these values should not be pre-scaled to meters in the material asset.
- Thin SSS and thin glass presets use `_OpenPbrGeometryThinWalled = 1` and `_Cull = 0` so both sides are shaded.
- Conductor presets use `_OpenPbrBaseMetalness = 1`; metallic reflectivity is authored through `_BaseColor` and edge tint through `_OpenPbrSpecularColor`, matching OpenPBR 1.1.1.
- Dielectric glass uses `_OpenPbrTransmissionWeight = 1`; thin panes/films additionally enable `_OpenPbrGeometryThinWalled`.
- Coated SSS presets follow the OpenPBR layering idea: coat reflection above a scattering dielectric base, not a white mirror pasted on top.
- Spectral dispersion is intentionally absent because the renderer is RGB. Thin-film thickness is authored in micrometers, matching OpenPBR 1.1.1.
- The exact official source mapping for each Unity material is recorded in `ASWF_OpenPBR_1.1.1_Source_Map.md`.

## References

- OpenPBR Surface specification v1.1.1, Academy Software Foundation.
- OpenPBR v1.1.1 official MaterialX reference and examples, Academy Software Foundation.
- Unity ShaderLab Properties and `ShaderGUI` documentation: material assets store shader property values by property name.
- Unity HDRP physical units documentation: 1 Unity unit is conventionally 1 meter.
