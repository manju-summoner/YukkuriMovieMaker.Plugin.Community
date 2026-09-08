cbuffer Constants : register(b0)
{
    int featureSize;
    int componentCount;
};

StructuredBuffer<int> histogram : register(t0);

RWStructuredBuffer<float> features : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 threadId : SV_DispatchThreadID)
{
    int component = (int)threadId.x;
    if (component >= componentCount)
        return;

    int baseIndex = component * featureSize;

    float sumSq = 0.0f;
    for (int k = 0; k < featureSize; k++)
    {
        float v = (float)histogram[baseIndex + k];
        sumSq = sumSq + v * v;
    }

    float norm = sqrt(sumSq);
    float invNorm = norm > 1e-6f ? 1.0f / norm : 0.0f;

    for (int j = 0; j < featureSize; j++)
        features[baseIndex + j] = (float)histogram[baseIndex + j] * invNorm;
}
