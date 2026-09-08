#include "DirectionalColorKeyCS.hlsli"

cbuffer _ : register(b0)
{
    uint __x;
    uint __y;
    uint __z;
    int width;
    int height;
}

RWStructuredBuffer<int> mask : register(u0);

RWStructuredBuffer<int> count : register(u1);

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
