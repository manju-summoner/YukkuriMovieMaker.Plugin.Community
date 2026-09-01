using ComputeWeave;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.FillSametype;

internal sealed class FillSametypePipeline : IDisposable
{
    readonly GraphicsDevice device;

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

    double[] moments = [];
    float[] centroids = [];
    int momentCapacity;

    public FillSametypePipeline()
    {
        device = GraphicsDevice.GetDefault();
    }

    public bool IsForeground(int index)
    {
        return LabelAt(index) >= 0;
    }

    internal int LabelAt(int index)
    {
        if ((uint)index >= (uint)pixelCount || gpuLabelBuffer is null)
            return -1;

        gpuLabelBuffer.CopyTo(labelProbe, index, 0, 1);

        return labelProbe[0];
    }

    public void InvalidateMatchCache()
    {
        lastSeedComponent = -1;
        lastSimilarityThreshold = float.NaN;
        lastMatchGeneration = -1;
        lastInvert = false;
    }

    public int AnalyzeShared(
        FillSametypeInteropHost interopHost,
        ComputeResourceBinding<ReadWriteTexture2D<Bgra32, Float4>> source,
        int width,
        int height)
    {
        EnsureCapacity(width, height);
        EnsureLabelResources();

        interopHost.ExtractForeground(source, gpuForegroundBuffer!, width, height).Wait();

        return AnalyzeFromLabels(LabelFromForeground(width, height), width, height);
    }

    public int Analyze(ReadOnlySpan<int> foreground, int width, int height)
    {
        EnsureCapacity(width, height);

        return AnalyzeFromLabels(LabelOnGpu(foreground, width, height), width, height);
    }

