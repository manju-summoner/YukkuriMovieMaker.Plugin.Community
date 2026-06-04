using ComputeSharp;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.FillSametype;

internal sealed class FillSametypePipeline : IDisposable
{
    readonly GraphicsDevice device;

    ReadOnlyBuffer<int>? labelBuffer;
    ReadOnlyBuffer<float>? centroidBuffer;
    ReadWriteBuffer<int>? histogramBuffer;
    ReadWriteBuffer<float>? featureBuffer;
    ReadWriteBuffer<int>? matchFlagBuffer;
    ReadWriteBuffer<int>? maskBuffer;

    int width;
    int height;
    int pixelCount;
    int componentCount;
    int analysisGeneration;
    int lastSeedComponent = -1;
    float lastSimilarityThreshold = float.NaN;
    int lastMatchGeneration = -1;
    bool lastInvert;

    const int MinimumComponentArea = 16;
    const int MomentStride = 3;
    const int AngleBins = 36;
    const int RadialBins = 12;
    const int FeatureSize = AngleBins * RadialBins;
    const int MaximumComponents = 65536;

    int[] labels = [];
    int[] parent = [];
    int[] rank = [];
    int[] remap = [];
    double[] moments = [];
    float[] centroids = [];
    int momentCapacity;

    public FillSametypePipeline()
    {
        device = GraphicsDevice.GetDefault();
    }

    public bool IsForeground(int index)
    {
        return (uint)index < (uint)pixelCount && labels[index] >= 0;
    }

    public int Analyze(ReadOnlySpan<int> foreground, int width, int height)
    {
        EnsureCapacity(width, height);

        componentCount = Label(foreground, width, height);
        if (componentCount == 0)
            return 0;

        EnsureMomentCapacity(componentCount);
        ComputeCentroids(width, height, componentCount);

        var labelGpu = EnsureLabelBuffer(pixelCount);
        var centroidGpu = EnsureCentroidBuffer(componentCount);
        var histogramGpu = EnsureHistogramBuffer(componentCount);
        var featureGpu = EnsureFeatureBuffer(componentCount);
        EnsureMatchFlagBuffer(componentCount);
        EnsureMaskBuffer(pixelCount);

        labelGpu.CopyFrom(labels.AsSpan(0, pixelCount));
        centroidGpu.CopyFrom(centroids.AsSpan(0, componentCount * 2));

        int histogramLength = componentCount * FeatureSize;
        device.For(width, CeilDiv(histogramLength, width), new ClearBufferShader(histogramGpu, width, histogramLength));

        float maxRadius = (float)Math.Sqrt((double)width * width + (double)height * height);
        float logRadiusScale = RadialBins / (float)Math.Log(maxRadius);

        device.For(width, height, new LogPolarHistogramShader(
            labelGpu, centroidGpu, histogramGpu, AngleBins, RadialBins, logRadiusScale, width, height));

        device.For(componentCount, new NormalizeHistogramShader(histogramGpu, featureGpu, FeatureSize, componentCount));

        analysisGeneration++;

        return componentCount;
    }

    public bool GenerateMask(int seedIndex, float threshold, bool invert, Span<int> maskResult)
    {
        if (componentCount == 0
            || labelBuffer is null
            || featureBuffer is null
            || matchFlagBuffer is null
            || maskBuffer is null)
        {
            maskResult.Clear();
            return true;
        }

        int seedComponent = labels[seedIndex];
        if (seedComponent < 0 || moments[seedComponent * MomentStride] < MinimumComponentArea)
        {
            maskResult.Clear();
            return true;
        }

        float similarityThreshold = 1f - Math.Clamp(threshold / 100f, 0f, 1f);

        bool correlationChanged = seedComponent != lastSeedComponent
            || similarityThreshold != lastSimilarityThreshold
            || analysisGeneration != lastMatchGeneration;

        bool maskChanged = correlationChanged || invert != lastInvert;

        if (!maskChanged)
            return false;

        if (correlationChanged)
        {
            device.For(componentCount, new CorrelationMatchShader(
                featureBuffer, matchFlagBuffer, seedComponent, AngleBins, RadialBins, similarityThreshold, componentCount));

            lastSeedComponent = seedComponent;
            lastSimilarityThreshold = similarityThreshold;
            lastMatchGeneration = analysisGeneration;
        }

        lastInvert = invert;

        device.For(width, height, new MaskShader(
            labelBuffer, matchFlagBuffer, maskBuffer, invert ? 1 : 0, width, height));

        maskBuffer.CopyTo(maskResult);
        return true;
    }

