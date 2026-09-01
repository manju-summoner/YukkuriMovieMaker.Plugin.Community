using ComputeWeave;

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
    IReadOnlyBuffer<int> labels,
    ReadOnlyBuffer<float> centroids,
    ReadWriteBuffer<int> histogram,
    int angleBins,
    int radialBins,
    float logRadiusScale,
    int width,
    int height) : IComputeShader
{
    private readonly IReadOnlyBuffer<int> labels = labels;
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
    IReadOnlyBuffer<int> labels,
    ReadWriteBuffer<int> matchFlags,
    ReadWriteBuffer<int> mask,
    int invert,
    int width,
    int height) : IComputeShader
{
    private readonly IReadOnlyBuffer<int> labels = labels;
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

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct SharedMaskShader(
    IReadOnlyBuffer<int> labels,
    ReadWriteBuffer<int> matchFlags,
    IReadWriteNormalizedTexture2D<float4> destination,
    int invert,
    int width,
    int height) : IComputeShader
{
    private readonly IReadOnlyBuffer<int> labels = labels;
    private readonly ReadWriteBuffer<int> matchFlags = matchFlags;
    private readonly IReadWriteNormalizedTexture2D<float4> destination = destination;
    private readonly int invert = invert;
    private readonly int width = width;
    private readonly int height = height;

    public void Execute()
    {
        int x = ThreadIds.X;
        int y = ThreadIds.Y;
        if (x < width && y < height)
        {
            int label = labels[y * width + x];

            int matched = 0;
            if (label >= 0 && matchFlags[label] != 0)
                matched = 1;

            if (invert != 0)
                matched = 1 - matched;

            float value = matched != 0 ? 1f : 0f;

            destination[x, y] = new float4(value, value, value, value);
        }
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct ClearSharedMaskShader(
    IReadWriteNormalizedTexture2D<float4> destination,
    int width,
    int height) : IComputeShader
{
    private readonly IReadWriteNormalizedTexture2D<float4> destination = destination;
    private readonly int width = width;
    private readonly int height = height;

    public void Execute()
    {
        int x = ThreadIds.X;
        int y = ThreadIds.Y;
        if (x < width && y < height)
            destination[x, y] = new float4(0f, 0f, 0f, 0f);
    }
}

internal static class LabelScanConstants
{
    public const int GroupSize = 256;
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct ForegroundFromSharedShader(
    IReadWriteNormalizedTexture2D<float4> source,
    ReadWriteBuffer<int> foreground,
    int width,
    int height) : IComputeShader
{
    private readonly IReadWriteNormalizedTexture2D<float4> source = source;
    private readonly ReadWriteBuffer<int> foreground = foreground;
    private readonly int width = width;
    private readonly int height = height;

    public void Execute()
    {
        int x = ThreadIds.X;
        int y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        foreground[y * width + x] = source[x, y].W > 0f ? 1 : 0;
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct LabelInitShader(
    ReadWriteBuffer<int> foreground,
    ReadWriteBuffer<int> parent,
    int width,
    int height) : IComputeShader
{
    private readonly ReadWriteBuffer<int> foreground = foreground;
    private readonly ReadWriteBuffer<int> parent = parent;
    private readonly int width = width;
    private readonly int height = height;

    public void Execute()
    {
        int x = ThreadIds.X;
        int y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        int index = y * width + x;

        parent[index] = foreground[index] != 0 ? index : -1;
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct LabelUnionShader(
    ReadWriteBuffer<int> foreground,
    ReadWriteBuffer<int> parent,
    ReadWriteBuffer<int> changed,
    int width,
    int height) : IComputeShader
{
    private readonly ReadWriteBuffer<int> foreground = foreground;
    private readonly ReadWriteBuffer<int> parent = parent;
    private readonly ReadWriteBuffer<int> changed = changed;
    private readonly int width = width;
    private readonly int height = height;

    public void Execute()
    {
        int x = ThreadIds.X;
        int y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        int index = y * width + x;
        if (foreground[index] == 0)
            return;

        for (int neighbour = 0; neighbour < 4; neighbour++)
        {
            int offsetX = neighbour == 0 ? -1 : neighbour == 1 ? 0 : neighbour == 2 ? -1 : 1;
            int offsetY = neighbour == 0 ? 0 : -1;

            int sampleX = x + offsetX;
            int sampleY = y + offsetY;

            if (sampleX < 0 || sampleX >= width || sampleY < 0 || sampleY >= height)
                continue;

            int other = sampleY * width + sampleX;
            if (foreground[other] == 0)
                continue;

            int left = index;
            while (parent[left] != left)
                left = parent[left];

            int right = other;
            while (parent[right] != right)
                right = parent[right];

            if (left == right)
                continue;

            int low = Hlsl.Min(left, right);
            int high = Hlsl.Max(left, right);

            Hlsl.InterlockedMin(ref parent[high], low);
            changed[0] = 1;
        }
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct LabelCompressShader(
    ReadWriteBuffer<int> foreground,
    ReadWriteBuffer<int> parent,
    int width,
    int height) : IComputeShader
{
    private readonly ReadWriteBuffer<int> foreground = foreground;
    private readonly ReadWriteBuffer<int> parent = parent;
    private readonly int width = width;
    private readonly int height = height;

    public void Execute()
    {
        int x = ThreadIds.X;
        int y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        int index = y * width + x;
        if (foreground[index] == 0)
            return;

        int root = index;
        while (parent[root] != root)
            root = parent[root];

        parent[index] = root;
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.X)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct LabelBlockCountShader(
    ReadWriteBuffer<int> parent,
    ReadWriteBuffer<int> blockCounts,
    int pixelCount,
    int blockCount) : IComputeShader
{
    private readonly ReadWriteBuffer<int> parent = parent;
    private readonly ReadWriteBuffer<int> blockCounts = blockCounts;
    private readonly int pixelCount = pixelCount;
    private readonly int blockCount = blockCount;

    public void Execute()
    {
        int block = ThreadIds.X;
        if (block >= blockCount)
            return;

        int start = block * LabelScanConstants.GroupSize;
        int end = Hlsl.Min(start + LabelScanConstants.GroupSize, pixelCount);

        int roots = 0;
        for (int i = start; i < end; i++)
        {
            if (parent[i] == i)
                roots++;
        }

        blockCounts[block] = roots;
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.X)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct LabelBlockScanShader(
    ReadWriteBuffer<int> blockCounts,
    ReadWriteBuffer<int> blockOffsets,
    ReadWriteBuffer<int> totals,
    int blockCount,
    int chunkCount) : IComputeShader
{
    private readonly ReadWriteBuffer<int> blockCounts = blockCounts;
    private readonly ReadWriteBuffer<int> blockOffsets = blockOffsets;
    private readonly ReadWriteBuffer<int> totals = totals;
    private readonly int blockCount = blockCount;
    private readonly int chunkCount = chunkCount;

    public void Execute()
    {
        int chunk = ThreadIds.X;
        if (chunk >= chunkCount)
            return;

        int start = chunk * LabelScanConstants.GroupSize;
        int end = Hlsl.Min(start + LabelScanConstants.GroupSize, blockCount);

        int running = 0;
        for (int i = start; i < end; i++)
        {
            blockOffsets[i] = running;
            running += blockCounts[i];
        }

        totals[chunk] = running;
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.X)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct LabelChunkScanShader(
    ReadWriteBuffer<int> totals,
    ReadWriteBuffer<int> chunkOffsets,
    ReadWriteBuffer<int> componentCount,
    int chunkCount) : IComputeShader
{
    private readonly ReadWriteBuffer<int> totals = totals;
    private readonly ReadWriteBuffer<int> chunkOffsets = chunkOffsets;
    private readonly ReadWriteBuffer<int> componentCount = componentCount;
    private readonly int chunkCount = chunkCount;

    public void Execute()
    {
        if (ThreadIds.X != 0)
            return;

        int running = 0;
        for (int i = 0; i < chunkCount; i++)
        {
            chunkOffsets[i] = running;
            running += totals[i];
        }

        componentCount[0] = running;
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.X)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct LabelNumberShader(
    ReadWriteBuffer<int> parent,
    ReadWriteBuffer<int> blockOffsets,
    ReadWriteBuffer<int> chunkOffsets,
    ReadWriteBuffer<int> rootIds,
    int pixelCount,
    int blockCount) : IComputeShader
{
    private readonly ReadWriteBuffer<int> parent = parent;
    private readonly ReadWriteBuffer<int> blockOffsets = blockOffsets;
    private readonly ReadWriteBuffer<int> chunkOffsets = chunkOffsets;
    private readonly ReadWriteBuffer<int> rootIds = rootIds;
    private readonly int pixelCount = pixelCount;
    private readonly int blockCount = blockCount;

    public void Execute()
    {
        int block = ThreadIds.X;
        if (block >= blockCount)
            return;

        int start = block * LabelScanConstants.GroupSize;
        int end = Hlsl.Min(start + LabelScanConstants.GroupSize, pixelCount);

        int next = chunkOffsets[block / LabelScanConstants.GroupSize] + blockOffsets[block];

        for (int i = start; i < end; i++)
        {
            if (parent[i] == i)
            {
                rootIds[i] = next;
                next++;
            }
        }
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct LabelAssignShader(
    ReadWriteBuffer<int> foreground,
    ReadWriteBuffer<int> parent,
    ReadWriteBuffer<int> rootIds,
    ReadWriteBuffer<int> labels,
    int maximumComponents,
    int width,
    int height) : IComputeShader
{
    private readonly ReadWriteBuffer<int> foreground = foreground;
    private readonly ReadWriteBuffer<int> parent = parent;
    private readonly ReadWriteBuffer<int> rootIds = rootIds;
    private readonly ReadWriteBuffer<int> labels = labels;
    private readonly int maximumComponents = maximumComponents;
    private readonly int width = width;
    private readonly int height = height;

    public void Execute()
    {
        int x = ThreadIds.X;
        int y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        int index = y * width + x;

        if (foreground[index] == 0)
        {
            labels[index] = -1;
            return;
        }

        int identifier = rootIds[parent[index]];

        labels[index] = identifier < maximumComponents ? identifier : -1;
    }
}

internal static class MomentConstants
{
    public const int LocalComponents = 128;
    public const int LocalSlots = LocalComponents * Stride;
    public const int Stride = 5;
    public const int Area = 0;
    public const int SumXLow = 1;
    public const int SumXCarry = 2;
    public const int SumYLow = 3;
    public const int SumYCarry = 4;
}

[ThreadGroupSize(DefaultThreadGroupSizes.X)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct ClearMomentShader(
    ReadWriteBuffer<uint> moments,
    int length) : IComputeShader
{
    private readonly ReadWriteBuffer<uint> moments = moments;
    private readonly int length = length;

    public void Execute()
    {
        int i = ThreadIds.X;
        if (i >= length)
            return;

        moments[i] = 0u;
    }
}

// 座標和は4Kの全面が一成分だと32bitを越える。下位と桁上がりへ分けて累算し、
// 呼び出し側で64bitへ組み直すことで大きさによらず厳密に保つ。
[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct MomentAccumulateShader(
    ReadWriteBuffer<int> labels,
    ReadWriteBuffer<uint> moments,
    int componentCount,
    int width,
    int height) : IComputeShader
{
    private readonly ReadWriteBuffer<int> labels = labels;
    private readonly ReadWriteBuffer<uint> moments = moments;
    private readonly int componentCount = componentCount;
    private readonly int width = width;
    private readonly int height = height;

    public void Execute()
    {
        int x = ThreadIds.X;
        int y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        int component = labels[y * width + x];
        if (component < 0 || component >= componentCount)
            return;

        int slot = component * MomentConstants.Stride;

        Hlsl.InterlockedAdd(ref moments[slot + MomentConstants.Area], 1u);

        uint previousX;
        Hlsl.InterlockedAdd(ref moments[slot + MomentConstants.SumXLow], (uint)x, out previousX);
        if (previousX + (uint)x < previousX)
            Hlsl.InterlockedAdd(ref moments[slot + MomentConstants.SumXCarry], 1u);

        uint previousY;
        Hlsl.InterlockedAdd(ref moments[slot + MomentConstants.SumYLow], (uint)y, out previousY);
        if (previousY + (uint)y < previousY)
            Hlsl.InterlockedAdd(ref moments[slot + MomentConstants.SumYCarry], 1u);
    }
}

// 成分が少ないほど大域アトミックの宛先が集中する。グループ内で畳んでから
// 一度だけ大域へ撃つ。桁上がりは合流のたびに検出しなおす必要がある。
[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct MomentAccumulateLocalShader(
    ReadWriteBuffer<int> labels,
    ReadWriteBuffer<uint> moments,
    int componentCount,
    int width,
    int height) : IComputeShader
{
    private readonly ReadWriteBuffer<int> labels = labels;
    private readonly ReadWriteBuffer<uint> moments = moments;
    private readonly int componentCount = componentCount;
    private readonly int width = width;
    private readonly int height = height;

    [GroupShared(MomentConstants.LocalSlots)]
    private static readonly uint[] localMoments = null!;

    public void Execute()
    {
        int slots = componentCount * MomentConstants.Stride;

        for (int slot = GroupIds.Index; slot < slots; slot += GroupSize.Count)
            localMoments[slot] = 0u;

        Hlsl.GroupMemoryBarrierWithGroupSync();

        int x = ThreadIds.X;
        int y = ThreadIds.Y;

        if (x < width && y < height)
        {
            int component = labels[y * width + x];

            if (component >= 0 && component < componentCount)
            {
                int slot = component * MomentConstants.Stride;

                Hlsl.InterlockedAdd(ref localMoments[slot + MomentConstants.Area], 1u);

                uint previousX;
                Hlsl.InterlockedAdd(ref localMoments[slot + MomentConstants.SumXLow], (uint)x, out previousX);
                if (previousX + (uint)x < previousX)
                    Hlsl.InterlockedAdd(ref localMoments[slot + MomentConstants.SumXCarry], 1u);

                uint previousY;
                Hlsl.InterlockedAdd(ref localMoments[slot + MomentConstants.SumYLow], (uint)y, out previousY);
                if (previousY + (uint)y < previousY)
                    Hlsl.InterlockedAdd(ref localMoments[slot + MomentConstants.SumYCarry], 1u);
            }
        }

        Hlsl.GroupMemoryBarrierWithGroupSync();

        for (int component = GroupIds.Index; component < componentCount; component += GroupSize.Count)
        {
            int slot = component * MomentConstants.Stride;

            uint area = localMoments[slot + MomentConstants.Area];
            if (area != 0u)
                Hlsl.InterlockedAdd(ref moments[slot + MomentConstants.Area], area);

            uint sumXLow = localMoments[slot + MomentConstants.SumXLow];
            if (sumXLow != 0u)
            {
                uint previousX;
                Hlsl.InterlockedAdd(ref moments[slot + MomentConstants.SumXLow], sumXLow, out previousX);
                if (previousX + sumXLow < previousX)
                    Hlsl.InterlockedAdd(ref moments[slot + MomentConstants.SumXCarry], 1u);
            }

            uint sumXCarry = localMoments[slot + MomentConstants.SumXCarry];
            if (sumXCarry != 0u)
                Hlsl.InterlockedAdd(ref moments[slot + MomentConstants.SumXCarry], sumXCarry);

            uint sumYLow = localMoments[slot + MomentConstants.SumYLow];
            if (sumYLow != 0u)
            {
                uint previousY;
                Hlsl.InterlockedAdd(ref moments[slot + MomentConstants.SumYLow], sumYLow, out previousY);
                if (previousY + sumYLow < previousY)
                    Hlsl.InterlockedAdd(ref moments[slot + MomentConstants.SumYCarry], 1u);
            }

            uint sumYCarry = localMoments[slot + MomentConstants.SumYCarry];
            if (sumYCarry != 0u)
                Hlsl.InterlockedAdd(ref moments[slot + MomentConstants.SumYCarry], sumYCarry);
        }
    }
}
