#include "DirectionalColorKeyCS.hlsli"

cbuffer _ : register(b0)
{
    float backgroundL;
    float backgroundA;
    float backgroundB;
    float noiseThreshold;
    int width;
    int height;
}

StructuredBuffer<int> bgra : register(t0);

RWStructuredBuffer<float> colorLab : register(u0);

RWStructuredBuffer<float> directions : register(u1);

[numthreads(GROUP_X, GROUP_Y, GROUP_Z)]
void main(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    int x = dispatchThreadId.x;
    int y = dispatchThreadId.y;
    if (x >= width || y >= height)
        return;
    int index = y * width + x;
    int triple = index * 3;
    int packed = bgra[index];
    int a = (packed >> 24) & 0xFF;
    if (a == 0)
    {
        colorLab[triple + 0] = 0.0;
        colorLab[triple + 1] = 0.0;
        colorLab[triple + 2] = 0.0;
        directions[triple + 0] = 0.0;
        directions[triple + 1] = 0.0;
        directions[triple + 2] = 0.0;
        return;
    }

    float invA = 1.0 / a;
    float bSrgb = saturate(((packed >> 0) & 0xFF) * invA);
    float gSrgb = saturate(((packed >> 8) & 0xFF) * invA);
    float rSrgb = saturate(((packed >> 16) & 0xFF) * invA);
    float lr = rSrgb <= 0.04045 ? rSrgb / 12.92 : pow((rSrgb + 0.055) / 1.055, 2.4);
    float lg = gSrgb <= 0.04045 ? gSrgb / 12.92 : pow((gSrgb + 0.055) / 1.055, 2.4);
    float lb = bSrgb <= 0.04045 ? bSrgb / 12.92 : pow((bSrgb + 0.055) / 1.055, 2.4);
    float l = 0.41222146 * lr + 0.53633255 * lg + 0.051445995 * lb;
    float m = 0.2119035 * lr + 0.6806995 * lg + 0.10739696 * lb;
    float s = 0.08830246 * lr + 0.28171885 * lg + 0.6299787 * lb;
    float l_ = pow(l, 1.0 / 3.0);
    float m_ = pow(m, 1.0 / 3.0);
    float s_ = pow(s, 1.0 / 3.0);
    float labL = 0.21045426 * l_ + 0.7936178 * m_ - 0.004072047 * s_;
    float labA = 1.9779985 * l_ - 2.4285922 * m_ + 0.4505937 * s_;
    float labB = 0.025904037 * l_ + 0.78277177 * m_ - 0.80867577 * s_;
    colorLab[triple + 0] = labL;
    colorLab[triple + 1] = labA;
    colorLab[triple + 2] = labB;
    float dl = labL - backgroundL;
    float da = labA - backgroundA;
    float db = labB - backgroundB;
    float len = sqrt(dl * dl + da * da + db * db);
    if (len < noiseThreshold || len <= 1E-06)
    {
        directions[triple + 0] = 0.0;
        directions[triple + 1] = 0.0;
        directions[triple + 2] = 0.0;
        return;
    }

    float inv = 1.0 / len;
    directions[triple + 0] = dl * inv;
    directions[triple + 1] = da * inv;
    directions[triple + 2] = db * inv;
}