#ifndef LUCENT_PATH_COMMON_INCLUDED
#define LUCENT_PATH_COMMON_INCLUDED

#define PI 3.14159265359
#define INV_PI 0.31830988618
#define MAX_STACK_SIZE 128
#define EPSILON 1e-5

#define LIGHT_TYPE_DIRECTIONAL 0.0
#define LIGHT_TYPE_POINT 1.0
#define LIGHT_TYPE_SPOT 2.0
#define LIGHT_TYPE_RECTANGLE 3.0
#define LIGHT_TYPE_DISC 4.0

#define MATERIAL_MODEL_STANDARD 0.0
#define MATERIAL_MODEL_DIFFUSE 1.0
#define MATERIAL_MODEL_CONDUCTOR 2.0
#define MATERIAL_MODEL_DIELECTRIC 3.0
#define MATERIAL_MODEL_THIN_DIELECTRIC 4.0
#define MATERIAL_MODEL_OPENPBR 5.0

#define BXDF_FLAG_REFLECTION 1u
#define BXDF_FLAG_TRANSMISSION 2u
#define BXDF_FLAG_DIFFUSE 4u
#define BXDF_FLAG_GLOSSY 8u
#define BXDF_FLAG_SPECULAR 16u
#define BXDF_FLAG_ALL 31u

#define PATH_SIGNAL_SURFACE 0u
#define PATH_SIGNAL_REFLECTION 1u
#define PATH_SIGNAL_TRANSMISSION 2u

#define PATH_PLANE_UNSPLIT 0
#define PATH_PLANE_MAIN 1
#define PATH_PLANE_DELTA_REFLECTION 2
#define PATH_PLANE_SECOND_DELTA_REFLECTION 3

#define TRANSPORT_MODE_RADIANCE 0u
#define TRANSPORT_MODE_IMPORTANCE 1u

#define SUBSURFACE_RADIUS_MM_TO_SCENE_UNITS 0.001
#define SUBSURFACE_MIN_RADIUS_SCENE_UNITS 1e-7

#define GUIDE_PATH_KIND_PRIMARY 0.0
#define GUIDE_PATH_KIND_SPECULAR 1.0
#define GUIDE_PATH_KIND_TRANSMISSION 2.0
#define GUIDE_PATH_KIND_SHADOW_BLOCKER 3.0
#define GUIDE_PATH_KIND_SHADOW_UNBLOCKED 4.0

float ResolveMaterialIor(float rawIor)
{
    return rawIor > 0.0 ? max(rawIor, 1.0) : 1.5;
}

struct GpuLight
{
    float4 positionRange;
    float4 directionType;
    float4 colorIntensity;
    float4 spotAngles;
    float4 areaData;
    float4 right;
    float4 up;
};

struct Ray
{
    float3 origin;
    float3 direction;
    float maxDistance;
};

struct Hit
{
    uint didHit;
    float t;
    int triIndex;
    int instanceIndex;
    float2 bary;
};

float ComputeLuminance(float3 color)
{
    return dot(color, float3(0.2126, 0.7152, 0.0722));
}

float3 RGBToYCoCg(float3 color)
{
    float Co = color.r - color.b;
    float temp = color.b + Co * 0.5;
    float Cg = color.g - temp;
    float Y = temp + Cg * 0.5;
    return float3(Y, Co, Cg);
}

float3 YCoCgToRGB(float3 color)
{
    float temp = color.x - color.z * 0.5;
    float g = color.z + temp;
    float b = temp - color.y * 0.5;
    float r = b + color.y;
    return float3(r, g, b);
}

float Max3(float3 v)
{
    return max(v.x, max(v.y, v.z));
}

bool IsSkyGuide(float4 guide0)
{
    return guide0.w < 0.0;
}

float EncodeShadowVisibility(float occlusion)
{
    return 0.5 + 0.5 * saturate(occlusion);
}

static const float SPECULAR_LOBE_DIRECTION_FLOOR = 1.5 / 255.0;

float GetSpecularLobeTanHalfAngle(float roughness, float lobeVolume)
{
    return saturate(roughness) * sqrt(lobeVolume / (1.0 - lobeVolume + 1e-6));
}

float GetLinearFalloffWeight(float x)
{
    return smoothstep(1.0, 0.0, abs(x));
}

bool ShadowVisibilitySampled(float encoded)
{
    return encoded > 0.25;
}

float DecodeShadowVisibility(float encoded)
{
    return saturate(encoded * 2.0 - 1.0);
}

float BlendShadowVisibility(float encodedFrame, float prevMask, bool historyAccepted, float alpha)
{
    float result = historyAccepted ? prevMask : 0.0;
    if (ShadowVisibilitySampled(encodedFrame))
    {
        float occlusion = DecodeShadowVisibility(encodedFrame);
        result = historyAccepted ? lerp(prevMask, occlusion, alpha) : occlusion;
    }
    return result;
}

bool IsParallaxGuide(float4 guide2)
{
    return guide2.x == GUIDE_PATH_KIND_SPECULAR || guide2.x == GUIDE_PATH_KIND_TRANSMISSION;
}

bool SamePathChain(float4 currentGuide2, float4 prevGuide2)
{
    bool currentParallax = IsParallaxGuide(currentGuide2);
    bool prevParallax = IsParallaxGuide(prevGuide2);
    bool sameKind = currentGuide2.x == prevGuide2.x;
    bool sameParallaxObject = !currentParallax ||
        (currentGuide2.y == prevGuide2.y && currentGuide2.z == prevGuide2.z);

    return sameKind && currentParallax == prevParallax && sameParallaxObject;
}

float3 SubsurfaceRadiusMmToSceneUnits(float3 radiusMm, float scale)
{
    return max(
        radiusMm * max(scale, 1e-5) * SUBSURFACE_RADIUS_MM_TO_SCENE_UNITS,
        float3(SUBSURFACE_MIN_RADIUS_SCENE_UNITS, SUBSURFACE_MIN_RADIUS_SCENE_UNITS, SUBSURFACE_MIN_RADIUS_SCENE_UNITS));
}

#endif
