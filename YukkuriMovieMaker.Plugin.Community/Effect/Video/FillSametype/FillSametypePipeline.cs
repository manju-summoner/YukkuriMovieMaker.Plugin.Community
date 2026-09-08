using System.Runtime.InteropServices;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Commons.Compute;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.FillSametype;

internal sealed class FillSametypePipeline : IDisposable
{
    readonly ComputeShaderDevice device;
    readonly ComputeConstantBuffer constants;

    ComputeBuffer<int>? labelBuffer;
    ComputeBuffer<float>? centroidBuffer;
    ComputeBuffer<int>? histogramBuffer;
    ComputeBuffer<float>? featureBuffer;
    ComputeBuffer<int>? matchFlagBuffer;
    ComputeBuffer<int>? maskBuffer;

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
    int[] maskReadback = [];
    int[] parent = [];
    int[] rank = [];
    int[] remap = [];
    double[] moments = [];
    float[] centroids = [];
    int momentCapacity;

    public FillSametypePipeline(IGraphicsDevicesAndContext devices)
    {
        device = new ComputeShaderDevice(devices);
        constants = new ComputeConstantBuffer(device, 32);
    }

    public bool IsSupported => device.IsSupported;

    public bool SupportsWritableSurface => device.SupportsWritableSurface;

    public ComputeSurface CreateSurface(ID2D1DeviceContext dc, int width, int height, bool writable)
        => new(device, dc, width, height, writable);

    public void ClearMask(ComputeSurface? target)
    {
        if (target is null)
        {
            Array.Clear(EnsureMaskReadback(), 0, pixelCount);
            return;
        }

        target.Clear();
    }

    public void CopyMaskTo(ID2D1Bitmap1 bitmap, int width)
        => bitmap.CopyFromMemory<int>(EnsureMaskReadback(), width * sizeof(int));

    // 面へ直接書ける環境では使わないため、必要になるまで確保しない。
    int[] EnsureMaskReadback()
    {
        if (maskReadback.Length < pixelCount)
            maskReadback = new int[pixelCount];

        return maskReadback;
    }

    public bool IsForeground(int index)
    {
        return (uint)index < (uint)pixelCount && labels[index] >= 0;
    }

    public void InvalidateMatchCache()
    {
        lastSeedComponent = -1;
        lastSimilarityThreshold = float.NaN;
        lastMatchGeneration = -1;
        lastInvert = false;
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

        labelGpu.Upload(labels.AsSpan(0, pixelCount));
        centroidGpu.Upload(centroids.AsSpan(0, componentCount * 2));

        float maxRadius = (float)Math.Sqrt((double)width * width + (double)height * height);
        float logRadiusScale = RadialBins / (float)Math.Log(maxRadius);

        using (device.Enter())
        {
            histogramGpu.Clear(componentCount * FeatureSize);

            constants.Update(new HistogramConstants(AngleBins, RadialBins, logRadiusScale, width, height));
            device.Dispatch("FillSametypeHistogramCS", constants.Buffer, ComputeShaderDevice.PixelGroups(width),
                ComputeShaderDevice.PixelGroups(height),
                labelGpu.Srv, centroidGpu.Srv, histogramGpu.Uav);

            constants.Update(new NormalizeConstants(FeatureSize, componentCount));
            device.Dispatch("FillSametypeNormalizeCS", constants.Buffer,
                ComputeShaderDevice.LinearGroups(componentCount), 1,
                histogramGpu.Srv, featureGpu.Uav);
        }

        analysisGeneration++;

        return componentCount;
    }

