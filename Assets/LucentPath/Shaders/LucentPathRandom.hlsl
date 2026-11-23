#ifndef LUCENT_PATH_RANDOM_INCLUDED
#define LUCENT_PATH_RANDOM_INCLUDED

#ifndef LUCENT_PATH_BLUE_NOISE_DECLARED
#define LUCENT_PATH_BLUE_NOISE_DECLARED
Texture2DArray<float> _BlueNoiseTexture;
int _BlueNoiseEnabled;
int _BlueNoiseLayerCount;
int _BlueNoiseTileWidth;
int _BlueNoiseTileHeight;
#endif

int _PathPlane;

#define SOBOL_MATRIX_SIZE 52u

#define SOBOL_CAMERA_JITTER_DIM_X 0u
#define SOBOL_CAMERA_JITTER_DIM_Y 1u
#define SOBOL_LENS_DIM_X 2u
#define SOBOL_LENS_DIM_Y 3u
#define SOBOL_TIME_DIM 4u

#define SOBOL_BOUNCE_DIM_BASE 5u
#define SOBOL_BOUNCE_DIM_STRIDE 16u

#define SOBOL_BSDF_SELECT_OFFSET 0u
#define SOBOL_BSDF_SAMPLE_U_OFFSET 1u
#define SOBOL_BSDF_SAMPLE_V_OFFSET 2u
#define SOBOL_LIGHT_PICK_OFFSET 3u
#define SOBOL_LIGHT_SAMPLE_U_OFFSET 4u
#define SOBOL_LIGHT_SAMPLE_V_OFFSET 5u
#define SOBOL_RR_OFFSET 6u
#define SOBOL_SSS_LOBE_SELECT_OFFSET 7u
#define SOBOL_SSS_DIRECTION_U_OFFSET 8u
#define SOBOL_SSS_DIRECTION_V_OFFSET 9u
#define SOBOL_SSS_DISTANCE_OFFSET 10u
#define SOBOL_VOLUME_PHASE_U_OFFSET 11u
#define SOBOL_VOLUME_PHASE_V_OFFSET 12u
#define SOBOL_VOLUME_DISTANCE_OFFSET 13u
#define SOBOL_GUIDING_SELECT_OFFSET 14u
#define SOBOL_RIS_AUX_OFFSET 15u

struct PathSampler
{
    uint2 pixel;
    uint sampleIndex;
    uint pixelSeed;
    uint planeSeed;
    uint blueNoiseLayer;
};

uint Hash(uint x)
{
    x ^= 2747636419u;
    x *= 2654435769u;
    x ^= x >> 16;
    x *= 2654435769u;
    x ^= x >> 16;
    x *= 2654435769u;
    return x;
}

uint Hash2(uint2 x)
{
    return Hash(x.x ^ Hash(x.y + 0x9E3779B9u));
}

float Random01(inout uint state)
{
    state = Hash(state);
    return (state & 0x00FFFFFFu) / 16777216.0;
}

float UintToFloat01(uint x)
{
    return x * 2.3283064365386963e-10;
}

#ifdef UNITY_COMPILER_HLSLCC
uint ReverseBitsSafe(uint x)
{
    x = ((x >> 1) & 0x55555555u) | ((x & 0x55555555u) << 1);
    x = ((x >> 2) & 0x33333333u) | ((x & 0x33333333u) << 2);
    x = ((x >> 4) & 0x0f0f0f0fu) | ((x & 0x0f0f0f0fu) << 4);
    x = ((x >> 8) & 0x00ff00ffu) | ((x & 0x00ff00ffu) << 8);
    x = ((x >> 16) & 0xffffu) | ((x & 0xffffu) << 16);
    return x;
}
#else
#define ReverseBitsSafe reversebits
#endif

uint LaineKarrasPermutation(uint x, uint seed)
{
    x ^= x * 0x3d20adeau;
    x += seed;
    x *= (seed >> 16) | 1u;
    x ^= x * 0x05526c56u;
    x ^= x * 0x53a22864u;
    return x;
}

uint NestedUniformOwenScramble(uint x, uint seed)
{
    x = ReverseBitsSafe(x);
    x = LaineKarrasPermutation(x, seed);
    x = ReverseBitsSafe(x);
    return x;
}

