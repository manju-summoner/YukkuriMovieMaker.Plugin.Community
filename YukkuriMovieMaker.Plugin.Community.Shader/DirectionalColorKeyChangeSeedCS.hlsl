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

StructuredBuffer<int> bgra : register(t0);

RWStructuredBuffer<int> previousBgra : register(u0);

RWStructuredBuffer<int> seedMask : register(u1);

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
        seedMask[index] = bgra[index] != previousBgra[index] ? 1 : 0;
    }
}