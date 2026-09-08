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

RWStructuredBuffer<float> computedDirections : register(u0);

RWStructuredBuffer<float> previousDirections : register(u1);

RWStructuredBuffer<int> adoptMask : register(u2);

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
        if (adoptMask[index] != 0)
            return;
        computedDirections[triple + 0] = previousDirections[triple + 0];
        computedDirections[triple + 1] = previousDirections[triple + 1];
        computedDirections[triple + 2] = previousDirections[triple + 2];
    }
}