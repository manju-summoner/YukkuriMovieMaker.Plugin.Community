#include "DirectionalColorKeyCS.hlsli"

cbuffer Constants : register(b0)
{
    int width;
    int height;
}

StructuredBuffer<float> previousDirections : register(t0);

StructuredBuffer<int> adoptMask : register(t1);

RWStructuredBuffer<float> computedDirections : register(u0);

[numthreads(GROUP_X, GROUP_Y, GROUP_Z)]
void main(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    int x = dispatchThreadId.x;
    int y = dispatchThreadId.y;
    if (x >= width || y >= height)
        return;
    int index = y * width + x;
    int triple = index * 3;
    if (adoptMask[index] != 0)
        return;
    computedDirections[triple + 0] = previousDirections[triple + 0];
    computedDirections[triple + 1] = previousDirections[triple + 1];
    computedDirections[triple + 2] = previousDirections[triple + 2];
}