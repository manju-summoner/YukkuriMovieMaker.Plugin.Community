#define __GroupSize__get_X 8
#define __GroupSize__get_Y 8
#define __GroupSize__get_Z 1

cbuffer _ : register(b0)
{
    uint __x;
    uint __y;
    uint __z;
    float backgroundL;
    float backgroundA;
    float backgroundB;
    float referencePerp;
    int width;
    int height;
}

StructuredBuffer<int> bgra : register(t0);

RWStructuredBuffer<float> colorLab : register(u0);

RWStructuredBuffer<int> foreground : register(u1);

RWStructuredBuffer<int> valid : register(u2);

[numthreads(__GroupSize__get_X, __GroupSize__get_Y, __GroupSize__get_Z)]
void Execute(uint3 ThreadIds : SV_DispatchThreadID)
{
    if (ThreadIds.x < __x && ThreadIds.y < __y && ThreadIds.z < __z)
    {
        int x = ThreadIds.x;
        int y = ThreadIds.y;
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
}