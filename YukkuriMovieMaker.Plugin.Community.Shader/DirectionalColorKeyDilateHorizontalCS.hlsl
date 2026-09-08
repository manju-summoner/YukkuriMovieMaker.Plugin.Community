#include "DirectionalColorKeyCS.hlsli"

cbuffer Constants : register(b0)
{
    int reach;
    int width;
    int height;
}

StructuredBuffer<int> source : register(t0);

RWStructuredBuffer<int> target : register(u0);

[numthreads(GROUP_X, GROUP_Y, GROUP_Z)]
void main(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    int x = dispatchThreadId.x;
    int y = dispatchThreadId.y;
    if (x >= width || y >= height)
        return;
    int row = y * width;
    int value = 0;
    for (int dx = -reach; dx <= reach; dx++)
    {
        int sx = x + dx;
        if (sx < 0 || sx >= width)
            continue;
        if (source[row + sx] != 0)
        {
            value = 1;
            break;
        }
    }

    target[row + x] = value;
}