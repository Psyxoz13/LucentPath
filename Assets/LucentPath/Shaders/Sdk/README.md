# LucentPath Material Authoring SDK

Authoring model (same separation as Unreal / Cycles / OptiX): a material describes **only its
surface** as a *closure bundle*; the fixed pipeline owns the **integrator** (NEE, MIS,
environment importance sampling, GI, Russian roulette, subsurface, volume) and the denoiser. A
material author **never edits the pipeline** — only one function:

```hlsl
LucentSurface SurfaceFunction(LucentHitContext ctx, LucentSurface baseSurface);
```

- `LucentHitContext` — hit geometry: `positionWS`, `geomNormalWS`, `shadingNormalWS`,
  `tangentWS`, `uv`, `viewDirWS`, `instanceId`, `frontFace`, and `rayType`
  (`LUCENT_RAY_TYPE_CAMERA` for camera-visible hits, `LUCENT_RAY_TYPE_INDIRECT` for GI/secondary
  bounces) — branch on it to author cheaper closures for non-camera rays (Unreal-style ray-type
  switch).
- `LucentSurface` — the canonical closure bundle the integrator decodes: base color/weight/metalness,
  specular (weight/color/roughness/anisotropy/ior), conductor (eta/k/edge-tint/weight),
  transmission (weight/color/depth), coat, fuzz (sheen), thin-film, subsurface, emission,
  geometry (`normalWS`, `tangentWS`, `alpha`, `geometryOpacity`, `materialModel`, `thinWalled`).
- `baseSurface` — surface already resolved by the pipeline (see modes below); return it modified.

## Two authoring modes

### A. Override an existing material (OpenPBR / standard)
The pipeline resolves the surface from the material's properties and hands it to `SurfaceFunction`.
Default `SurfaceFunction` returns it unchanged (identical to no authoring). Override to tweak:

```hlsl
#define LUCENT_PATH_HAS_SURFACE_FUNCTION
LucentSurface SurfaceFunction(LucentHitContext ctx, LucentSurface s)
{
    s.specularRoughness = lerp(s.specularRoughness, 1.0, /* procedural wear */ ...);
    return s;
}
#include "Assets/LucentPath/Shaders/DXR/DXRLucentPathMaterialClosestHit.hlsl"
```

### B. Author from scratch (custom material, no pipeline properties)
Define `LUCENT_PATH_SURFACE_FROM_SCRATCH`: the pipeline reads **no** OpenPBR/standard properties and
hands `SurfaceFunction` a neutral surface seeded with hit geometry (`LucentSurfaceDefault`). Your
`SurfaceFunction` fills the closures from your own properties. This is how you write a slim
diffuse+specular (or any) material that compiles with only the lobes you use.

See the complete example: `Shaders/Materials/Examples/LucentSurfaceFunctionExample.shader`
(declares its own `_BaseMap/_BaseColor/_Metallic/_Roughness/_EmissionColor` and authors a slim
diffuse + specular surface in `SurfaceFunction`).

## Material shader skeleton (ray-tracing pass)

```hlsl
Pass
{
    Name "RayTracing"
    Tags { "LightMode" = "RayTracing" }
    HLSLPROGRAM
    #pragma target 6.3
    #pragma require RayTracing
    #pragma raytracing DXRLucentPathPass

    #define LUCENT_PATH_SURFACE_FROM_SCRATCH 1   // optional (mode B)
    #define LUCENT_PATH_HAS_SURFACE_FUNCTION 1
    #include "Assets/LucentPath/Shaders/Sdk/LucentMaterialSdk.hlsl"

    LucentSurface SurfaceFunction(LucentHitContext ctx, LucentSurface s) { /* author */ return s; }

    #include "Assets/LucentPath/Shaders/DXR/DXRLucentPathMaterialClosestHit.hlsl"
    ENDHLSL
}
```

Geometry (vertex fetch, tangent frame) and any-hit alpha test are provided by the closest-hit; the
RT pass still needs `_BaseMap`/`_BaseColor`/`_BaseMap_ST`/`_EmissionMap_ST`/`_AlphaClip`/`_Cutoff`
declared (used by geometry + any-hit) plus whatever your `SurfaceFunction` reads.

## SDK files
- `LucentMaterialSdk.hlsl` — `LucentSurface`, `LucentHitContext`, `LucentSurfaceDefault`,
  default `SurfaceFunction`.
- `LucentStandardHit.hlsl` — `LucentPackSurface(hit, LucentSurface)` packs the closure bundle into
  the `GpuSurfaceHit` G-buffer the fixed wavefront integrator decodes.

## What you must NOT do
- Do not write the integrator / sample lights / trace shadow or bounce rays — the pipeline owns it.
- Do not edit pipeline passes; author only `SurfaceFunction`.