    public bool GenerateMask(int seedIndex, float threshold, bool invert, ComputeSurface? target)
    {
        if (componentCount == 0
            || labelBuffer is null
            || featureBuffer is null
            || matchFlagBuffer is null
            || maskBuffer is null)
        {
            ClearMask(target);
            return true;
        }

        int seedComponent = labels[seedIndex];
        if (seedComponent < 0 || moments[seedComponent * MomentStride] < MinimumComponentArea)
        {
            ClearMask(target);
            return true;
        }

        float similarityThreshold = 1f - Math.Clamp(threshold / 100f, 0f, 1f);

        bool correlationChanged = seedComponent != lastSeedComponent
            || similarityThreshold != lastSimilarityThreshold
            || analysisGeneration != lastMatchGeneration;

        bool maskChanged = correlationChanged || invert != lastInvert;

        if (!maskChanged)
            return false;

        using (device.Enter())
        {
            if (correlationChanged)
            {
                constants.Update(new CorrelationConstants(
                    seedComponent, AngleBins, RadialBins, similarityThreshold, componentCount));
                device.Dispatch("FillSametypeCorrelationCS", constants.Buffer,
                    ComputeShaderDevice.LinearGroups(componentCount), 1,
                    featureBuffer.Srv, matchFlagBuffer.Uav);

                lastSeedComponent = seedComponent;
                lastSimilarityThreshold = similarityThreshold;
                lastMatchGeneration = analysisGeneration;
            }

            lastInvert = invert;

            constants.Update(new MaskConstants(invert ? 1 : 0, width, height));
            device.Dispatch("FillSametypeMaskCS", constants.Buffer, ComputeShaderDevice.PixelGroups(width),
                ComputeShaderDevice.PixelGroups(height),
                labelBuffer.Srv, matchFlagBuffer.Srv, maskBuffer.Uav);

            if (target is not null)
            {
                constants.Update(new SurfaceConstants(width, height));
                device.Dispatch("PackedBufferToSurfaceCS", constants.Buffer, ComputeShaderDevice.PixelGroups(width),
                    ComputeShaderDevice.PixelGroups(height),
                    maskBuffer.Srv, target.Uav);
            }
            else
            {
                maskBuffer.Readback(EnsureMaskReadback().AsSpan(0, pixelCount));
            }
        }

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

    ComputeBuffer<int> EnsureLabelBuffer(int count)
    {
        if (labelBuffer is null || labelBuffer.Length < count)
        {
            labelBuffer?.Dispose();
            labelBuffer = new ComputeBuffer<int>(device, count, false);
        }
        return labelBuffer;
    }

    ComputeBuffer<float> EnsureCentroidBuffer(int componentCount)
    {
        int count = componentCount * 2;
        if (centroidBuffer is null || centroidBuffer.Length < count)
        {
            centroidBuffer?.Dispose();
            centroidBuffer = new ComputeBuffer<float>(device, count, false);
        }
        return centroidBuffer;
    }

    ComputeBuffer<int> EnsureHistogramBuffer(int componentCount)
    {
        int count = componentCount * FeatureSize;
        if (histogramBuffer is null || histogramBuffer.Length < count)
        {
            histogramBuffer?.Dispose();
            histogramBuffer = new ComputeBuffer<int>(device, count, true);
        }
        return histogramBuffer;
    }

    ComputeBuffer<float> EnsureFeatureBuffer(int componentCount)
    {
        int count = componentCount * FeatureSize;
        if (featureBuffer is null || featureBuffer.Length < count)
        {
            featureBuffer?.Dispose();
            featureBuffer = new ComputeBuffer<float>(device, count, true);
        }
        return featureBuffer;
    }

    ComputeBuffer<int> EnsureMatchFlagBuffer(int count)
    {
        if (matchFlagBuffer is null || matchFlagBuffer.Length < count)
        {
            matchFlagBuffer?.Dispose();
            matchFlagBuffer = new ComputeBuffer<int>(device, count, true);
        }
        return matchFlagBuffer;
    }

    ComputeBuffer<int> EnsureMaskBuffer(int count)
    {
        if (maskBuffer is null || maskBuffer.Length < count)
        {
            maskBuffer?.Dispose();
            maskBuffer = new ComputeBuffer<int>(device, count, true);
        }
        return maskBuffer;
    }


    public void Dispose()
    {
        constants.Dispose();
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
        device.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct SurfaceConstants(int Width, int Height);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct HistogramConstants(
        int AngleBins, int RadialBins, float LogRadiusScale, int Width, int Height);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct NormalizeConstants(int FeatureSize, int ComponentCount);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct CorrelationConstants(
        int SeedComponent, int AngleBins, int RadialBins, float Threshold, int ComponentCount);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct MaskConstants(int Invert, int Width, int Height);
}
