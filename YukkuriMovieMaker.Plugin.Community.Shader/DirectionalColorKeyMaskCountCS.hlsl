#include "DirectionalColorKeyCS.hlsli"

cbuffer Constants : register(b0)
{
    int width;
    int height;
}

StructuredBuffer<int> mask : register(t0);

RWStructuredBuffer<int> count : register(u0);

groupshared int partialCounts [GROUP_THREADS];

[numthreads(GROUP_X, GROUP_Y, GROUP_Z)]
void main(uint3 dispatchThreadId : SV_DispatchThreadID, uint groupIndex : SV_GroupIndex)
{
    int x = dispatchThreadId.x;
    int y = dispatchThreadId.y;
    int local = groupIndex;
    partialCounts[local] = (x < width && y < height && mask[y * width + x] != 0) ? 1 : 0;
    GroupMemoryBarrierWithGroupSync();
    if (local != 0)
        return;
    int total = 0;
    for (int i = 0; i < GROUP_THREADS; i++)
        total += partialCounts[i];
    if (total != 0)
        InterlockedAdd(count[0], total);
}
