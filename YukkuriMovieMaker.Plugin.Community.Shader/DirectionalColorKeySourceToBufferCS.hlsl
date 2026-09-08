#include "DirectionalColorKeyCS.hlsli"

cbuffer Constants : register(b0)
{
    int width;
    int height;
    int compare;
};

Texture2D<float4> source : register(t0);

RWStructuredBuffer<int> target : register(u0);

RWStructuredBuffer<int> changeCount : register(u1);

[numthreads(GROUP_X, GROUP_Y, GROUP_Z)]
void main(uint3 threadId : SV_DispatchThreadID)
{
    int x = (int)threadId.x;
    int y = (int)threadId.y;
    if (x >= width || y >= height)
        return;

    float4 color = source.Load(int3(x, y, 0));

    uint b = (uint)round(color.b * 255.0f);
    uint g = (uint)round(color.g * 255.0f);
    uint r = (uint)round(color.r * 255.0f);
    uint a = (uint)round(color.a * 255.0f);

    int packed = asint((a << 24) | (r << 16) | (g << 8) | b);
    int index = y * width + x;

    if (compare != 0 && target[index] != packed)
        InterlockedAdd(changeCount[0], 1);

    target[index] = packed;
}
