cbuffer Constants : register(b0)
{
    int seedComponent;
    int angleBins;
    int radialBins;
    float threshold;
    int componentCount;
};

StructuredBuffer<float> features : register(t0);

RWStructuredBuffer<int> matchFlags : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 threadId : SV_DispatchThreadID)
{
    int component = (int)threadId.x;
    if (component >= componentCount)
        return;

    int featureSize = angleBins * radialBins;
    int seedBase = seedComponent * featureSize;
    int candBase = component * featureSize;

    float best = 0.0f;

    for (int shift = 0; shift < angleBins; shift++)
    {
        float dot = 0.0f;

        for (int a = 0; a < angleBins; a++)
        {
            int rotated = a + shift;
            if (rotated >= angleBins)
                rotated = rotated - angleBins;

            int seedRow = seedBase + a * radialBins;
            int candRow = candBase + rotated * radialBins;

            for (int r = 0; r < radialBins; r++)
                dot = dot + features[seedRow + r] * features[candRow + r];
        }

        if (dot > best)
            best = dot;
    }

    matchFlags[component] = best >= threshold ? 1 : 0;
}
