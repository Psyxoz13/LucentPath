#ifndef LUCENT_MATERIAL_SDK_INCLUDED
#define LUCENT_MATERIAL_SDK_INCLUDED

// Public authoring surface description ("closure bundle"). A material shader's
// SurfaceFunction fills this; the fixed pipeline integrator samples it. The author
// never writes the integrator (NEE/MIS/GI) — only this surface description.
struct LucentSurface
{
    // Geometry / shading frame (a SurfaceFunction may perturb these).
    float3 positionWS;
    float3 normalWS;
    float3 geomNormalWS;
    float4 tangentWS;
    float2 uv;
    float  alpha;
    uint   materialModel;
    bool   thinWalled;
    bool   hasExplicitConductor;

    // Base layer.
    float3 baseColor;
    float  baseWeight;
    float  baseMetalness;
    float  baseDiffuseRoughness;

    // Specular / conductor.
    float  specularWeight;
    float3 specularColor;
    float  specularRoughness;
    float  specularAnisotropy;
    float  ior;
    float  conductorWeight;
    float3 conductorEta;
    float3 conductorK;
    float3 conductorEdgeTint;

    // Transmission.
    float  transmissionWeight;
    float3 transmissionColor;
    float  transmissionDepth;

    // Coat.
    float3 coatColor;
    float  coatWeight;
    float  coatRoughness;
    float  coatAnisotropy;
    float  coatIor;
    float  coatDarkening;
    float  coatAffectColor;
    float  coatAffectRoughness;

    // Fuzz / sheen.
    float3 fuzzColor;
    float  fuzzWeight;
    float  fuzzRoughness;

    // Thin film.
    float  thinFilmWeight;
    float  thinFilmThickness;
    float  thinFilmIor;

    // Subsurface.
    float3 subsurfaceColor;
    float  subsurfaceWeight;
    float3 subsurfaceRadius;
    float  subsurfaceAnisotropy;

    // Emission / geometry.
    float3 emission;
    float  geometryOpacity;
};

// Ray classification handed to SurfaceFunction (Unreal-style ray-type switch). Lets a material
// author cheaper closures for non-camera rays.
#define LUCENT_RAY_TYPE_CAMERA   0u
#define LUCENT_RAY_TYPE_INDIRECT 1u

// Geometry/context handed to the author's SurfaceFunction at a hit.
struct LucentHitContext
{
    float3 positionWS;
    float3 geomNormalWS;
    float3 shadingNormalWS;
    float4 tangentWS;
    float2 uv;
    float3 viewDirWS;
    uint   instanceId;
    bool   frontFace;
    uint   rayType;
};

// Neutral closure bundle seeded with the hit geometry. Used by from-scratch materials
// (LUCENT_PATH_SURFACE_FROM_SCRATCH) whose SurfaceFunction authors everything itself, so they
// need none of the pipeline's OpenPBR/standard material properties.
LucentSurface LucentSurfaceDefault(LucentHitContext ctx)
{
    LucentSurface s;
    s.positionWS = ctx.positionWS;
    s.normalWS = ctx.shadingNormalWS;
    s.geomNormalWS = ctx.geomNormalWS;
    s.tangentWS = ctx.tangentWS;
    s.uv = ctx.uv;
    s.alpha = 1.0;
    s.materialModel = 0u;
    s.thinWalled = false;
    s.hasExplicitConductor = false;
    s.baseColor = float3(0.5, 0.5, 0.5);
    s.baseWeight = 1.0;
    s.baseMetalness = 0.0;
    s.baseDiffuseRoughness = 0.5;
    s.specularWeight = 1.0;
    s.specularColor = float3(1.0, 1.0, 1.0);
    s.specularRoughness = 0.5;
    s.specularAnisotropy = 0.0;
    s.ior = 1.5;
    s.conductorWeight = 0.0;
    s.conductorEta = float3(1.0, 1.0, 1.0);
    s.conductorK = float3(0.0, 0.0, 0.0);
    s.conductorEdgeTint = float3(1.0, 1.0, 1.0);
    s.transmissionWeight = 0.0;
    s.transmissionColor = float3(1.0, 1.0, 1.0);
    s.transmissionDepth = 1.0;
    s.coatColor = float3(1.0, 1.0, 1.0);
    s.coatWeight = 0.0;
    s.coatRoughness = 0.0;
    s.coatAnisotropy = 0.0;
    s.coatIor = 1.5;
    s.coatDarkening = 1.0;
    s.coatAffectColor = 0.0;
    s.coatAffectRoughness = 0.0;
    s.fuzzColor = float3(1.0, 1.0, 1.0);
    s.fuzzWeight = 0.0;
    s.fuzzRoughness = 0.3;
    s.thinFilmWeight = 0.0;
    s.thinFilmThickness = 0.5;
    s.thinFilmIor = 1.5;
    s.subsurfaceColor = float3(0.0, 0.0, 0.0);
    s.subsurfaceWeight = 0.0;
    s.subsurfaceRadius = float3(1.0, 1.0, 1.0);
    s.subsurfaceAnisotropy = 0.0;
    s.emission = float3(0.0, 0.0, 0.0);
    s.geometryOpacity = 1.0;
    return s;
}

// Material shaders override this (define LUCENT_PATH_HAS_SURFACE_FUNCTION) to author their
// surface. Default passes the pipeline-resolved surface through unchanged.
#ifndef LUCENT_PATH_HAS_SURFACE_FUNCTION
LucentSurface SurfaceFunction(LucentHitContext ctx, LucentSurface baseSurface)
{
    return baseSurface;
}
#endif

#endif
