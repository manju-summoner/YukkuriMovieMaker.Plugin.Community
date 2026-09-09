#include "ComputeGroup.hlsli"

cbuffer Constants : register(b0)
{
    int invert;
    int width;
    int height;
};

StructuredBuffer<int> labels : register(t0);

StructuredBuffer<int> matchFlags : register(t1);

RWStructuredBuffer<int> mask : register(u0);

[numthreads(GROUP_X, GROUP_Y, GROUP_Z)]
void main(uint3 threadId : SV_DispatchThreadID)
{
    int x = (int)threadId.x;
    int y = (int)threadId.y;
    if (x >= width || y >= height)
        return;

    int index = y * width + x;
    int label = labels[index];

    int matched = 0;
    if (label >= 0 && matchFlags[label] != 0)
        matched = 1;

    if (invert != 0)
        matched = 1 - matched;

    mask[index] = matched != 0 ? -1 : 0;
}
