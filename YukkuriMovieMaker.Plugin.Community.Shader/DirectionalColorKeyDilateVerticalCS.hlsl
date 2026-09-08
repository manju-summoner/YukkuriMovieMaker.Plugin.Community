#include "DirectionalColorKeyCS.hlsli"

cbuffer _ : register(b0)
{
    int reach;
    int width;
    int height;
}

RWStructuredBuffer<int> source : register(u0);

RWStructuredBuffer<int> target : register(u1);

[numthreads(GROUP_X, GROUP_Y, GROUP_Z)]
void main(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    int x = dispatchThreadId.x;
    int y = dispatchThreadId.y;
    if (x >= width || y >= height)
        return;
    int value = 0;
    for (int dy = -reach; dy <= reach; dy++)
    {
        int sy = y + dy;
        if (sy < 0 || sy >= height)
            continue;
        if (source[sy * width + x] != 0)
        {
            value = 1;
            break;
        }
    }

    target[y * width + x] = value;
}