    int AnalyzeFromLabels(int count, int width, int height)
    {
        componentCount = count;
        if (componentCount == 0)
            return 0;

        EnsureMomentCapacity(componentCount);
        ComputeCentroidsOnGpu(width, height, componentCount);

        var labelGpu = gpuLabelBuffer!.AsReadOnly();
        var centroidGpu = EnsureCentroidBuffer(componentCount);
        var histogramGpu = EnsureHistogramBuffer(componentCount);
        var featureGpu = EnsureFeatureBuffer(componentCount);
        EnsureMatchFlagBuffer(componentCount);
        EnsureMaskBuffer(pixelCount);

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

    const int LabelBlockSize = LabelScanConstants.GroupSize;
    const int MaximumLabelPasses = 64;

    ReadWriteBuffer<int>? gpuForegroundBuffer;
    ReadWriteBuffer<int>? gpuParentBuffer;
    ReadWriteBuffer<int>? gpuRootIdBuffer;
    ReadWriteBuffer<int>? gpuLabelBuffer;
    ReadWriteBuffer<int>? gpuBlockCountBuffer;
    ReadWriteBuffer<int>? gpuBlockOffsetBuffer;
    ReadWriteBuffer<int>? gpuTotalBuffer;
    ReadWriteBuffer<int>? gpuChunkOffsetBuffer;
    ReadWriteBuffer<int>? gpuScalarBuffer;
    readonly int[] gpuScalar = new int[1];
    readonly int[] labelProbe = new int[1];

    static int CeilDivide(int value, int divisor) => (value + divisor - 1) / divisor;

    ReadWriteBuffer<int> EnsureExact(ref ReadWriteBuffer<int>? buffer, int count)
    {
        if (buffer is null || buffer.Length < count)
        {
            buffer?.Dispose();
            buffer = device.AllocateReadWriteBuffer<int>(count);
        }

        return buffer;
    }

    // 連結成分のラベル付けをGPUで行う。根は成分の最小添字とし、根を添字順に数えて
    // 番号を振るため、CPU実装の走査順の付番と一致する。
    ReadWriteBuffer<uint>? gpuMomentBuffer;
    uint[] momentReadback = [];

    void ComputeCentroidsOnGpu(int width, int height, int componentCount)
    {
        int length = componentCount * MomentConstants.Stride;

        if (gpuMomentBuffer is null || gpuMomentBuffer.Length < length)
        {
            gpuMomentBuffer?.Dispose();
            gpuMomentBuffer = device.AllocateReadWriteBuffer<uint>(length);
        }

        if (momentReadback.Length < length)
            momentReadback = new uint[length];

        device.For(length, new ClearMomentShader(gpuMomentBuffer, length));
        if (componentCount <= MomentConstants.LocalComponents)
            device.For(width, height, new MomentAccumulateLocalShader(gpuLabelBuffer!, gpuMomentBuffer, componentCount, width, height));
        else
            device.For(width, height, new MomentAccumulateShader(gpuLabelBuffer!, gpuMomentBuffer, componentCount, width, height));

        gpuMomentBuffer.CopyTo(momentReadback.AsSpan(0, length));

        for (int c = 0; c < componentCount; c++)
        {
            int slot = c * MomentConstants.Stride;

            double area = momentReadback[slot + MomentConstants.Area];
            double sumX = (momentReadback[slot + MomentConstants.SumXCarry] * 4294967296d) + momentReadback[slot + MomentConstants.SumXLow];
            double sumY = (momentReadback[slot + MomentConstants.SumYCarry] * 4294967296d) + momentReadback[slot + MomentConstants.SumYLow];

            int b = c * MomentStride;
            moments[b + 0] = area;
            moments[b + 1] = sumX;
            moments[b + 2] = sumY;

            centroids[c * 2 + 0] = (float)(sumX / area);
            centroids[c * 2 + 1] = (float)(sumY / area);
        }
    }

    int LabelOnGpu(ReadOnlySpan<int> foreground, int width, int height)
    {
        EnsureCapacity(width, height);
        EnsureLabelResources();

        gpuForegroundBuffer!.CopyFrom(foreground[..pixelCount]);

        return LabelFromForeground(width, height);
    }

    void EnsureLabelResources()
    {
        int blockCount = CeilDivide(pixelCount, LabelBlockSize);
        int chunkCount = CeilDivide(blockCount, LabelBlockSize);

        EnsureExact(ref gpuForegroundBuffer, pixelCount);
        EnsureExact(ref gpuParentBuffer, pixelCount);
        EnsureExact(ref gpuRootIdBuffer, pixelCount);
        EnsureExact(ref gpuLabelBuffer, pixelCount);
        EnsureExact(ref gpuBlockCountBuffer, blockCount);
        EnsureExact(ref gpuBlockOffsetBuffer, blockCount);
        EnsureExact(ref gpuTotalBuffer, chunkCount);
        EnsureExact(ref gpuChunkOffsetBuffer, chunkCount);
        EnsureExact(ref gpuScalarBuffer, 1);
    }

    int LabelFromForeground(int width, int height)
    {

        int blockCount = CeilDivide(pixelCount, LabelBlockSize);
        int chunkCount = CeilDivide(blockCount, LabelBlockSize);

        var foregroundGpu = gpuForegroundBuffer!;
        var parentGpu = gpuParentBuffer!;
        var rootIdGpu = gpuRootIdBuffer!;
        var labelGpu = gpuLabelBuffer!;
        var blockCountGpu = gpuBlockCountBuffer!;
        var blockOffsetGpu = gpuBlockOffsetBuffer!;
        var totalGpu = gpuTotalBuffer!;
        var chunkOffsetGpu = gpuChunkOffsetBuffer!;
        var scalarGpu = gpuScalarBuffer!;

        device.For(width, height, new LabelInitShader(foregroundGpu, parentGpu, width, height));

        for (int pass = 0; pass < MaximumLabelPasses; pass++)
        {
            gpuScalar[0] = 0;
            scalarGpu.CopyFrom(gpuScalar.AsSpan(0, 1));

            device.For(width, height, new LabelUnionShader(foregroundGpu, parentGpu, scalarGpu, width, height));
            device.For(width, height, new LabelCompressShader(foregroundGpu, parentGpu, width, height));

            scalarGpu.CopyTo(gpuScalar.AsSpan(0, 1));
            if (gpuScalar[0] == 0)
                break;
        }

        device.For(blockCount, new LabelBlockCountShader(parentGpu, blockCountGpu, pixelCount, blockCount));
        device.For(chunkCount, new LabelBlockScanShader(blockCountGpu, blockOffsetGpu, totalGpu, blockCount, chunkCount));
        device.For(1, new LabelChunkScanShader(totalGpu, chunkOffsetGpu, scalarGpu, chunkCount));
        device.For(blockCount, new LabelNumberShader(parentGpu, blockOffsetGpu, chunkOffsetGpu, rootIdGpu, pixelCount, blockCount));
        device.For(width, height, new LabelAssignShader(foregroundGpu, parentGpu, rootIdGpu, labelGpu, MaximumComponents, width, height));

        scalarGpu.CopyTo(gpuScalar.AsSpan(0, 1));

        return Math.Min(gpuScalar[0], MaximumComponents);
    }

    public bool GenerateMaskShared(
        FillSametypeInteropHost interopHost,
        ComputeResourceBinding<ReadWriteTexture2D<Bgra32, Float4>> destination,
        int seedIndex,
        float threshold,
        bool invert)
    {
        if (componentCount == 0
            || gpuLabelBuffer is null
            || featureBuffer is null
            || matchFlagBuffer is null)
        {
            interopHost.ClearMask(destination, width, height).Wait();
            return true;
        }

        int seedComponent = LabelAt(seedIndex);
        if (seedComponent < 0 || moments[seedComponent * MomentStride] < MinimumComponentArea)
        {
            interopHost.ClearMask(destination, width, height).Wait();
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

        interopHost.WriteMask(destination, gpuLabelBuffer.AsReadOnly(), matchFlagBuffer, invert ? 1 : 0, width, height).Wait();
        return true;
    }

    public bool GenerateMask(int seedIndex, float threshold, bool invert, Span<int> maskResult)
    {
        if (componentCount == 0
            || gpuLabelBuffer is null
            || featureBuffer is null
            || matchFlagBuffer is null
            || maskBuffer is null)
        {
            maskResult.Clear();
            return true;
        }

        int seedComponent = LabelAt(seedIndex);
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
            gpuLabelBuffer.AsReadOnly(), matchFlagBuffer, maskBuffer, invert ? 1 : 0, width, height));

        maskBuffer.CopyTo(maskResult);
        return true;
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
        if (this.width == width && this.height == height)
            return;

        this.width = width;
        this.height = height;
        pixelCount = width * height;

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

    void DisposeLabelBuffers()
    {
        gpuForegroundBuffer?.Dispose();
        gpuParentBuffer?.Dispose();
        gpuRootIdBuffer?.Dispose();
        gpuLabelBuffer?.Dispose();
        gpuBlockCountBuffer?.Dispose();
        gpuBlockOffsetBuffer?.Dispose();
        gpuTotalBuffer?.Dispose();
        gpuChunkOffsetBuffer?.Dispose();
        gpuScalarBuffer?.Dispose();
        gpuMomentBuffer?.Dispose();
        gpuForegroundBuffer = null;
        gpuParentBuffer = null;
        gpuRootIdBuffer = null;
        gpuLabelBuffer = null;
        gpuBlockCountBuffer = null;
        gpuBlockOffsetBuffer = null;
        gpuTotalBuffer = null;
        gpuChunkOffsetBuffer = null;
        gpuScalarBuffer = null;
        gpuMomentBuffer = null;
    }

    public void Dispose()
    {
        DisposeLabelBuffers();
        centroidBuffer?.Dispose();
        histogramBuffer?.Dispose();
        featureBuffer?.Dispose();
        matchFlagBuffer?.Dispose();
        maskBuffer?.Dispose();
        centroidBuffer = null;
        histogramBuffer = null;
        featureBuffer = null;
        matchFlagBuffer = null;
        maskBuffer = null;
    }
}
