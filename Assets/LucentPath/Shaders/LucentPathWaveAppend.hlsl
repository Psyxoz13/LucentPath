#ifndef LUCENT_PATH_WAVE_APPEND_INCLUDED
#define LUCENT_PATH_WAVE_APPEND_INCLUDED

uint LucentWaveAppendSlot(RWStructuredBuffer<uint> counters, uint counterIndex, bool isAppending)
{
    uint appendCount = WaveActiveCountBits(isAppending);
    uint laneOffset = WavePrefixCountBits(isAppending);
    uint baseSlot = 0u;

    if (WaveIsFirstLane() && appendCount > 0u)
    {
        InterlockedAdd(counters[counterIndex], appendCount, baseSlot);
    }

    return WaveReadLaneFirst(baseSlot) + laneOffset;
}

#endif
