#define __GroupSize__get_X 8
#define __GroupSize__get_Y 8
#define __GroupSize__get_Z 1

cbuffer _ : register(b0)
{
    uint __x;
    uint __y;
    uint __z;
    int reach;
    int width;
    int height;
}

RWStructuredBuffer<int> source : register(u0);

RWStructuredBuffer<int> target : register(u1);

[numthreads(__GroupSize__get_X, __GroupSize__get_Y, __GroupSize__get_Z)]
void Execute(uint3 ThreadIds : SV_DispatchThreadID)
{
    if (ThreadIds.x < __x && ThreadIds.y < __y && ThreadIds.z < __z)
    {
        int x = ThreadIds.x;
        int y = ThreadIds.y;
        if (x >= width || y >= height)
            return;
        int value = 0;
        for (int dy = -reach; dy <= reach; dy++)
        {
            int sy = y + dy;
            if (sy < 0 || sy >= height)
                continue;
            if (source[sy * width + x] != 0)
            {
                value = 1;
                break;
            }
        }

        target[y * width + x] = value;
    }
}