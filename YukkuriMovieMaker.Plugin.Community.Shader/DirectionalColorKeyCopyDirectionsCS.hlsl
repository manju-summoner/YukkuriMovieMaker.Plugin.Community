#define __GroupSize__get_X 8
#define __GroupSize__get_Y 8
#define __GroupSize__get_Z 1

cbuffer _ : register(b0)
{
    uint __x;
    uint __y;
    uint __z;
    int width;
    int height;
}

RWStructuredBuffer<float> source : register(u0);

RWStructuredBuffer<float> target : register(u1);

[numthreads(__GroupSize__get_X, __GroupSize__get_Y, __GroupSize__get_Z)]
void Execute(uint3 ThreadIds : SV_DispatchThreadID)
{
    if (ThreadIds.x < __x && ThreadIds.y < __y && ThreadIds.z < __z)
    {
        int x = ThreadIds.x;
        int y = ThreadIds.y;
        if (x >= width || y >= height)
            return;
        int triple = (y * width + x) * 3;
        target[triple + 0] = source[triple + 0];
        target[triple + 1] = source[triple + 1];
        target[triple + 2] = source[triple + 2];
    }
}