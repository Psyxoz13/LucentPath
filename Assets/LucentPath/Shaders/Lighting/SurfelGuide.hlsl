#ifndef LUCENT_PATH_SURFEL_GUIDE_INCLUDED
#define LUCENT_PATH_SURFEL_GUIDE_INCLUDED

#ifndef LUCENT_PATH_SURFEL_STRUCT_DECLARED
#define LUCENT_PATH_SURFEL_STRUCT_DECLARED
struct GpuEmissiveSurfel
{
    float4 positionPower;
    float4 normal;
    float4 emission;
};
#endif

#ifndef LUCENT_PATH_SURFEL_DATA_RW
StructuredBuffer<GpuEmissiveSurfel> _EmissiveSurfels;
StructuredBuffer<uint> _EmissiveSurfelCounter;
int _EmissiveSurfelCapacity;
#endif

int _EmissiveSurfelNeeEnabled;
int _EmissiveSurfelNeeMaxCandidates;

static const float _SurfelGuideCosThetaMax = 0.98;

uint SurfelGuideCandidateCount()
{
    uint count = min(_EmissiveSurfelCounter[0], (uint)max(_EmissiveSurfelCapacity, 0));
    return min(count, (uint)max(_EmissiveSurfelNeeMaxCandidates, 0));
}

float SurfelGuideConePdf()
{
    return 1.0 / max(2.0 * PI * (1.0 - _SurfelGuideCosThetaMax), 1e-6);
}

float3 SampleSurfelCone(float3 axis, float2 u, out float pdf)
{
    float cosTheta = 1.0 - u.x * (1.0 - _SurfelGuideCosThetaMax);
    float sinTheta = sqrt(max(1.0 - cosTheta * cosTheta, 0.0));
    float phi = 2.0 * PI * u.y;
    float3 tangent;
    float3 bitangent;
    BuildBasis(axis, tangent, bitangent);
    pdf = SurfelGuideConePdf();
    return SafeNormalize(sinTheta * cos(phi) * tangent + sinTheta * sin(phi) * bitangent + cosTheta * axis, axis);
}

float SurfelGuideTotalPower(uint n)
{
    float total = 0.0;
    [loop]
    for (uint i = 0u; i < n; i++)
        total += max(_EmissiveSurfels[i].positionPower.w, 0.0);
    return total;
}

float EvaluateSurfelGuidePdf(float3 p, float3 dir)
{
    uint n = SurfelGuideCandidateCount();
    if (n == 0u)
        return 0.0;

    float total = SurfelGuideTotalPower(n);
    if (total <= 0.0)
        return 0.0;

    float conePdf = SurfelGuideConePdf();
    float pdf = 0.0;
    [loop]
    for (uint i = 0u; i < n; i++)
    {
        float power = max(_EmissiveSurfels[i].positionPower.w, 0.0);
        if (power <= 0.0)
            continue;
        float3 axis = SafeNormalize(_EmissiveSurfels[i].positionPower.xyz - p, dir);
        if (dot(dir, axis) >= _SurfelGuideCosThetaMax)
            pdf += (power / total) * conePdf;
    }
    return pdf;
}

bool SampleSurfelGuideDirection(float3 p, float uSelect, float2 uCone, out float3 dir, out float pdf)
{
    dir = float3(0.0, 1.0, 0.0);
    pdf = 0.0;

    uint n = SurfelGuideCandidateCount();
    if (n == 0u)
        return false;

    float total = SurfelGuideTotalPower(n);
    if (total <= 0.0)
        return false;

    float targetMass = saturate(uSelect) * total;
    float accumulated = 0.0;
    uint sel = n - 1u;
    [loop]
    for (uint i = 0u; i < n; i++)
    {
        accumulated += max(_EmissiveSurfels[i].positionPower.w, 0.0);
        if (accumulated >= targetMass)
        {
            sel = i;
            break;
        }
    }

    float3 axis = SafeNormalize(_EmissiveSurfels[sel].positionPower.xyz - p, float3(0.0, 1.0, 0.0));
    float conePdf;
    dir = SampleSurfelCone(axis, uCone, conePdf);
    pdf = EvaluateSurfelGuidePdf(p, dir);
    return pdf > 0.0;
}

float3 SurfelGuideEmissionForInstance(float3 p, uint instanceId)
{
    uint n = SurfelGuideCandidateCount();
    [loop]
    for (uint i = 0u; i < n; i++)
    {
        if (asuint(_EmissiveSurfels[i].normal.w) == instanceId)
            return _EmissiveSurfels[i].emission.rgb;
    }
    return float3(0.0, 0.0, 0.0);
}

#endif