uint SobolSampleUint(uint index, uint dimension)
{
    uint result = 0u;
    uint offset = dimension * SOBOL_MATRIX_SIZE;

    while (index != 0u)
    {
        uint bit = firstbitlow(index);
        result ^= _SobolMatricesBuffer[offset + bit];
        index &= index - 1u;
    }

    return result;
}

bool IsBlueNoiseAvailable()
{
    return _BlueNoiseEnabled != 0 && _BlueNoiseLayerCount > 0 && _BlueNoiseTileWidth > 0 && _BlueNoiseTileHeight > 0;
}

uint GetBlueNoiseCranleyPattersonBits(uint2 pixel, uint layer, uint dimension)
{
    float2 r2 = frac(0.5 + (float)dimension * float2(0.75487766624669276, 0.56984029099805326));
    uint shiftedX = (pixel.x + (uint)(r2.x * _BlueNoiseTileWidth)) % (uint)_BlueNoiseTileWidth;
    uint shiftedY = (pixel.y + (uint)(r2.y * _BlueNoiseTileHeight)) % (uint)_BlueNoiseTileHeight;
    float bn = _BlueNoiseTexture.Load(int4(shiftedX, shiftedY, layer, 0)).r;
    uint blueHighBits = (uint)(saturate(bn) * 255.0 + 0.5) << 24;
    uint whiteLowBits = Hash((pixel.x * 0x9E3779B9u) ^ (pixel.y * 0x85EBCA6Bu) ^ (dimension * 0xC2B2AE35u)) >> 8;
    return blueHighBits | whiteLowBits;
}

uint GetPathPlaneSeed()
{
    if (_PathPlane < PATH_PLANE_DELTA_REFLECTION)
    {
        return 0u;
    }

    return Hash((uint)_PathPlane * 0x6A09E667u);
}

PathSampler CreatePathSampler(uint2 pixel, uint sampleIndex, uint planeSeed)
{
    PathSampler pathSampler;
    pathSampler.pixel = pixel;
    pathSampler.sampleIndex = sampleIndex;
    pathSampler.planeSeed = planeSeed;
    pathSampler.pixelSeed = Hash2(pixel) ^ planeSeed;
    pathSampler.blueNoiseLayer = _BlueNoiseLayerCount > 0 ? sampleIndex % (uint)_BlueNoiseLayerCount : 0u;
    return pathSampler;
}

PathSampler CreatePathSampler(uint2 pixel, uint sampleIndex)
{
    return CreatePathSampler(pixel, sampleIndex, GetPathPlaneSeed());
}

uint GetBounceDimensionBase(uint bounce)
{
    return SOBOL_BOUNCE_DIM_BASE + bounce * SOBOL_BOUNCE_DIM_STRIDE;
}

uint GetSobolSampleBits(PathSampler pathSampler, uint dimension)
{
    uint sobol = SobolSampleUint(pathSampler.sampleIndex, dimension);
    if (IsBlueNoiseAvailable())
    {
        uint scrambled = NestedUniformOwenScramble(sobol, Hash((dimension * 0x9E3779B9u) ^ 0xBB67AE85u ^ pathSampler.planeSeed));
        return scrambled + GetBlueNoiseCranleyPattersonBits(pathSampler.pixel, pathSampler.blueNoiseLayer, dimension);
    }
    return NestedUniformOwenScramble(sobol, Hash(pathSampler.pixelSeed ^ (dimension * 0x9E3779B9u) ^ 0xBB67AE85u));
}

float GetSobolSample(PathSampler pathSampler, uint dimension)
{
    return UintToFloat01(GetSobolSampleBits(pathSampler, dimension));
}

float2 GetSobolSample2(PathSampler pathSampler, uint dimensionBase)
{
    return float2(
        GetSobolSample(pathSampler, dimensionBase),
        GetSobolSample(pathSampler, dimensionBase + 1u));
}

float2 GetCameraJitterSample(PathSampler pathSampler)
{
    return float2(
        GetSobolSample(pathSampler, SOBOL_CAMERA_JITTER_DIM_X),
        GetSobolSample(pathSampler, SOBOL_CAMERA_JITTER_DIM_Y));
}

