#ifndef LUCENT_PATH_HIT_PACK_INCLUDED
#define LUCENT_PATH_HIT_PACK_INCLUDED

#define LUCENT_HIT_IOR_REMAP_MAX 2.5

float2 _LucentSignNotZero(float2 v)
{
    return float2(v.x >= 0.0 ? 1.0 : -1.0, v.y >= 0.0 ? 1.0 : -1.0);
}

float2 _LucentOctEncode(float3 n)
{
    float invL1 = 1.0 / (abs(n.x) + abs(n.y) + abs(n.z) + 1e-20);
    float2 result = n.xy * invL1;
    if (n.z < 0.0)
        result = (1.0 - abs(result.yx)) * _LucentSignNotZero(result);
    return result * 0.5 + 0.5;
}

float3 _LucentOctDecode(float2 f)
{
    float2 e = f * 2.0 - 1.0;
    float3 n = float3(e.xy, 1.0 - abs(e.x) - abs(e.y));
    if (n.z < 0.0)
        n.xy = (1.0 - abs(n.yx)) * _LucentSignNotZero(n.xy);
    return normalize(n);
}

uint _LucentPackOct32(float3 n)
{
    float2 oct = _LucentOctEncode(normalize(n));
    uint x = uint(saturate(oct.x) * 65535.0 + 0.5);
    uint y = uint(saturate(oct.y) * 65535.0 + 0.5);
    return x | (y << 16);
}

float3 _LucentUnpackOct32(uint p)
{
    float2 oct = float2(
        float(p & 0xFFFFu) / 65535.0,
        float((p >> 16) & 0xFFFFu) / 65535.0);
    return _LucentOctDecode(oct);
}

uint _LucentPackHalf2(float a, float b)
{
    return f32tof16(a) | (f32tof16(b) << 16);
}

void _LucentUnpackHalf2(uint p, out float a, out float b)
{
    a = f16tof32(p & 0xFFFFu);
    b = f16tof32((p >> 16) & 0xFFFFu);
}

uint _LucentPackUnorm4x8(float4 v)
{
    uint4 q = uint4(saturate(v) * 255.0 + 0.5);
    return q.x | (q.y << 8) | (q.z << 16) | (q.w << 24);
}

float4 _LucentUnpackUnorm4x8(uint p)
{
    return float4(
        float(p & 0xFFu),
        float((p >> 8) & 0xFFu),
        float((p >> 16) & 0xFFu),
        float((p >> 24) & 0xFFu)) / 255.0;
}

uint _LucentPackUnorm3x8AndSign(float3 v, float signValue)
{
    uint3 q = uint3(saturate(v) * 255.0 + 0.5);
    uint signBit = signValue < 0.0 ? 0x80000000u : 0u;
    return q.x | (q.y << 8) | (q.z << 16) | signBit;
}

float3 _LucentUnpackUnorm3x8(uint p)
{
    return float3(
        float(p & 0xFFu),
        float((p >> 8) & 0xFFu),
        float((p >> 16) & 0xFFu)) / 255.0;
}

float _LucentUnpackSignFromBit31(uint p)
{
    return (p & 0x80000000u) != 0u ? -1.0 : 1.0;
}

uint _LucentPackAlphaTangentSign(float alpha, float tangentSign)
{
    uint alpha16 = uint(saturate(alpha) * 65535.0 + 0.5);
    uint signBit = tangentSign < 0.0 ? 0x80000000u : 0u;
    return (alpha16 & 0xFFFFu) | signBit;
}

void _LucentUnpackAlphaTangentSign(uint p, out float alpha, out float tangentSign)
{
    alpha = float(p & 0xFFFFu) / 65535.0;
    tangentSign = (p & 0x80000000u) != 0u ? -1.0 : 1.0;
}

uint _LucentPackR11G11B10f(float3 v)
{
    v = max(v, 0.0);
    uint r = (f32tof16(v.x) >> 4) & 0x7FFu;
    uint g = (f32tof16(v.y) >> 4) & 0x7FFu;
    uint b = (f32tof16(v.z) >> 5) & 0x3FFu;
    return r | (g << 11) | (b << 22);
}

float3 _LucentUnpackR11G11B10f(uint p)
{
    uint r = (p & 0x7FFu) << 4;
    uint g = ((p >> 11) & 0x7FFu) << 4;
    uint b = ((p >> 22) & 0x3FFu) << 5;
    return float3(f16tof32(r), f16tof32(g), f16tof32(b));
}

uint _LucentPackIor8(float ior)
{
    float norm = saturate((ior - 1.0) / (LUCENT_HIT_IOR_REMAP_MAX - 1.0));
    return uint(norm * 255.0 + 0.5);
}

float _LucentUnpackIor8(uint p)
{
    return 1.0 + (float(p & 0xFFu) / 255.0) * (LUCENT_HIT_IOR_REMAP_MAX - 1.0);
}

uint _LucentPackSignedUnorm8(float v)
{
    float norm = saturate(v * 0.5 + 0.5);
    return uint(norm * 255.0 + 0.5);
}

float _LucentUnpackSignedUnorm8(uint p)
{
    return float(p & 0xFFu) / 255.0 * 2.0 - 1.0;
}

uint _LucentPackUnorm8(float v)
{
    return uint(saturate(v) * 255.0 + 0.5);
}

float _LucentUnpackUnorm8(uint p)
{
    return float(p & 0xFFu) / 255.0;
}

uint _LucentPackBool8(bool v)
{
    return v ? 255u : 0u;
}

bool _LucentUnpackBool8(uint p)
{
    return (p & 0xFFu) > 127u;
}

uint _LucentMaterialCacheSlot(uint instanceId, uint geometryIndex, int capacity)
{
    if (capacity <= 0)
        return 0u;
    uint key = (instanceId * 2654435761u) ^ ((geometryIndex + 1u) * 2246822519u);
    return key % (uint)capacity;
}

uint _LucentPackFlagsMaterial(uint flags, uint materialModel)
{
    return (flags & 0xFFFFu) | ((materialModel & 0xFFu) << 16);
}

void _LucentUnpackFlagsMaterial(uint p, out uint flags, out uint materialModel)
{
    flags = p & 0xFFFFu;
    materialModel = (p >> 16) & 0xFFu;
}

#endif
