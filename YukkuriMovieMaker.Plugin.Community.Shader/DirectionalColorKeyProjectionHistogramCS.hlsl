#define __GroupSize__get_X 8
#define __GroupSize__get_Y 8
#define __GroupSize__get_Z 1
#define __YukkuriMovieMaker_Plugin_Community_Effect_Video_DirectionalColorKey_DirectionFieldConstants__ValidLengthSquaredThreshold 0.25

cbuffer _ : register(b0)
{
    uint __x;
    uint __y;
    uint __z;
    float backgroundL;
    float backgroundA;
    float backgroundB;
    int clusterCount;
    int binsPerCluster;
    float projectionScale;
    int width;
    int height;
}

RWStructuredBuffer<float> colorLab : register(u0);

RWStructuredBuffer<float> directions : register(u1);

StructuredBuffer<float> centers : register(t0);

RWStructuredBuffer<int> histogram : register(u2);

[numthreads(__GroupSize__get_X, __GroupSize__get_Y, __GroupSize__get_Z)]
void main(uint3 ThreadIds : SV_DispatchThreadID)
{
    if (ThreadIds.x < __x && ThreadIds.y < __y && ThreadIds.z < __z)
    {
        int x = ThreadIds.x;
        int y = ThreadIds.y;
        if (x >= width || y >= height)
            return;
        int index = y * width + x;
        int triple = index * 3;
        float nl = directions[triple + 0];
        float na = directions[triple + 1];
        float nb = directions[triple + 2];
        if (nl * nl + na * na + nb * nb < __YukkuriMovieMaker_Plugin_Community_Effect_Video_DirectionalColorKey_DirectionFieldConstants__ValidLengthSquaredThreshold)
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

        int cBase2 = best * 3;
        float dl = colorLab[triple + 0] - backgroundL;
        float da = colorLab[triple + 1] - backgroundA;
        float db = colorLab[triple + 2] - backgroundB;
        float proj = dl * centers[cBase2 + 0] + da * centers[cBase2 + 1] + db * centers[cBase2 + 2];
        if (proj <= 0.0)
            return;
        int bin = (int)(proj * projectionScale);
        if (bin >= binsPerCluster)
            bin = binsPerCluster - 1;
        if (bin < 0)
            bin = 0;
        InterlockedAdd(histogram[best * binsPerCluster + bin], 1);
    }
}