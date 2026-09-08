#include "DirectionalColorKeyCS.hlsli"

cbuffer _ : register(b0)
{
    uint __x;
    uint __y;
    uint __z;
    int clusterCount;
    float fixedPointScale;
    int width;
    int height;
}

RWStructuredBuffer<float> directions : register(u0);

StructuredBuffer<float> centers : register(t0);

RWStructuredBuffer<int> accumulators : register(u1);

[numthreads(GROUP_X, GROUP_Y, GROUP_Z)]
void main(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    if (dispatchThreadId.x < __x && dispatchThreadId.y < __y && dispatchThreadId.z < __z)
    {
        int x = dispatchThreadId.x;
        int y = dispatchThreadId.y;
        if (x >= width || y >= height)
            return;
        int index = y * width + x;
        int triple = index * 3;
        float nl = directions[triple + 0];
        float na = directions[triple + 1];
        float nb = directions[triple + 2];
        if (nl * nl + na * na + nb * nb < VALID_LENGTH_SQUARED_THRESHOLD)
            return;
        int best = 0;
        float bestDot = -2.0;
        for (int c = 0; c < clusterCount; c++)
        {
            int cBase = c * 3;
            float __reserved__dot = nl * centers[cBase + 0] + na * centers[cBase + 1] + nb * centers[cBase + 2];
            if (__reserved__dot > bestDot)
            {
                bestDot = __reserved__dot;
                best = c;
            }
        }

        int sumBase = best * 3;
        InterlockedAdd(accumulators[sumBase + 0], (int)round(nl * fixedPointScale));
        InterlockedAdd(accumulators[sumBase + 1], (int)round(na * fixedPointScale));
        InterlockedAdd(accumulators[sumBase + 2], (int)round(nb * fixedPointScale));
        InterlockedAdd(accumulators[clusterCount * 3 + best], 1);
    }
}