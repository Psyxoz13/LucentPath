#ifndef LUCENT_PATH_SECONDARY_GUIDE_EVENT_INCLUDED
#define LUCENT_PATH_SECONDARY_GUIDE_EVENT_INCLUDED

#define LUCENT_PATH_SECONDARY_GUIDE_RESERVED_SKY 1u
#define LUCENT_PATH_SECONDARY_GUIDE_RESERVED_MULTI_TRANSMISSION 2u
#define LUCENT_PATH_SECONDARY_GUIDE_RESERVED_PRIMARY_VIRTUAL 4u

struct GpuSecondaryGuideEvent
{
    float3 worldPosition;
    float hitDistance;
    float3 worldNormalOrDirection;
    uint valid;
    uint stableId;
    uint historyVersion;
    uint pathKind;
    uint flags;
    float roughness;
    float eta;
    uint mediumState;
    uint reserved;
    float3 previousWorldPosition;
    uint previousPositionValid;
};

GpuSecondaryGuideEvent CreateEmptySecondaryGuideEvent()
{
    GpuSecondaryGuideEvent eventData;
    eventData.worldPosition = 0.0;
    eventData.hitDistance = 0.0;
    eventData.worldNormalOrDirection = float3(0.0, 1.0, 0.0);
    eventData.valid = 0u;
    eventData.stableId = 0u;
    eventData.historyVersion = 0u;
    eventData.pathKind = 0u;
    eventData.flags = 0u;
    eventData.roughness = 1.0;
    eventData.eta = 1.0;
    eventData.mediumState = 0u;
    eventData.reserved = 0u;
    eventData.previousWorldPosition = 0.0;
    eventData.previousPositionValid = 0u;
    return eventData;
}

uint PackSecondaryGuideSmall(GpuSecondaryGuideEvent eventData)
{
    return (eventData.valid & 1u)
        | ((eventData.previousPositionValid & 1u) << 1)
        | ((eventData.pathKind & 7u) << 2)
        | ((eventData.mediumState & 15u) << 5)
        | ((eventData.reserved & 15u) << 9);
}

uint4 PackSecondaryGuideEvent0(GpuSecondaryGuideEvent eventData)
{
    return uint4(
        asuint(eventData.worldPosition.x),
        asuint(eventData.worldPosition.y),
        asuint(eventData.worldPosition.z),
        asuint(eventData.hitDistance));
}

uint4 PackSecondaryGuideEvent1(GpuSecondaryGuideEvent eventData)
{
    return uint4(
        asuint(eventData.worldNormalOrDirection.x),
        asuint(eventData.worldNormalOrDirection.y),
        asuint(eventData.worldNormalOrDirection.z),
        eventData.stableId);
}

uint4 PackSecondaryGuideEvent2(GpuSecondaryGuideEvent eventData)
{
    return uint4(
        asuint(eventData.previousWorldPosition.x),
        asuint(eventData.previousWorldPosition.y),
        asuint(eventData.previousWorldPosition.z),
        PackSecondaryGuideSmall(eventData));
}

uint4 PackSecondaryGuideEvent3(GpuSecondaryGuideEvent eventData)
{
    return uint4(
        asuint(eventData.roughness),
        asuint(eventData.eta),
        eventData.flags,
        0u);
}

GpuSecondaryGuideEvent UnpackSecondaryGuideEvent(uint4 event0, uint4 event1, uint4 event2, uint4 event3)
{
    GpuSecondaryGuideEvent eventData;
    eventData.worldPosition = float3(asfloat(event0.x), asfloat(event0.y), asfloat(event0.z));
    eventData.hitDistance = asfloat(event0.w);
    eventData.worldNormalOrDirection = float3(asfloat(event1.x), asfloat(event1.y), asfloat(event1.z));
    eventData.stableId = event1.w;

    eventData.previousWorldPosition = float3(asfloat(event2.x), asfloat(event2.y), asfloat(event2.z));
    uint small = event2.w;
    eventData.valid = small & 1u;
    eventData.previousPositionValid = (small >> 1) & 1u;
    eventData.pathKind = (small >> 2) & 7u;
    eventData.mediumState = (small >> 5) & 15u;
    eventData.reserved = (small >> 9) & 15u;

    eventData.roughness = asfloat(event3.x);
    eventData.eta = asfloat(event3.y);
    eventData.flags = event3.z;
    eventData.historyVersion = 0u;
    return eventData;
}

#endif
