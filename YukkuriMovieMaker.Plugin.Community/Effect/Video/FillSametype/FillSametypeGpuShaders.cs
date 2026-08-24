using ComputeSharp;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.FillSametype;

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct ClearBufferShader(
    ReadWriteBuffer<int> buffer,
    int gridWidth,
    int length) : IComputeShader
{
    private readonly ReadWriteBuffer<int> buffer = buffer;
    private readonly int gridWidth = gridWidth;
    private readonly int length = length;

    public void Execute()
    {
        int index = ThreadIds.Y * gridWidth + ThreadIds.X;
        if (index < length)
            buffer[index] = 0;
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct LogPolarHistogramShader(
    ReadOnlyBuffer<int> labels,
    ReadOnlyBuffer<float> centroids,
    ReadWriteBuffer<int> histogram,
    int angleBins,
    int radialBins,
    float logRadiusScale,
    int width,
    int height) : IComputeShader
{
    private readonly ReadOnlyBuffer<int> labels = labels;
    private readonly ReadOnlyBuffer<float> centroids = centroids;
    private readonly ReadWriteBuffer<int> histogram = histogram;
    private readonly int angleBins = angleBins;
    private readonly int radialBins = radialBins;
    private readonly float logRadiusScale = logRadiusScale;
    private readonly int width = width;
    private readonly int height = height;

    public void Execute()
    {
        int x = ThreadIds.X;
        int y = ThreadIds.Y;
        if (x < width && y < height)
        {
            int index = y * width + x;
            int label = labels[index];
            if (label >= 0)
            {
                float cx = centroids[label * 2 + 0];
                float cy = centroids[label * 2 + 1];

                float dx = x - cx;
                float dy = y - cy;
                float radius = Hlsl.Sqrt(dx * dx + dy * dy);

                if (radius >= 1.0f)
                {
                    float angle = Hlsl.Atan2(dy, dx);
                    float twoPi = 6.28318530718f;
                    float normalizedAngle = (angle + 3.14159265359f) / twoPi;

                    int angleBin = (int)(normalizedAngle * angleBins);
                    if (angleBin >= angleBins)
                        angleBin = angleBins - 1;
                    if (angleBin < 0)
                        angleBin = 0;

                    int radiusBin = (int)(Hlsl.Log(radius) * logRadiusScale);
                    if (radiusBin >= radialBins)
                        radiusBin = radialBins - 1;
                    if (radiusBin < 0)
                        radiusBin = 0;

                    int featureSize = angleBins * radialBins;
                    int slot = label * featureSize + angleBin * radialBins + radiusBin;
                    Hlsl.InterlockedAdd(ref histogram[slot], 1);
                }
            }
        }
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.X)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct NormalizeHistogramShader(
    ReadWriteBuffer<int> histogram,
    ReadWriteBuffer<float> features,
    int featureSize,
    int componentCount) : IComputeShader
{
    private readonly ReadWriteBuffer<int> histogram = histogram;
    private readonly ReadWriteBuffer<float> features = features;
    private readonly int featureSize = featureSize;
    private readonly int componentCount = componentCount;

    public void Execute()
    {
        int component = ThreadIds.X;
        if (component < componentCount)
        {
            int baseIndex = component * featureSize;

            float sumSq = 0f;
            for (int k = 0; k < featureSize; k++)
            {
                float v = (float)histogram[baseIndex + k];
                sumSq = sumSq + v * v;
            }

            float norm = Hlsl.Sqrt(sumSq);
            float invNorm = norm > 1e-6f ? 1.0f / norm : 0f;

            for (int k = 0; k < featureSize; k++)
                features[baseIndex + k] = (float)histogram[baseIndex + k] * invNorm;
        }
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.X)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct CorrelationMatchShader(
    ReadWriteBuffer<float> features,
    ReadWriteBuffer<int> matchFlags,
    int seedComponent,
    int angleBins,
    int radialBins,
    float threshold,
    int componentCount) : IComputeShader
{
    private readonly ReadWriteBuffer<float> features = features;
    private readonly ReadWriteBuffer<int> matchFlags = matchFlags;
    private readonly int seedComponent = seedComponent;
    private readonly int angleBins = angleBins;
    private readonly int radialBins = radialBins;
    private readonly float threshold = threshold;
    private readonly int componentCount = componentCount;

    public void Execute()
    {
        int component = ThreadIds.X;
        if (component < componentCount)
        {
            int featureSize = angleBins * radialBins;
            int seedBase = seedComponent * featureSize;
            int candBase = component * featureSize;

            float best = 0f;

            for (int shift = 0; shift < angleBins; shift++)
            {
                float dot = 0f;

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
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct MaskShader(
    ReadOnlyBuffer<int> labels,
    ReadWriteBuffer<int> matchFlags,
    ReadWriteBuffer<int> mask,
    int invert,
    int width,
    int height) : IComputeShader
{
    private readonly ReadOnlyBuffer<int> labels = labels;
    private readonly ReadWriteBuffer<int> matchFlags = matchFlags;
    private readonly ReadWriteBuffer<int> mask = mask;
    private readonly int invert = invert;
    private readonly int width = width;
    private readonly int height = height;

    public void Execute()
    {
        int x = ThreadIds.X;
        int y = ThreadIds.Y;
        if (x < width && y < height)
        {
            int index = y * width + x;
            int label = labels[index];

            int matched = 0;
            if (label >= 0 && matchFlags[label] != 0)
                matched = 1;

            if (invert != 0)
                matched = 1 - matched;

            mask[index] = matched != 0 ? -1 : 0;
        }
    }
}
