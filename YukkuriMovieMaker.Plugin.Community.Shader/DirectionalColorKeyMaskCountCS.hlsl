#define __GroupSize__get_X 8
#define __GroupSize__get_Y 8
#define __GroupSize__get_Z 1

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

groupshared int partialCounts [64];

[numthreads(__GroupSize__get_X, __GroupSize__get_Y, __GroupSize__get_Z)]
void Execute(uint3 ThreadIds : SV_DispatchThreadID, uint __GroupIds__get_Index : SV_GroupIndex)
{
    int x = ThreadIds.x;
    int y = ThreadIds.y;
    int local = __GroupIds__get_Index;
    partialCounts[local] = (x < width && y < height && mask[y * width + x] != 0) ? 1 : 0;
    GroupMemoryBarrierWithGroupSync();
    if (local != 0)
        return;
    int total = 0;
    for (int i = 0; i < __GroupSize__get_X * __GroupSize__get_Y * __GroupSize__get_Z; i++)
        total += partialCounts[i];
    if (total != 0)
        InterlockedAdd(count[0], total);
}