    int Label(ReadOnlySpan<int> foreground, int width, int height)
    {
        Array.Fill(remap, -1, 0, pixelCount);

        for (int i = 0; i < pixelCount; i++)
        {
            parent[i] = i;
            rank[i] = 0;
        }

        for (int y = 0; y < height; y++)
        {
            int rowBase = y * width;
            for (int x = 0; x < width; x++)
            {
                int index = rowBase + x;
                if (foreground[index] == 0)
                    continue;

                if (x > 0 && foreground[index - 1] != 0)
                    Union(index, index - 1);
                if (y > 0 && foreground[index - width] != 0)
                    Union(index, index - width);
                if (y > 0 && x > 0 && foreground[index - width - 1] != 0)
                    Union(index, index - width - 1);
                if (y > 0 && x < width - 1 && foreground[index - width + 1] != 0)
                    Union(index, index - width + 1);
            }
        }

        int count = 0;
        for (int i = 0; i < pixelCount; i++)
        {
            if (foreground[i] == 0)
            {
                labels[i] = -1;
                continue;
            }

            int root = Find(i);
            if (remap[root] < 0)
            {
                if (count >= MaximumComponents)
                {
                    labels[i] = -1;
                    continue;
                }
                remap[root] = count++;
            }
            labels[i] = remap[root];
        }

        return count;
    }

    void Union(int a, int b)
    {
        int ra = Find(a);
        int rb = Find(b);
        if (ra == rb)
            return;
        if (rank[ra] < rank[rb])
            parent[ra] = rb;
        else if (rank[ra] > rank[rb])
            parent[rb] = ra;
        else
        {
            parent[rb] = ra;
            rank[ra]++;
        }
    }

    int Find(int x)
    {
        while (parent[x] != x)
        {
            parent[x] = parent[parent[x]];
            x = parent[x];
        }
        return x;
    }

    void ComputeCentroids(int width, int height, int componentCount)
    {
        Array.Clear(moments, 0, componentCount * MomentStride);

        var moment = moments;
        var label = labels;

        for (int y = 0; y < height; y++)
        {
            int rowBase = y * width;
            for (int x = 0; x < width; x++)
            {
                int c = label[rowBase + x];
                if (c < 0)
                    continue;

                int b = c * MomentStride;
                moment[b + 0] += 1;
                moment[b + 1] += x;
                moment[b + 2] += y;
            }
        }

        for (int c = 0; c < componentCount; c++)
        {
            int b = c * MomentStride;
            double m00 = moment[b + 0];
            centroids[c * 2 + 0] = (float)(moment[b + 1] / m00);
            centroids[c * 2 + 1] = (float)(moment[b + 2] / m00);
        }
    }

    void EnsureMomentCapacity(int count)
    {
        if (momentCapacity >= count)
            return;

        moments = new double[count * MomentStride];
        centroids = new float[count * 2];
        momentCapacity = count;
    }

    void EnsureCapacity(int width, int height)
    {
        if (this.width == width && this.height == height && labels.Length >= width * height)
            return;

        this.width = width;
        this.height = height;
        pixelCount = width * height;

        labels = new int[pixelCount];
        parent = new int[pixelCount];
        rank = new int[pixelCount];
        remap = new int[pixelCount];
    }

    ReadOnlyBuffer<int> EnsureLabelBuffer(int count)
    {
        if (labelBuffer is null || labelBuffer.Length < count)
        {
            labelBuffer?.Dispose();
            labelBuffer = device.AllocateReadOnlyBuffer<int>(count);
        }
        return labelBuffer;
    }

    ReadOnlyBuffer<float> EnsureCentroidBuffer(int componentCount)
    {
        int count = componentCount * 2;
        if (centroidBuffer is null || centroidBuffer.Length < count)
        {
            centroidBuffer?.Dispose();
            centroidBuffer = device.AllocateReadOnlyBuffer<float>(count);
        }
        return centroidBuffer;
    }

    ReadWriteBuffer<int> EnsureHistogramBuffer(int componentCount)
    {
        int count = componentCount * FeatureSize;
        if (histogramBuffer is null || histogramBuffer.Length < count)
        {
            histogramBuffer?.Dispose();
            histogramBuffer = device.AllocateReadWriteBuffer<int>(count);
        }
        return histogramBuffer;
    }

    ReadWriteBuffer<float> EnsureFeatureBuffer(int componentCount)
    {
        int count = componentCount * FeatureSize;
        if (featureBuffer is null || featureBuffer.Length < count)
        {
            featureBuffer?.Dispose();
            featureBuffer = device.AllocateReadWriteBuffer<float>(count);
        }
        return featureBuffer;
    }

    ReadWriteBuffer<int> EnsureMatchFlagBuffer(int count)
    {
        if (matchFlagBuffer is null || matchFlagBuffer.Length < count)
        {
            matchFlagBuffer?.Dispose();
            matchFlagBuffer = device.AllocateReadWriteBuffer<int>(count);
        }
        return matchFlagBuffer;
    }

    ReadWriteBuffer<int> EnsureMaskBuffer(int count)
    {
        if (maskBuffer is null || maskBuffer.Length < count)
        {
            maskBuffer?.Dispose();
            maskBuffer = device.AllocateReadWriteBuffer<int>(count);
        }
        return maskBuffer;
    }

    static int CeilDiv(int value, int divisor)
    {
        return (value + divisor - 1) / divisor;
    }

    public void Dispose()
    {
        labelBuffer?.Dispose();
        centroidBuffer?.Dispose();
        histogramBuffer?.Dispose();
        featureBuffer?.Dispose();
        matchFlagBuffer?.Dispose();
        maskBuffer?.Dispose();
        labelBuffer = null;
        centroidBuffer = null;
        histogramBuffer = null;
        featureBuffer = null;
        matchFlagBuffer = null;
        maskBuffer = null;
    }
}
