#include "DirectionalColorKeyCS.hlsli"

cbuffer Constants : register(b0)
{
    float backgroundL;
    float backgroundA;
    float backgroundB;
    float referencePerp;
    int width;
    int height;
}

StructuredBuffer<int> bgra : register(t0);

StructuredBuffer<float> colorLab : register(t1);

RWStructuredBuffer<int> foreground : register(u0);

RWStructuredBuffer<int> valid : register(u1);

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
        foreground[index] = 0;
        valid[index] = 0;
        return;
    }

    float bgLenSq = backgroundL * backgroundL + backgroundA * backgroundA + backgroundB * backgroundB;
    if (bgLenSq <= 1E-08 || referencePerp <= 1E-05)
    {
        foreground[index] = 0;
        valid[index] = 0;
        return;
    }

    float labL = colorLab[triple + 0];
    float labA = colorLab[triple + 1];
    float labB = colorLab[triple + 2];
    float along = (labL * backgroundL + labA * backgroundA + labB * backgroundB) / bgLenSq;
    float pl = labL - along * backgroundL;
    float pa = labA - along * backgroundA;
    float pb = labB - along * backgroundB;
    float perp = sqrt(pl * pl + pa * pa + pb * pb);
    if (perp < referencePerp)
    {
        foreground[index] = 0;
        valid[index] = 0;
        return;
    }

    float invA = 1.0 / a;
    float bSrgb = saturate(((packed >> 0) & 0xFF) * invA);
    float gSrgb = saturate(((packed >> 8) & 0xFF) * invA);
    float rSrgb = saturate(((packed >> 16) & 0xFF) * invA);
    int rByte = (int)(rSrgb * 255.0 + 0.5);
    int gByte = (int)(gSrgb * 255.0 + 0.5);
    int bByte = (int)(bSrgb * 255.0 + 0.5);
    foreground[index] = (0xFF << 24) | (rByte << 16) | (gByte << 8) | bByte;
    valid[index] = 1;
}