#include "DirectionalColorKeyCS.hlsli"

cbuffer Constants : register(b0)
{
    int width;
    int height;
}

StructuredBuffer<float> source : register(t0);

RWStructuredBuffer<float> target : register(u0);

[numthreads(GROUP_X, GROUP_Y, GROUP_Z)]
void main(uint3 dispatchThreadId : SV_DispatchThreadID)
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