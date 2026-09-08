#include "ComputeGroup.hlsli"

cbuffer Constants : register(b0)
{
    int width;
    int height;
};

StructuredBuffer<int> source : register(t0);

RWTexture2D<float4> target : register(u0);

[numthreads(GROUP_X, GROUP_Y, GROUP_Z)]
void main(uint3 threadId : SV_DispatchThreadID)
{
    int x = (int)threadId.x;
    int y = (int)threadId.y;
    if (x >= width || y >= height)
        return;

    uint packed = asuint(source[y * width + x]);

    target[uint2(x, y)] = float4(
        ((packed >> 16) & 0xFF) / 255.0f,
        ((packed >> 8) & 0xFF) / 255.0f,
        (packed & 0xFF) / 255.0f,
        ((packed >> 24) & 0xFF) / 255.0f);
}
