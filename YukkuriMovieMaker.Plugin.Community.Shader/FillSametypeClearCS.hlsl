cbuffer Constants : register(b0)
{
    int gridWidth;
    int bufferLength;
};

RWStructuredBuffer<int> target : register(u0);

[numthreads(8, 8, 1)]
void main(uint3 threadId : SV_DispatchThreadID)
{
    int index = (int)threadId.y * gridWidth + (int)threadId.x;
    if (index < bufferLength)
        target[index] = 0;
}
