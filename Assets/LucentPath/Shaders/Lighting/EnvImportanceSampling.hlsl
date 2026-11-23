#ifndef LUCENT_PATH_ENV_IMPORTANCE_SAMPLING_INCLUDED
#define LUCENT_PATH_ENV_IMPORTANCE_SAMPLING_INCLUDED

#ifndef LUCENT_PATH_ENV_IMPORTANCE_DECLARED
#define LUCENT_PATH_ENV_IMPORTANCE_DECLARED
StructuredBuffer<float> _EnvCdfBuffer;
StructuredBuffer<float> _EnvLuminanceBuffer;
StructuredBuffer<float> _EnvTotalLuminanceBuffer;
StructuredBuffer<float> _EnvAliasProbBuffer;
StructuredBuffer<uint> _EnvAliasIndexBuffer;
int _EnvSamplingResolution;
int _EnvSamplingReady;
int _EnvNeeEnabled;
int _SkyObjectLighting;
#endif

float3 EnvDirectionFromFaceUv(uint face, float u, float v)
{
    float a = u * 2.0 - 1.0;
    float b = v * 2.0 - 1.0;
    float3 dir;
    switch (face)
    {
        case 0u: dir = float3( 1.0, -b, -a); break;
        case 1u: dir = float3(-1.0, -b,  a); break;
        case 2u: dir = float3( a,  1.0,  b); break;
        case 3u: dir = float3( a, -1.0, -b); break;
        case 4u: dir = float3( a, -b,  1.0); break;
        case 5u: dir = float3(-a, -b, -1.0); break;
        default: dir = float3(0.0, 1.0, 0.0); break;
    }
    return normalize(dir);
}

uint EnvDirectionToFlatIndex(float3 dir, uint resolution, out uint outFace, out float outU, out float outV)
{
    float ax = abs(dir.x);
    float ay = abs(dir.y);
    float az = abs(dir.z);
    float ma;
    float sc;
    float tc;
    uint face;
    if (ax >= ay && ax >= az)
    {
        ma = ax;
        if (dir.x >= 0.0) { face = 0u; sc = -dir.z; tc = -dir.y; }
        else              { face = 1u; sc =  dir.z; tc = -dir.y; }
    }
    else if (ay >= az)
    {
        ma = ay;
        if (dir.y >= 0.0) { face = 2u; sc =  dir.x; tc =  dir.z; }
        else              { face = 3u; sc =  dir.x; tc = -dir.z; }
    }
    else
    {
        ma = az;
        if (dir.z >= 0.0) { face = 4u; sc =  dir.x; tc = -dir.y; }
        else              { face = 5u; sc = -dir.x; tc = -dir.y; }
    }
    float u = 0.5 * (sc / max(ma, 1e-6) + 1.0);
    float v = 0.5 * (tc / max(ma, 1e-6) + 1.0);
    u = clamp(u, 0.0, 1.0);
    v = clamp(v, 0.0, 1.0);
    outFace = face;
    outU = u;
    outV = v;
    uint x = min((uint)(u * (float)resolution), (uint)resolution - 1u);
    uint y = min((uint)(v * (float)resolution), (uint)resolution - 1u);
    return (face * (uint)resolution + y) * (uint)resolution + x;
}

float EnvTexelSolidAngle(float u, float v, uint resolution)
{
    float a = u * 2.0 - 1.0;
    float b = v * 2.0 - 1.0;
    float texelSize = 2.0 / (float)resolution;
    float aMinusHalf = a - 0.5 * texelSize;
    float aPlusHalf  = a + 0.5 * texelSize;
    float bMinusHalf = b - 0.5 * texelSize;
    float bPlusHalf  = b + 0.5 * texelSize;
    float omega1 = atan2(aMinusHalf * bMinusHalf, sqrt(aMinusHalf * aMinusHalf + bMinusHalf * bMinusHalf + 1.0));
    float omega2 = atan2(aPlusHalf  * bMinusHalf, sqrt(aPlusHalf  * aPlusHalf  + bMinusHalf * bMinusHalf + 1.0));
    float omega3 = atan2(aMinusHalf * bPlusHalf,  sqrt(aMinusHalf * aMinusHalf + bPlusHalf  * bPlusHalf  + 1.0));
    float omega4 = atan2(aPlusHalf  * bPlusHalf,  sqrt(aPlusHalf  * aPlusHalf  + bPlusHalf  * bPlusHalf  + 1.0));
    return max(omega1 - omega2 - omega3 + omega4, 1e-8);
}

uint EnvCdfBinarySearch(float u)
{
    uint total = (uint)_EnvSamplingResolution * (uint)_EnvSamplingResolution * 6u;
    uint lo = 0u;
    uint hi = total;
    while (lo < hi)
    {
        uint mid = (lo + hi) / 2u;
        if (_EnvCdfBuffer[mid] < u) lo = mid + 1u;
        else hi = mid;
    }
    return min(lo, total - 1u);
}

bool SampleEnvironmentImportance(float u1, float2 jitter, out float3 outDirection, out float3 outRadiance, out float outPdf)
{
    outDirection = float3(0.0, 1.0, 0.0);
    outRadiance = 0.0;
    outPdf = 0.0;

    if (_EnvSamplingReady == 0 || _EnvSamplingResolution <= 0)
        return false;

    float total = _EnvTotalLuminanceBuffer[0];
    if (total <= 1e-8)
        return false;

    uint res = (uint)_EnvSamplingResolution;
    uint binCount = res * res * 6u;
    float scaled = saturate(u1) * (float)binCount;
    uint bin = min((uint)scaled, binCount - 1u);
    float aliasU = scaled - (float)bin;
    uint idx = (aliasU < _EnvAliasProbBuffer[bin]) ? bin : _EnvAliasIndexBuffer[bin];
    uint face = idx / (res * res);
    uint within = idx - face * res * res;
    uint y = within / res;
    uint x = within - y * res;
    float jx = saturate(jitter.x);
    float jy = saturate(jitter.y);
    float u = ((float)x + jx) / (float)res;
    float v = ((float)y + jy) / (float)res;
    outDirection = EnvDirectionFromFaceUv(face, u, v);
    outRadiance = EvaluateHdriCubemap(outDirection);

    float weight = _EnvLuminanceBuffer[idx];
    float solidAngle = EnvTexelSolidAngle(u, v, res);
    outPdf = weight / (total * solidAngle);
    return outPdf > 0.0;
}

float EvaluateEnvironmentPdf(float3 direction)
{
    if (_EnvSamplingReady == 0 || _EnvSamplingResolution <= 0)
        return 0.0;

    float total = _EnvTotalLuminanceBuffer[0];
    if (total <= 1e-8)
        return 0.0;

    uint face;
    float u;
    float v;
    uint idx = EnvDirectionToFlatIndex(direction, (uint)_EnvSamplingResolution, face, u, v);
    float weight = _EnvLuminanceBuffer[idx];
    float solidAngle = EnvTexelSolidAngle(u, v, (uint)_EnvSamplingResolution);
    return weight / (total * solidAngle);
}

#endif
