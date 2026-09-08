#include "DirectionalColorKeyCS.hlsli"

cbuffer _ : register(b0)
{
    uint __x;
    uint __y;
    uint __z;
    int width;
    int height;
}

RWStructuredBuffer<float> source : register(u0);

RWStructuredBuffer<float> target : register(u1);

[numthreads(GROUP_X, GROUP_Y, GROUP_Z)]
void main(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    if (dispatchThreadId.x < __x && dispatchThreadId.y < __y && dispatchThreadId.z < __z)
    {
        int x = dispatchThreadId.x;
        int y = dispatchThreadId.y;
        if (x >= width || y >= height)
            return;
        int triple = (y * width + x) * 3;
        target[triple + 0] = source[triple + 0];
        target[triple + 1] = source[triple + 1];
        target[triple + 2] = source[triple + 2];
    }
}