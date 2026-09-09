#include "ComputeGroup.hlsli"

cbuffer Constants : register(b0)
{
    int angleBins;
    int radialBins;
    float logRadiusScale;
    int width;
    int height;
};

StructuredBuffer<int> labels : register(t0);

StructuredBuffer<float> centroids : register(t1);

RWStructuredBuffer<int> histogram : register(u0);

[numthreads(GROUP_X, GROUP_Y, GROUP_Z)]
void main(uint3 threadId : SV_DispatchThreadID)
{
    int x = (int)threadId.x;
    int y = (int)threadId.y;
    if (x >= width || y >= height)
        return;

    int label = labels[y * width + x];
    if (label < 0)
        return;

    float dx = x - centroids[label * 2 + 0];
    float dy = y - centroids[label * 2 + 1];
    float radius = sqrt(dx * dx + dy * dy);
    if (radius < 1.0f)
        return;

    float angle = atan2(dy, dx);
    float normalizedAngle = (angle + 3.14159265359f) / 6.28318530718f;

    int angleBin = (int)(normalizedAngle * angleBins);
    if (angleBin >= angleBins)
        angleBin = angleBins - 1;
    if (angleBin < 0)
        angleBin = 0;

    int radiusBin = (int)(log(radius) * logRadiusScale);
    if (radiusBin >= radialBins)
        radiusBin = radialBins - 1;
    if (radiusBin < 0)
        radiusBin = 0;

    int slot = label * (angleBins * radialBins) + angleBin * radialBins + radiusBin;
    InterlockedAdd(histogram[slot], 1);
}
