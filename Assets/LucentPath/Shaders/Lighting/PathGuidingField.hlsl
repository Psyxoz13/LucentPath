#ifndef LUCENT_PATH_GUIDING_FIELD_INCLUDED
#define LUCENT_PATH_GUIDING_FIELD_INCLUDED

#include "SpatialHashGrid.hlsl"

static const uint GuideFieldBinAxis = 8u;
static const uint GuideFieldBinCount = GuideFieldBinAxis * GuideFieldBinAxis;
static const uint GuideFieldStride = 2u + GuideFieldBinCount;
static const float GuideFieldFixedScale = 4096.0;
static const uint GuideFieldMaxCount = 0x0FFFFFFFu;
static const float GuideFieldBinSolidAngle = (4.0 * PI) / (float)GuideFieldBinCount;
static const float GuideFieldInvBinSolidAngle = (float)GuideFieldBinCount / (4.0 * PI);

float3 GuideFieldSquareToSphere(float2 p)
{
    float u = 2.0 * p.x - 1.0;
    float v = 2.0 * p.y - 1.0;
    float up = abs(u);
    float vp = abs(v);
    float signedDistance = 1.0 - (up + vp);
    float d = abs(signedDistance);
    float r = 1.0 - d;
    float phi = (r == 0.0 ? 1.0 : (vp - up) / r + 1.0) * (PI / 4.0);
    float z = (signedDistance >= 0.0 ? 1.0 : -1.0) * (1.0 - r * r);
    float cosPhi = (u >= 0.0 ? 1.0 : -1.0) * cos(phi);
    float sinPhi = (v >= 0.0 ? 1.0 : -1.0) * sin(phi);
    float rSqrt = r * sqrt(max(2.0 - r * r, 0.0));
    return float3(cosPhi * rSqrt, sinPhi * rSqrt, z);
}

float2 GuideFieldSphereToSquare(float3 dir)
{
    float x = abs(dir.x);
    float y = abs(dir.y);
    float z = abs(dir.z);
    float r = sqrt(max(1.0 - z, 0.0));
    float a = max(x, y);
    float b = min(x, y);
    b = a == 0.0 ? 0.0 : b / a;
    float phi = atan(b) * (2.0 / PI);
    if (x < y)
        phi = 1.0 - phi;
    float vv = phi * r;
    float uu = r - vv;
    if (dir.z < 0.0)
    {
        float tmp = uu;
        uu = 1.0 - vv;
        vv = 1.0 - tmp;
    }
    uu = (dir.x >= 0.0 ? 1.0 : -1.0) * uu;
    vv = (dir.y >= 0.0 ? 1.0 : -1.0) * vv;
    return float2(0.5 * (uu + 1.0), 0.5 * (vv + 1.0));
}

uint GuideFieldBinIndex(float3 dir)
{
    float2 sq = GuideFieldSphereToSquare(dir);
    uint ix = (uint)clamp((int)floor(sq.x * (float)GuideFieldBinAxis), 0, (int)GuideFieldBinAxis - 1);
    uint iy = (uint)clamp((int)floor(sq.y * (float)GuideFieldBinAxis), 0, (int)GuideFieldBinAxis - 1);
    return iy * GuideFieldBinAxis + ix;
}

float3 GuideFieldBinDirection(uint binIndex, float2 u)
{
    uint ix = binIndex % GuideFieldBinAxis;
    uint iy = binIndex / GuideFieldBinAxis;
    float2 sq = (float2(ix, iy) + u) / (float)GuideFieldBinAxis;
    return GuideFieldSquareToSphere(sq);
}

void GuideFieldDeposit(RWStructuredBuffer<uint> field, uint capacity, SpatialHashKey key, float3 dir, float weight)
{
    if (key.slot >= capacity || weight <= 0.0)
        return;

    uint base = key.slot * GuideFieldStride;
    uint previousChecksum;
    InterlockedCompareExchange(field[base + 0u], 0u, key.checksum, previousChecksum);
    if (previousChecksum != 0u && previousChecksum != key.checksum)
        return;

    InterlockedAdd(field[base + 1u], 1u);
    uint binIndex = GuideFieldBinIndex(dir);
    uint fixedWeight = (uint)(clamp(weight, 0.0, 65504.0) * GuideFieldFixedScale);
    InterlockedAdd(field[base + 2u + binIndex], fixedWeight);
}

bool GuideFieldRead(RWStructuredBuffer<uint> field, uint capacity, SpatialHashKey key, uint minSamples, out float bins[GuideFieldBinCount], out uint count)
{
    count = 0u;
    [unroll]
    for (uint b = 0u; b < GuideFieldBinCount; ++b)
        bins[b] = 0.0;

    if (key.slot >= capacity)
        return false;

    uint base = key.slot * GuideFieldStride;
    if (field[base + 0u] != key.checksum)
        return false;

    count = min(field[base + 1u], GuideFieldMaxCount);
    if (count < minSamples)
        return false;

    [unroll]
    for (uint i = 0u; i < GuideFieldBinCount; ++i)
        bins[i] = (float)field[base + 2u + i] / GuideFieldFixedScale;

    return true;
}

bool GuideFieldFetch(RWStructuredBuffer<uint> field, uint capacity, SpatialHashKey key, uint minSamples, out float bins[GuideFieldBinCount], out float total)
{
    total = 0.0;
    uint count;
    if (!GuideFieldRead(field, capacity, key, minSamples, bins, count))
        return false;

    [unroll]
    for (uint i = 0u; i < GuideFieldBinCount; ++i)
        total += bins[i];

    return total > 0.0;
}

void GuideFieldSampleFromBins(float bins[GuideFieldBinCount], float total, float u1, float2 u2, out float3 wi, out float pdf)
{
    wi = float3(0.0, 0.0, 1.0);
    pdf = 0.0;
    if (total <= 0.0)
        return;

    float target = u1 * total;
    float accum = 0.0;
    uint chosen = GuideFieldBinCount - 1u;
    bool found = false;
    [unroll]
    for (uint i = 0u; i < GuideFieldBinCount; ++i)
    {
        accum += bins[i];
        if (!found && target < accum)
        {
            chosen = i;
            found = true;
        }
    }

    wi = GuideFieldBinDirection(chosen, u2);
    pdf = (bins[chosen] / total) * GuideFieldInvBinSolidAngle;
}

float GuideFieldPdfFromBins(float bins[GuideFieldBinCount], float total, float3 wi)
{
    if (total <= 0.0)
        return 0.0;

    uint bin = GuideFieldBinIndex(wi);
    return (bins[bin] / total) * GuideFieldInvBinSolidAngle;
}

#endif
