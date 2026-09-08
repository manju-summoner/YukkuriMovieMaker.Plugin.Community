#include "DirectionalColorKeyCS.hlsli"

cbuffer _ : register(b0)
{
    int width;
    int height;
}

StructuredBuffer<int> bgra : register(t0);

RWStructuredBuffer<int> previousBgra : register(u0);

RWStructuredBuffer<int> seedMask : register(u1);

[numthreads(GROUP_X, GROUP_Y, GROUP_Z)]
void main(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    int x = dispatchThreadId.x;
    int y = dispatchThreadId.y;
    if (x >= width || y >= height)
        return;
    int index = y * width + x;
    seedMask[index] = bgra[index] != previousBgra[index] ? 1 : 0;
}