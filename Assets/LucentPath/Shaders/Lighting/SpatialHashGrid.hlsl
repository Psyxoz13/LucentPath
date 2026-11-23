#ifndef LUCENT_PATH_SPATIAL_HASH_GRID_INCLUDED
#define LUCENT_PATH_SPATIAL_HASH_GRID_INCLUDED

uint SpatialHashPcg(uint value)
{
    uint state = value * 747796405u + 2891336453u;
    uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;
    return (word >> 22u) ^ word;
}

uint SpatialHashCell(uint3 cellBits, uint levelBits)
{
    uint hash = SpatialHashPcg(levelBits);
    hash = SpatialHashPcg(cellBits.x + hash);
    hash = SpatialHashPcg(cellBits.y + hash);
    hash = SpatialHashPcg(cellBits.z + hash);
    return hash;
}

int SpatialHashGridLevel(float3 worldPosition, float3 cameraPosition, float lodScale)
{
    float viewDistance = length(worldPosition - cameraPosition);
    return (int)max(floor(log2(max(viewDistance * lodScale, 1.0))), 0.0);
}

float SpatialHashCellSize(int level, float baseCellSize)
{
    return baseCellSize * exp2((float)level);
}

int3 SpatialHashCellCoord(float3 worldPosition, float cellSize)
{
    return (int3)floor(worldPosition / max(cellSize, 1e-6));
}

float3 SpatialHashJitter(uint pixelSeed, uint sampleIndex, uint depth)
{
    uint seed = pixelSeed ^ (sampleIndex * 0x9E3779B9u) ^ (depth * 0x68E31DA4u) ^ 0x7F6F2A8Du;
    uint hx = SpatialHashPcg(seed);
    uint hy = SpatialHashPcg(hx);
    uint hz = SpatialHashPcg(hy);
    float3 unitSample = float3(hx, hy, hz) * 2.3283064365386963e-10;
    return unitSample - 0.5;
}

struct SpatialHashKey
{
    uint slot;
    uint checksum;
};

SpatialHashKey SpatialHashComputeKeyJittered(
    float3 worldPosition,
    float3 cameraPosition,
    float baseCellSize,
    float lodScale,
    uint capacity,
    float3 jitter)
{
    int level = SpatialHashGridLevel(worldPosition, cameraPosition, lodScale);
    float cellSize = SpatialHashCellSize(level, baseCellSize);
    int3 cell = SpatialHashCellCoord(worldPosition + jitter * cellSize, cellSize);

    uint3 cellBits = uint3(asuint(cell.x), asuint(cell.y), asuint(cell.z));
    uint levelBits = (uint)level;

    uint primary = SpatialHashCell(cellBits, levelBits);
    uint secondary = SpatialHashPcg(primary ^ 0x9E3779B9u);

    SpatialHashKey result;
    result.slot = capacity > 0u ? (primary % capacity) : 0u;
    result.checksum = secondary == 0u ? 1u : secondary;
    return result;
}

SpatialHashKey SpatialHashComputeKey(
    float3 worldPosition,
    float3 cameraPosition,
    float baseCellSize,
    float lodScale,
    uint capacity)
{
    return SpatialHashComputeKeyJittered(worldPosition, cameraPosition, baseCellSize, lodScale, capacity, float3(0.0, 0.0, 0.0));
}

#endif
