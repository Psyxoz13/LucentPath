#ifndef LUCENT_PATH_RESERVOIR_INCLUDED
#define LUCENT_PATH_RESERVOIR_INCLUDED

#include "LightTypes.hlsl"

struct LightReservoir
{
    LightSelector y;
    float wSum;
    float m;
    float w;
};

LightReservoir MakeEmptyLightReservoir()
{
    LightReservoir reservoir;
    reservoir.y = MakeEmptyLightSelector();
    reservoir.wSum = 0.0;
    reservoir.m = 0.0;
    reservoir.w = 0.0;
    return reservoir;
}

bool LightReservoirIsValid(LightReservoir reservoir)
{
    return reservoir.y.category != LIGHT_CATEGORY_NONE && reservoir.w > 0.0;
}

bool LightReservoirUpdate(inout LightReservoir reservoir, LightSelector candidate, float weight, float sampleCount, float u)
{
    reservoir.wSum += weight;
    reservoir.m += sampleCount;

    bool selected = weight > 0.0 && u * reservoir.wSum < weight;
    if (selected)
        reservoir.y = candidate;
    return selected;
}

void LightReservoirFinalize(inout LightReservoir reservoir, float targetPdf)
{
    float denom = reservoir.m * max(targetPdf, 1e-8);
    reservoir.w = denom > 0.0 ? reservoir.wSum / denom : 0.0;
}

bool LightReservoirMerge(inout LightReservoir reservoir, LightReservoir other, float targetPdfAtReceiver, float u)
{
    float weight = targetPdfAtReceiver * other.w * other.m;
    reservoir.wSum += weight;
    reservoir.m += other.m;

    bool selected = weight > 0.0 && u * reservoir.wSum < weight;
    if (selected)
        reservoir.y = other.y;
    return selected;
}

void LightReservoirClampHistory(inout LightReservoir reservoir, float maxConfidence)
{
    if (reservoir.m > maxConfidence)
    {
        float scale = maxConfidence / reservoir.m;
        reservoir.wSum *= scale;
        reservoir.m = maxConfidence;
    }
}

#endif