float2 GetLensUvSample(PathSampler pathSampler)
{
    return float2(
        GetSobolSample(pathSampler, SOBOL_LENS_DIM_X),
        GetSobolSample(pathSampler, SOBOL_LENS_DIM_Y));
}

float GetShutterTimeSample(PathSampler pathSampler)
{
    return GetSobolSample(pathSampler, SOBOL_TIME_DIM);
}

float GetWeyl1D(float offset, uint index)
{
    return frac(offset + (float)((index * 10368889u) & 0x00FFFFFFu) / 16777216.0);
}

float GetBounceBsdfSelector(PathSampler pathSampler, uint bounce)
{
    return GetSobolSample(pathSampler, GetBounceDimensionBase(bounce) + SOBOL_BSDF_SELECT_OFFSET);
}

float2 GetBounceBsdfSample(PathSampler pathSampler, uint bounce)
{
    return GetSobolSample2(pathSampler, GetBounceDimensionBase(bounce) + SOBOL_BSDF_SAMPLE_U_OFFSET);
}

float GetBounceLightPickSample(PathSampler pathSampler, uint bounce)
{
    return GetSobolSample(pathSampler, GetBounceDimensionBase(bounce) + SOBOL_LIGHT_PICK_OFFSET);
}

float2 GetBounceLightSample(PathSampler pathSampler, uint bounce)
{
    return GetSobolSample2(pathSampler, GetBounceDimensionBase(bounce) + SOBOL_LIGHT_SAMPLE_U_OFFSET);
}

float GetBounceRouletteSample(PathSampler pathSampler, uint bounce)
{
    return GetSobolSample(pathSampler, GetBounceDimensionBase(bounce) + SOBOL_RR_OFFSET);
}

float GetBounceSssLobeSelector(PathSampler pathSampler, uint bounce)
{
    return GetSobolSample(pathSampler, GetBounceDimensionBase(bounce) + SOBOL_SSS_LOBE_SELECT_OFFSET);
}

float2 GetBounceSssDirectionSample(PathSampler pathSampler, uint bounce)
{
    return GetSobolSample2(pathSampler, GetBounceDimensionBase(bounce) + SOBOL_SSS_DIRECTION_U_OFFSET);
}

float GetBounceSssDistanceSample(PathSampler pathSampler, uint bounce)
{
    return GetSobolSample(pathSampler, GetBounceDimensionBase(bounce) + SOBOL_SSS_DISTANCE_OFFSET);
}

float2 GetBounceVolumePhaseSample(PathSampler pathSampler, uint bounce)
{
    return GetSobolSample2(pathSampler, GetBounceDimensionBase(bounce) + SOBOL_VOLUME_PHASE_U_OFFSET);
}

float GetBounceVolumeDistanceSample(PathSampler pathSampler, uint bounce)
{
    return GetSobolSample(pathSampler, GetBounceDimensionBase(bounce) + SOBOL_VOLUME_DISTANCE_OFFSET);
}

float GetBounceGuidingSelector(PathSampler pathSampler, uint bounce)
{
    return GetSobolSample(pathSampler, GetBounceDimensionBase(bounce) + SOBOL_GUIDING_SELECT_OFFSET);
}

float GetBounceRisAuxSample(PathSampler pathSampler, uint bounce)
{
    return GetSobolSample(pathSampler, GetBounceDimensionBase(bounce) + SOBOL_RIS_AUX_OFFSET);
}

uint GetBounceLightSeed(PathSampler pathSampler, uint bounce)
{
    uint baseDim = GetBounceDimensionBase(bounce);
    uint seed = Hash(pathSampler.pixelSeed ^ 0xA511E9B3u);
    seed = Hash(seed ^ GetSobolSampleBits(pathSampler, baseDim + SOBOL_LIGHT_PICK_OFFSET));
    seed = Hash(seed ^ GetSobolSampleBits(pathSampler, baseDim + SOBOL_LIGHT_SAMPLE_U_OFFSET));
    seed = Hash(seed ^ GetSobolSampleBits(pathSampler, baseDim + SOBOL_LIGHT_SAMPLE_V_OFFSET));
    return seed;
}

#endif
