#ifndef LUCENT_PATH_SAMPLING_INCLUDED
#define LUCENT_PATH_SAMPLING_INCLUDED

float3 SafeNormalize(float3 value, float3 fallback)
{
    float lengthSq = dot(value, value);
    float3 normalized = value * rsqrt(max(lengthSq, 1e-20));
    return lerp(fallback, normalized, step(1e-20, lengthSq));
}

float3 SafeNormalize(float3 value)
{
    return SafeNormalize(value, float3(0.0, 0.0, 1.0));
}

void BuildBasis(float3 n, out float3 t, out float3 b)
{
    float3 normal = SafeNormalize(n, float3(0.0, 1.0, 0.0));
    float s = normal.z >= 0.0 ? 1.0 : -1.0;
    float a = -1.0 / (s + normal.z);
    float bb = normal.x * normal.y * a;
    t = SafeNormalize(float3(1.0 + s * normal.x * normal.x * a, s * bb, -s * normal.x), float3(1.0, 0.0, 0.0));
    b = SafeNormalize(float3(bb, s + normal.y * normal.y * a, -normal.y), float3(0.0, 1.0, 0.0));
}

void OrthonormalizeFrame(float3 n, float3 tangentHint, float3 bitangentHint, out float3 t, out float3 b)
{
    t = float3(1.0, 0.0, 0.0);
    b = float3(0.0, 0.0, 1.0);

    float3 normal = SafeNormalize(n, float3(0.0, 1.0, 0.0));
    float3 tangent = tangentHint - normal * dot(normal, tangentHint);
    float tangentLengthSq = dot(tangent, tangent);
    if (tangentLengthSq <= 1e-12)
    {
        BuildBasis(normal, t, b);
        if (dot(b, bitangentHint) < 0.0)
            b = -b;
        return;
    }

    t = tangent * rsqrt(tangentLengthSq);
    float3 bitangent = bitangentHint - normal * dot(normal, bitangentHint) - t * dot(t, bitangentHint);
    float bitangentLengthSq = dot(bitangent, bitangent);
    if (bitangentLengthSq <= 1e-12)
        bitangent = cross(normal, t);
    else
        bitangent *= rsqrt(bitangentLengthSq);

    if (dot(cross(t, bitangent), normal) < 0.0)
        bitangent = -bitangent;

    b = SafeNormalize(bitangent, cross(normal, t));
}

float2 SampleUniformDiskConcentric(float2 u)
{
    float2 offset = 2.0 * u - 1.0;
    if (offset.x == 0.0 && offset.y == 0.0)
        return 0.0;

    float r;
    float theta;
    if (abs(offset.x) > abs(offset.y))
    {
        r = offset.x;
        theta = (PI * 0.25) * (offset.y / offset.x);
    }
    else
    {
        r = offset.y;
        theta = (PI * 0.5) - (PI * 0.25) * (offset.x / offset.y);
    }

    return r * float2(cos(theta), sin(theta));
}

float3 SampleCosineHemisphereLocal(float2 u)
{
    float2 d = SampleUniformDiskConcentric(u);
    float z = sqrt(saturate(1.0 - dot(d, d)));
    return float3(d.x, d.y, z);
}

float3 SampleCosineHemisphere(float3 normal, float2 u)
{
    float3 local = SampleCosineHemisphereLocal(u);
    float3 t;
    float3 b;
    BuildBasis(normal, t, b);
    return SafeNormalize(t * local.x + b * local.y + SafeNormalize(normal, float3(0.0, 1.0, 0.0)) * local.z, normal);
}

#endif
