using ComputeWeave;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.DirectionalColorKey;

internal static class DirectionSmoothConstants
{
    // DefaultThreadGroupSizes.XY が (8, 8, 1) に解決されることに対応する固定値。
    public const int GroupSize = 8;
    public const int Radius = 4;
    public const int TileSize = GroupSize + Radius * 2;
    public const int TileCount = TileSize * TileSize;
    public const int SpaceTableStride = Radius * 2 + 1;
    public const int SpaceTableCount = SpaceTableStride * SpaceTableStride;
}

internal static class DirectionFieldConstants
{
    // 方向ベクトルは長さ0の無効方向か長さ1の単位方向のいずれかを取る。
    // 長さの2乗がこの閾値未満なら無効方向とみなす。0と1の中間に置く。
    public const float ValidLengthSquaredThreshold = 0.25f;
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct DisplacementFieldShader(
    IReadOnlyBuffer<int> bgra,
    ReadWriteBuffer<float> colorLab,
    ReadWriteBuffer<float> directions,
    float backgroundL,
    float backgroundA,
    float backgroundB,
    float noiseThreshold,
    int width,
    int height) : IComputeShader
{
    private readonly IReadOnlyBuffer<int> bgra = bgra;
    private readonly ReadWriteBuffer<float> colorLab = colorLab;
    private readonly ReadWriteBuffer<float> directions = directions;
    private readonly float backgroundL = backgroundL;
    private readonly float backgroundA = backgroundA;
    private readonly float backgroundB = backgroundB;
    private readonly float noiseThreshold = noiseThreshold;
    private readonly int width = width;
    private readonly int height = height;

    public void Execute()
    {
        int x = ThreadIds.X;
        int y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        int index = y * width + x;
        int triple = index * 3;

        int packed = bgra[index];
        int a = (packed >> 24) & 0xFF;

        if (a == 0)
        {
            colorLab[triple + 0] = 0f;
            colorLab[triple + 1] = 0f;
            colorLab[triple + 2] = 0f;
            directions[triple + 0] = 0f;
            directions[triple + 1] = 0f;
            directions[triple + 2] = 0f;
            return;
        }

        float invA = 1f / a;
        float bSrgb = Hlsl.Saturate(((packed >> 0) & 0xFF) * invA);
        float gSrgb = Hlsl.Saturate(((packed >> 8) & 0xFF) * invA);
        float rSrgb = Hlsl.Saturate(((packed >> 16) & 0xFF) * invA);

        float lr = rSrgb <= 0.04045f ? rSrgb / 12.92f : Hlsl.Pow((rSrgb + 0.055f) / 1.055f, 2.4f);
        float lg = gSrgb <= 0.04045f ? gSrgb / 12.92f : Hlsl.Pow((gSrgb + 0.055f) / 1.055f, 2.4f);
        float lb = bSrgb <= 0.04045f ? bSrgb / 12.92f : Hlsl.Pow((bSrgb + 0.055f) / 1.055f, 2.4f);

        float l = 0.4122214708f * lr + 0.5363325363f * lg + 0.0514459929f * lb;
        float m = 0.2119034982f * lr + 0.6806995451f * lg + 0.1073969566f * lb;
        float s = 0.0883024619f * lr + 0.2817188376f * lg + 0.6299787005f * lb;

        float l_ = Hlsl.Pow(l, 1f / 3f);
        float m_ = Hlsl.Pow(m, 1f / 3f);
        float s_ = Hlsl.Pow(s, 1f / 3f);

        float labL = 0.2104542553f * l_ + 0.7936177850f * m_ - 0.0040720468f * s_;
        float labA = 1.9779984951f * l_ - 2.4285922050f * m_ + 0.4505937099f * s_;
        float labB = 0.0259040371f * l_ + 0.7827717662f * m_ - 0.8086757660f * s_;

        colorLab[triple + 0] = labL;
        colorLab[triple + 1] = labA;
        colorLab[triple + 2] = labB;

        float dl = labL - backgroundL;
        float da = labA - backgroundA;
        float db = labB - backgroundB;

        float len = Hlsl.Sqrt(dl * dl + da * da + db * db);

		if (len < noiseThreshold || len <= 1e-6f)
		{
            directions[triple + 0] = 0f;
            directions[triple + 1] = 0f;
            directions[triple + 2] = 0f;
            return;
        }

        float inv = 1f / len;
        directions[triple + 0] = dl * inv;
        directions[triple + 1] = da * inv;
        directions[triple + 2] = db * inv;
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct DirectionSmoothShader(
    ReadWriteBuffer<float> sourceDirections,
    ReadWriteBuffer<float> colorLab,
    ReadWriteBuffer<float> targetDirections,
    float sigmaColorSq,
    int width,
    int height) : IComputeShader
{
    private readonly ReadWriteBuffer<float> sourceDirections = sourceDirections;
    private readonly ReadWriteBuffer<float> colorLab = colorLab;
    private readonly ReadWriteBuffer<float> targetDirections = targetDirections;
    private readonly float sigmaColorSq = sigmaColorSq;
    private readonly int width = width;
    private readonly int height = height;

    [GroupShared(DirectionSmoothConstants.TileCount * 3)]
    private static readonly float[] directionTile = null!;
    [GroupShared(DirectionSmoothConstants.TileCount * 3)]
    private static readonly float[] colorTile = null!;
    [GroupShared(DirectionSmoothConstants.SpaceTableCount)]
    private static readonly float[] spaceTable = null!;

    public void Execute()
    {
        int x = ThreadIds.X;
        int y = ThreadIds.Y;

        float twoSigmaSpaceSq = 2f * DirectionSmoothConstants.Radius * DirectionSmoothConstants.Radius;

        for (int slot = GroupIds.Index; slot < DirectionSmoothConstants.SpaceTableCount; slot += GroupSize.Count)
        {
            int ty = slot / DirectionSmoothConstants.SpaceTableStride;
            int tx = slot - ty * DirectionSmoothConstants.SpaceTableStride;
            int offX = tx - DirectionSmoothConstants.Radius;
            int offY = ty - DirectionSmoothConstants.Radius;
            spaceTable[slot] = Hlsl.Exp(-(offX * offX + offY * offY) / Hlsl.Max(twoSigmaSpaceSq, 1e-6f));
        }

        int originX = x - GroupIds.X - DirectionSmoothConstants.Radius;
        int originY = y - GroupIds.Y - DirectionSmoothConstants.Radius;

        for (int slot = GroupIds.Index; slot < DirectionSmoothConstants.TileCount; slot += GroupSize.Count)
        {
            int localY = slot / DirectionSmoothConstants.TileSize;
            int localX = slot - localY * DirectionSmoothConstants.TileSize;
            int sampleX = originX + localX;
            int sampleY = originY + localY;
            int tileTriple = slot * 3;

            if (sampleX < 0 || sampleX >= width || sampleY < 0 || sampleY >= height)
            {
                directionTile[tileTriple + 0] = 0f;
                directionTile[tileTriple + 1] = 0f;
                directionTile[tileTriple + 2] = 0f;
                colorTile[tileTriple + 0] = 0f;
                colorTile[tileTriple + 1] = 0f;
                colorTile[tileTriple + 2] = 0f;
                continue;
            }

            int sampleTriple = (sampleY * width + sampleX) * 3;
            directionTile[tileTriple + 0] = sourceDirections[sampleTriple + 0];
            directionTile[tileTriple + 1] = sourceDirections[sampleTriple + 1];
            directionTile[tileTriple + 2] = sourceDirections[sampleTriple + 2];
            colorTile[tileTriple + 0] = colorLab[sampleTriple + 0];
            colorTile[tileTriple + 1] = colorLab[sampleTriple + 1];
            colorTile[tileTriple + 2] = colorLab[sampleTriple + 2];
        }

        Hlsl.GroupMemoryBarrierWithGroupSync();

        if (x >= width || y >= height)
            return;

        int centerLocalX = GroupIds.X + DirectionSmoothConstants.Radius;
        int centerLocalY = GroupIds.Y + DirectionSmoothConstants.Radius;
        int centerTile = (centerLocalY * DirectionSmoothConstants.TileSize + centerLocalX) * 3;

        int triple = (y * width + x) * 3;

        float nl = directionTile[centerTile + 0];
        float na = directionTile[centerTile + 1];
        float nb = directionTile[centerTile + 2];

        if (nl * nl + na * na + nb * nb < DirectionFieldConstants.ValidLengthSquaredThreshold)
        {
            targetDirections[triple + 0] = 0f;
            targetDirections[triple + 1] = 0f;
            targetDirections[triple + 2] = 0f;
            return;
        }

        float cl = colorTile[centerTile + 0];
        float ca = colorTile[centerTile + 1];
        float cb = colorTile[centerTile + 2];

        float sumL = 0f;
        float sumA = 0f;
        float sumB = 0f;
        float sumW = 0f;

        for (int dy = -DirectionSmoothConstants.Radius; dy <= DirectionSmoothConstants.Radius; dy++)
        {
            int sy = y + dy;
            if (sy < 0 || sy >= height)
                continue;

            for (int dx = -DirectionSmoothConstants.Radius; dx <= DirectionSmoothConstants.Radius; dx++)
            {
                int sx = x + dx;
                if (sx < 0 || sx >= width)
                    continue;

                int sTile = ((centerLocalY + dy) * DirectionSmoothConstants.TileSize + (centerLocalX + dx)) * 3;

                float ml = directionTile[sTile + 0];
                float ma = directionTile[sTile + 1];
                float mb = directionTile[sTile + 2];

                if (ml * ml + ma * ma + mb * mb < DirectionFieldConstants.ValidLengthSquaredThreshold)
                    continue;

                float dot = nl * ml + na * ma + nb * mb;
                if (dot <= 0f)
                    continue;

                float wSpace = spaceTable[(dy + DirectionSmoothConstants.Radius) * DirectionSmoothConstants.SpaceTableStride + (dx + DirectionSmoothConstants.Radius)];

                float dcl = cl - colorTile[sTile + 0];
                float dca = ca - colorTile[sTile + 1];
                float dcb = cb - colorTile[sTile + 2];
                float colorDistSq = dcl * dcl + dca * dca + dcb * dcb;
                float wColor = Hlsl.Exp(-colorDistSq / Hlsl.Max(sigmaColorSq, 1e-6f));

                float w = wSpace * wColor * dot;

                sumL += ml * w;
                sumA += ma * w;
                sumB += mb * w;
                sumW += w;
            }
        }

        if (sumW > 1e-6f)
        {
            float avgL = sumL / sumW;
            float avgA = sumA / sumW;
            float avgB = sumB / sumW;
            float norm = Hlsl.Sqrt(avgL * avgL + avgA * avgA + avgB * avgB);
            if (norm > 1e-6f)
            {
                float inv = 1f / norm;
                targetDirections[triple + 0] = avgL * inv;
                targetDirections[triple + 1] = avgA * inv;
                targetDirections[triple + 2] = avgB * inv;
                return;
            }
        }

        targetDirections[triple + 0] = nl;
        targetDirections[triple + 1] = na;
        targetDirections[triple + 2] = nb;
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct ChangeSeedShader(
    IReadOnlyBuffer<int> bgra,
    ReadWriteBuffer<int> previousBgra,
    ReadWriteBuffer<int> seedMask,
    int width,
    int height) : IComputeShader
{
    private readonly IReadOnlyBuffer<int> bgra = bgra;
    private readonly ReadWriteBuffer<int> previousBgra = previousBgra;
    private readonly ReadWriteBuffer<int> seedMask = seedMask;
    private readonly int width = width;
    private readonly int height = height;

    public void Execute()
    {
        int x = ThreadIds.X;
        int y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        int index = y * width + x;
        seedMask[index] = bgra[index] != previousBgra[index] ? 1 : 0;
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct DilateHorizontalShader(
    ReadWriteBuffer<int> source,
    ReadWriteBuffer<int> target,
    int reach,
    int width,
    int height) : IComputeShader
{
    private readonly ReadWriteBuffer<int> source = source;
    private readonly ReadWriteBuffer<int> target = target;
    private readonly int reach = reach;
    private readonly int width = width;
    private readonly int height = height;

    public void Execute()
    {
        int x = ThreadIds.X;
        int y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        int row = y * width;
        int value = 0;
        for (int dx = -reach; dx <= reach; dx++)
        {
            int sx = x + dx;
            if (sx < 0 || sx >= width)
                continue;
            if (source[row + sx] != 0)
            {
                value = 1;
                break;
            }
        }
        target[row + x] = value;
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct DilateVerticalShader(
    ReadWriteBuffer<int> source,
    ReadWriteBuffer<int> target,
    int reach,
    int width,
    int height) : IComputeShader
{
    private readonly ReadWriteBuffer<int> source = source;
    private readonly ReadWriteBuffer<int> target = target;
    private readonly int reach = reach;
    private readonly int width = width;
    private readonly int height = height;

    public void Execute()
    {
        int x = ThreadIds.X;
        int y = ThreadIds.Y;
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

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct RegionDirectionSmoothShader(
    ReadWriteBuffer<float> sourceDirections,
    ReadWriteBuffer<float> colorLab,
    ReadWriteBuffer<float> targetDirections,
    ReadWriteBuffer<int> computeMask,
    float sigmaColorSq,
    int width,
    int height) : IComputeShader
{
    private readonly ReadWriteBuffer<float> sourceDirections = sourceDirections;
    private readonly ReadWriteBuffer<float> colorLab = colorLab;
    private readonly ReadWriteBuffer<float> targetDirections = targetDirections;
    private readonly ReadWriteBuffer<int> computeMask = computeMask;
    private readonly float sigmaColorSq = sigmaColorSq;
    private readonly int width = width;
    private readonly int height = height;

    [GroupShared(DirectionSmoothConstants.TileCount * 3)]
    private static readonly float[] directionTile = null!;
    [GroupShared(DirectionSmoothConstants.TileCount * 3)]
    private static readonly float[] colorTile = null!;
    [GroupShared(DirectionSmoothConstants.SpaceTableCount)]
    private static readonly float[] spaceTable = null!;

    public void Execute()
    {
        int x = ThreadIds.X;
        int y = ThreadIds.Y;

        float twoSigmaSpaceSq = 2f * DirectionSmoothConstants.Radius * DirectionSmoothConstants.Radius;

        for (int slot = GroupIds.Index; slot < DirectionSmoothConstants.SpaceTableCount; slot += GroupSize.Count)
        {
            int ty = slot / DirectionSmoothConstants.SpaceTableStride;
            int tx = slot - ty * DirectionSmoothConstants.SpaceTableStride;
            int offX = tx - DirectionSmoothConstants.Radius;
            int offY = ty - DirectionSmoothConstants.Radius;
            spaceTable[slot] = Hlsl.Exp(-(offX * offX + offY * offY) / Hlsl.Max(twoSigmaSpaceSq, 1e-6f));
        }

        int originX = x - GroupIds.X - DirectionSmoothConstants.Radius;
        int originY = y - GroupIds.Y - DirectionSmoothConstants.Radius;

        for (int slot = GroupIds.Index; slot < DirectionSmoothConstants.TileCount; slot += GroupSize.Count)
        {
            int localY = slot / DirectionSmoothConstants.TileSize;
            int localX = slot - localY * DirectionSmoothConstants.TileSize;
            int sampleX = originX + localX;
            int sampleY = originY + localY;
            int tileTriple = slot * 3;

            if (sampleX < 0 || sampleX >= width || sampleY < 0 || sampleY >= height)
            {
                directionTile[tileTriple + 0] = 0f;
                directionTile[tileTriple + 1] = 0f;
                directionTile[tileTriple + 2] = 0f;
                colorTile[tileTriple + 0] = 0f;
                colorTile[tileTriple + 1] = 0f;
                colorTile[tileTriple + 2] = 0f;
                continue;
            }

            int sampleTriple = (sampleY * width + sampleX) * 3;
            directionTile[tileTriple + 0] = sourceDirections[sampleTriple + 0];
            directionTile[tileTriple + 1] = sourceDirections[sampleTriple + 1];
            directionTile[tileTriple + 2] = sourceDirections[sampleTriple + 2];
            colorTile[tileTriple + 0] = colorLab[sampleTriple + 0];
            colorTile[tileTriple + 1] = colorLab[sampleTriple + 1];
            colorTile[tileTriple + 2] = colorLab[sampleTriple + 2];
        }

        Hlsl.GroupMemoryBarrierWithGroupSync();

        if (x >= width || y >= height)
            return;

        int index = y * width + x;
        int triple = index * 3;

        int centerLocalX = GroupIds.X + DirectionSmoothConstants.Radius;
        int centerLocalY = GroupIds.Y + DirectionSmoothConstants.Radius;
        int centerTile = (centerLocalY * DirectionSmoothConstants.TileSize + centerLocalX) * 3;

        if (computeMask[index] == 0)
        {
            targetDirections[triple + 0] = directionTile[centerTile + 0];
            targetDirections[triple + 1] = directionTile[centerTile + 1];
            targetDirections[triple + 2] = directionTile[centerTile + 2];
            return;
        }

        float nl = directionTile[centerTile + 0];
        float na = directionTile[centerTile + 1];
        float nb = directionTile[centerTile + 2];

        if (nl * nl + na * na + nb * nb < DirectionFieldConstants.ValidLengthSquaredThreshold)
        {
            targetDirections[triple + 0] = 0f;
            targetDirections[triple + 1] = 0f;
            targetDirections[triple + 2] = 0f;
            return;
        }

        float cl = colorTile[centerTile + 0];
        float ca = colorTile[centerTile + 1];
        float cb = colorTile[centerTile + 2];

        float sumL = 0f;
        float sumA = 0f;
        float sumB = 0f;
        float sumW = 0f;

        for (int dy = -DirectionSmoothConstants.Radius; dy <= DirectionSmoothConstants.Radius; dy++)
        {
            int sy = y + dy;
            if (sy < 0 || sy >= height)
                continue;

            for (int dx = -DirectionSmoothConstants.Radius; dx <= DirectionSmoothConstants.Radius; dx++)
            {
                int sx = x + dx;
                if (sx < 0 || sx >= width)
                    continue;

                int sTile = ((centerLocalY + dy) * DirectionSmoothConstants.TileSize + (centerLocalX + dx)) * 3;

                float ml = directionTile[sTile + 0];
                float ma = directionTile[sTile + 1];
                float mb = directionTile[sTile + 2];

                if (ml * ml + ma * ma + mb * mb < DirectionFieldConstants.ValidLengthSquaredThreshold)
                    continue;

                float dot = nl * ml + na * ma + nb * mb;
                if (dot <= 0f)
                    continue;

                float wSpace = spaceTable[(dy + DirectionSmoothConstants.Radius) * DirectionSmoothConstants.SpaceTableStride + (dx + DirectionSmoothConstants.Radius)];

                float dcl = cl - colorTile[sTile + 0];
                float dca = ca - colorTile[sTile + 1];
                float dcb = cb - colorTile[sTile + 2];
                float colorDistSq = dcl * dcl + dca * dca + dcb * dcb;
                float wColor = Hlsl.Exp(-colorDistSq / Hlsl.Max(sigmaColorSq, 1e-6f));

                float w = wSpace * wColor * dot;

                sumL += ml * w;
                sumA += ma * w;
                sumB += mb * w;
                sumW += w;
            }
        }

        if (sumW > 1e-6f)
        {
            float avgL = sumL / sumW;
            float avgA = sumA / sumW;
            float avgB = sumB / sumW;
            float norm = Hlsl.Sqrt(avgL * avgL + avgA * avgA + avgB * avgB);
            if (norm > 1e-6f)
            {
                float inv = 1f / norm;
                targetDirections[triple + 0] = avgL * inv;
                targetDirections[triple + 1] = avgA * inv;
                targetDirections[triple + 2] = avgB * inv;
                return;
            }
        }

        targetDirections[triple + 0] = nl;
        targetDirections[triple + 1] = na;
        targetDirections[triple + 2] = nb;
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct AdoptRegionShader(
    ReadWriteBuffer<float> computedDirections,
    ReadWriteBuffer<float> previousDirections,
    ReadWriteBuffer<int> adoptMask,
    int width,
    int height) : IComputeShader
{
    private readonly ReadWriteBuffer<float> computedDirections = computedDirections;
    private readonly ReadWriteBuffer<float> previousDirections = previousDirections;
    private readonly ReadWriteBuffer<int> adoptMask = adoptMask;
    private readonly int width = width;
    private readonly int height = height;

    public void Execute()
    {
        int x = ThreadIds.X;
        int y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        int index = y * width + x;
        int triple = index * 3;

        if (adoptMask[index] != 0)
            return;

        computedDirections[triple + 0] = previousDirections[triple + 0];
        computedDirections[triple + 1] = previousDirections[triple + 1];
        computedDirections[triple + 2] = previousDirections[triple + 2];
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct CopyDirectionsShader(
    ReadWriteBuffer<float> source,
    ReadWriteBuffer<float> target,
    int width,
    int height) : IComputeShader
{
    private readonly ReadWriteBuffer<float> source = source;
    private readonly ReadWriteBuffer<float> target = target;
    private readonly int width = width;
    private readonly int height = height;

    public void Execute()
    {
        int x = ThreadIds.X;
        int y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        int triple = (y * width + x) * 3;
        target[triple + 0] = source[triple + 0];
        target[triple + 1] = source[triple + 1];
        target[triple + 2] = source[triple + 2];
    }
}

// packed byte から線形値への変換は256通りしかない。
// 伝播ループが近傍ごとに同じ変換を繰り返さないよう、同一の式で表を一度だけ作る。
[ThreadGroupSize(DefaultThreadGroupSizes.X)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct SrgbToLinearTableShader(
    ReadWriteBuffer<float> table) : IComputeShader
{
    private readonly ReadWriteBuffer<float> table = table;

    public void Execute()
    {
        int index = ThreadIds.X;
        if (index >= 256)
            return;

        float srgb = index * (1f / 255f);

        table[index] = srgb <= 0.04045f ? srgb / 12.92f : Hlsl.Pow((srgb + 0.055f) / 1.055f, 2.4f);
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct CopyPackedShader(
    IReadOnlyBuffer<int> source,
    ReadWriteBuffer<int> target,
    int width,
    int height) : IComputeShader
{
    private readonly IReadOnlyBuffer<int> source = source;
    private readonly ReadWriteBuffer<int> target = target;
    private readonly int width = width;
    private readonly int height = height;

    public void Execute()
    {
        int x = ThreadIds.X;
        int y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        int index = y * width + x;
        target[index] = source[index];
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct MaskCountShader(
    ReadWriteBuffer<int> mask,
    ReadWriteBuffer<int> count,
    int width,
    int height) : IComputeShader
{
    private readonly ReadWriteBuffer<int> mask = mask;
    private readonly ReadWriteBuffer<int> count = count;
    private readonly int width = width;
    private readonly int height = height;

    [GroupShared(DirectionSmoothConstants.GroupSize * DirectionSmoothConstants.GroupSize)]
    private static readonly int[] partialCounts = null!;

    public void Execute()
    {
        int x = ThreadIds.X;
        int y = ThreadIds.Y;

        int local = GroupIds.Index;
        partialCounts[local] = (x < width && y < height && mask[y * width + x] != 0) ? 1 : 0;

        Hlsl.GroupMemoryBarrierWithGroupSync();

        if (local != 0)
            return;

        int total = 0;
        for (int i = 0; i < GroupSize.Count; i++)
            total += partialCounts[i];

        if (total != 0)
            Hlsl.InterlockedAdd(ref count[0], total);
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct ClusterAssignAccumulateShader(
    ReadWriteBuffer<float> directions,
    ReadOnlyBuffer<float> centers,
    ReadWriteBuffer<int> accumulators,
    int clusterCount,
    float fixedPointScale,
    int width,
    int height) : IComputeShader
{
    private readonly ReadWriteBuffer<float> directions = directions;
    private readonly ReadOnlyBuffer<float> centers = centers;
    private readonly ReadWriteBuffer<int> accumulators = accumulators;
    private readonly int clusterCount = clusterCount;
    private readonly float fixedPointScale = fixedPointScale;
    private readonly int width = width;
    private readonly int height = height;

    [GroupShared(ClusterAccumulateConstants.SlotCount)]
    private static readonly int[] partialAccumulators = null!;

    public void Execute()
    {
        int accumulatorLength = clusterCount * 3 + clusterCount;

        for (int slot = GroupIds.Index; slot < accumulatorLength; slot += GroupSize.Count)
            partialAccumulators[slot] = 0;

        Hlsl.GroupMemoryBarrierWithGroupSync();

        int x = ThreadIds.X;
        int y = ThreadIds.Y;

        if (x < width && y < height)
        {
            int triple = (y * width + x) * 3;

            float nl = directions[triple + 0];
            float na = directions[triple + 1];
            float nb = directions[triple + 2];

            if (nl * nl + na * na + nb * nb >= DirectionFieldConstants.ValidLengthSquaredThreshold)
            {
                int best = 0;
                float bestDot = -2f;

                for (int c = 0; c < clusterCount; c++)
                {
                    int cBase = c * 3;
                    float dot = nl * centers[cBase + 0] + na * centers[cBase + 1] + nb * centers[cBase + 2];
                    if (dot > bestDot)
                    {
                        bestDot = dot;
                        best = c;
                    }
                }

                int sumBase = best * 3;
                Hlsl.InterlockedAdd(ref partialAccumulators[sumBase + 0], (int)Hlsl.Round(nl * fixedPointScale));
                Hlsl.InterlockedAdd(ref partialAccumulators[sumBase + 1], (int)Hlsl.Round(na * fixedPointScale));
                Hlsl.InterlockedAdd(ref partialAccumulators[sumBase + 2], (int)Hlsl.Round(nb * fixedPointScale));
                Hlsl.InterlockedAdd(ref partialAccumulators[clusterCount * 3 + best], 1);
            }
        }

        Hlsl.GroupMemoryBarrierWithGroupSync();

        for (int slot = GroupIds.Index; slot < accumulatorLength; slot += GroupSize.Count)
        {
            int value = partialAccumulators[slot];
            if (value != 0)
                Hlsl.InterlockedAdd(ref accumulators[slot], value);
        }
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct ProjectionHistogramShader(
    ReadWriteBuffer<float> colorLab,
    ReadWriteBuffer<float> directions,
    ReadOnlyBuffer<float> centers,
    ReadWriteBuffer<int> histogram,
    float backgroundL,
    float backgroundA,
    float backgroundB,
    int clusterCount,
    int binsPerCluster,
    float projectionScale,
    int width,
    int height) : IComputeShader
{
    private readonly ReadWriteBuffer<float> colorLab = colorLab;
    private readonly ReadWriteBuffer<float> directions = directions;
    private readonly ReadOnlyBuffer<float> centers = centers;
    private readonly ReadWriteBuffer<int> histogram = histogram;
    private readonly float backgroundL = backgroundL;
    private readonly float backgroundA = backgroundA;
    private readonly float backgroundB = backgroundB;
    private readonly int clusterCount = clusterCount;
    private readonly int binsPerCluster = binsPerCluster;
    private readonly float projectionScale = projectionScale;
    private readonly int width = width;
    private readonly int height = height;

    public void Execute()
    {
        int x = ThreadIds.X;
        int y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        int index = y * width + x;
        int triple = index * 3;

        float nl = directions[triple + 0];
        float na = directions[triple + 1];
        float nb = directions[triple + 2];

        if (nl * nl + na * na + nb * nb < DirectionFieldConstants.ValidLengthSquaredThreshold)
            return;

        int best = 0;
        float bestDot = -2f;

        for (int c = 0; c < clusterCount; c++)
        {
            int cBase = c * 3;
            float dot = nl * centers[cBase + 0] + na * centers[cBase + 1] + nb * centers[cBase + 2];
            if (dot > bestDot)
            {
                bestDot = dot;
                best = c;
            }
        }

        int cBase2 = best * 3;
        float dl = colorLab[triple + 0] - backgroundL;
        float da = colorLab[triple + 1] - backgroundA;
        float db = colorLab[triple + 2] - backgroundB;

        float proj = dl * centers[cBase2 + 0] + da * centers[cBase2 + 1] + db * centers[cBase2 + 2];
        if (proj <= 0f)
            return;

        int bin = (int)(proj * projectionScale);
        if (bin >= binsPerCluster)
            bin = binsPerCluster - 1;
        if (bin < 0)
            bin = 0;

        Hlsl.InterlockedAdd(ref histogram[best * binsPerCluster + bin], 1);
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct ForegroundSeedShader(
    IReadOnlyBuffer<int> bgra,
    ReadWriteBuffer<float> colorLab,
    ReadWriteBuffer<int> foreground,
    float backgroundL,
    float backgroundA,
    float backgroundB,
    float referencePerp,
    int width,
    int height) : IComputeShader
{
    private readonly IReadOnlyBuffer<int> bgra = bgra;
    private readonly ReadWriteBuffer<float> colorLab = colorLab;
    private readonly ReadWriteBuffer<int> foreground = foreground;
    private readonly float backgroundL = backgroundL;
    private readonly float backgroundA = backgroundA;
    private readonly float backgroundB = backgroundB;
    private readonly float referencePerp = referencePerp;
    private readonly int width = width;
    private readonly int height = height;

    public void Execute()
    {
        int x = ThreadIds.X;
        int y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        int index = y * width + x;
        int triple = index * 3;

        int packed = bgra[index];
        int a = (packed >> 24) & 0xFF;

        if (a == 0)
        {
            foreground[index] = 0;
            return;
        }

        float bgLenSq = backgroundL * backgroundL + backgroundA * backgroundA + backgroundB * backgroundB;

        if (bgLenSq <= 1e-8f || referencePerp <= 1e-5f)
        {
            foreground[index] = 0;
            return;
        }

        float labL = colorLab[triple + 0];
        float labA = colorLab[triple + 1];
        float labB = colorLab[triple + 2];

        float along = (labL * backgroundL + labA * backgroundA + labB * backgroundB) / bgLenSq;
        float pl = labL - along * backgroundL;
        float pa = labA - along * backgroundA;
        float pb = labB - along * backgroundB;
        float perp = Hlsl.Sqrt(pl * pl + pa * pa + pb * pb);

        if (perp < referencePerp)
        {
            foreground[index] = 0;
            return;
        }

        float invA = 1f / a;
        float bSrgb = Hlsl.Saturate(((packed >> 0) & 0xFF) * invA);
        float gSrgb = Hlsl.Saturate(((packed >> 8) & 0xFF) * invA);
        float rSrgb = Hlsl.Saturate(((packed >> 16) & 0xFF) * invA);

        int rByte = (int)(rSrgb * 255f + 0.5f);
        int gByte = (int)(gSrgb * 255f + 0.5f);
        int bByte = (int)(bSrgb * 255f + 0.5f);

        foreground[index] = (0xFF << 24) | (rByte << 16) | (gByte << 8) | bByte;
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct SharedTextureToBufferShader(
    IReadWriteNormalizedTexture2D<float4> source,
    ReadWriteBuffer<int> bgra,
    int width,
    int height) : IComputeShader
{
    private readonly IReadWriteNormalizedTexture2D<float4> source = source;
    private readonly ReadWriteBuffer<int> bgra = bgra;
    private readonly int width = width;
    private readonly int height = height;

    public void Execute()
    {
        int x = ThreadIds.X;
        int y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        float4 value = source[x, y];

        int b = (int)(Hlsl.Saturate(value.Z) * 255f + 0.5f);
        int g = (int)(Hlsl.Saturate(value.Y) * 255f + 0.5f);
        int r = (int)(Hlsl.Saturate(value.X) * 255f + 0.5f);
        int a = (int)(Hlsl.Saturate(value.W) * 255f + 0.5f);

        bgra[y * width + x] = (a << 24) | (r << 16) | (g << 8) | b;
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct BufferToSharedTextureShader(
    IReadOnlyBuffer<int> bgra,
    IReadWriteNormalizedTexture2D<float4> destination,
    int width,
    int height) : IComputeShader
{
    private readonly IReadOnlyBuffer<int> bgra = bgra;
    private readonly IReadWriteNormalizedTexture2D<float4> destination = destination;
    private readonly int width = width;
    private readonly int height = height;

    public void Execute()
    {
        int x = ThreadIds.X;
        int y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        int packed = bgra[y * width + x];

        destination[x, y] = new float4(
            ((packed >> 16) & 0xFF) / 255f,
            ((packed >> 8) & 0xFF) / 255f,
            ((packed >> 0) & 0xFF) / 255f,
            ((packed >> 24) & 0xFF) / 255f);
    }
}

internal static class PremultipliedLinearConstants
{
    public const int AlphaCount = 256;
    public const int ChannelCount = 256;
    public const int TableLength = AlphaCount * ChannelCount;
}

internal static class ClusterAccumulateConstants
{
    public const int MaxClusters = 4;
    public const int SlotCount = MaxClusters * 3 + MaxClusters;
}

internal static class ForegroundPropagateConstants
{
    public const int GroupSize = 8;
    public const int Radius = 4;
    public const int TileSize = GroupSize + Radius * 2;
    public const int TileCount = TileSize * TileSize;
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct ForegroundPropagateShader(
    ReadWriteBuffer<int> sourceForeground,
    IReadOnlyBuffer<int> bgra,
    IReadOnlyBuffer<float> srgbToLinear,
    IReadOnlyBuffer<float> premultipliedLinear,
    ReadWriteBuffer<int> targetForeground,
    float backgroundR,
    float backgroundG,
    float backgroundB,
    float sigmaLineSq,
    int width,
    int height) : IComputeShader
{
    private readonly ReadWriteBuffer<int> sourceForeground = sourceForeground;
    private readonly IReadOnlyBuffer<int> bgra = bgra;
    private readonly IReadOnlyBuffer<float> srgbToLinear = srgbToLinear;
    private readonly IReadOnlyBuffer<float> premultipliedLinear = premultipliedLinear;
    private readonly ReadWriteBuffer<int> targetForeground = targetForeground;
    private readonly float backgroundR = backgroundR;
    private readonly float backgroundG = backgroundG;
    private readonly float backgroundB = backgroundB;
    private readonly float sigmaLineSq = sigmaLineSq;
    private readonly int width = width;
    private readonly int height = height;

    [GroupShared(ForegroundPropagateConstants.TileCount)]
    private static readonly int[] foregroundTile = null!;
    [GroupShared(ForegroundPropagateConstants.TileCount * 3)]
    private static readonly float[] deltaTile = null!;
    [GroupShared(ForegroundPropagateConstants.TileCount)]
    private static readonly float[] lengthTile = null!;
    [GroupShared(ForegroundPropagateConstants.TileCount)]
    private static readonly float[] purityTile = null!;
    [GroupShared(1)]
    private static readonly int[] tileHasCandidate = null!;

    public void Execute()
    {
        int x = ThreadIds.X;
        int y = ThreadIds.Y;

        if (GroupIds.Index == 0)
            tileHasCandidate[0] = 0;

        Hlsl.GroupMemoryBarrierWithGroupSync();

        float bgRl = backgroundR <= 0.04045f ? backgroundR / 12.92f : Hlsl.Pow((backgroundR + 0.055f) / 1.055f, 2.4f);
        float bgGl = backgroundG <= 0.04045f ? backgroundG / 12.92f : Hlsl.Pow((backgroundG + 0.055f) / 1.055f, 2.4f);
        float bgBl = backgroundB <= 0.04045f ? backgroundB / 12.92f : Hlsl.Pow((backgroundB + 0.055f) / 1.055f, 2.4f);
        float bgLenSq = bgRl * bgRl + bgGl * bgGl + bgBl * bgBl;

        int originX = x - GroupIds.X - ForegroundPropagateConstants.Radius;
        int originY = y - GroupIds.Y - ForegroundPropagateConstants.Radius;

        for (int slot = GroupIds.Index; slot < ForegroundPropagateConstants.TileCount; slot += GroupSize.Count)
        {
            int localY = slot / ForegroundPropagateConstants.TileSize;
            int localX = slot - localY * ForegroundPropagateConstants.TileSize;
            int sampleX = originX + localX;
            int sampleY = originY + localY;
            int tileTriple = slot * 3;

            int sample = 0;
            if (sampleX >= 0 && sampleX < width && sampleY >= 0 && sampleY < height)
                sample = sourceForeground[sampleY * width + sampleX];

            foregroundTile[slot] = sample;

            if (sample != 0)
                tileHasCandidate[0] = 1;

            if (sample == 0)
            {
                deltaTile[tileTriple + 0] = 0f;
                deltaTile[tileTriple + 1] = 0f;
                deltaTile[tileTriple + 2] = 0f;
                lengthTile[slot] = 0f;
                purityTile[slot] = 0f;
                continue;
            }

            float fr = srgbToLinear[(sample >> 16) & 0xFF];
            float fg = srgbToLinear[(sample >> 8) & 0xFF];
            float fb = srgbToLinear[(sample >> 0) & 0xFF];

            float dr = fr - bgRl;
            float dg = fg - bgGl;
            float db = fb - bgBl;

            deltaTile[tileTriple + 0] = dr;
            deltaTile[tileTriple + 1] = dg;
            deltaTile[tileTriple + 2] = db;
            lengthTile[slot] = dr * dr + dg * dg + db * db;

            float dotFB = fr * bgRl + fg * bgGl + fb * bgBl;
            purityTile[slot] = fr * fr + fg * fg + fb * fb - (bgLenSq > 1e-8f ? dotFB * dotFB / bgLenSq : 0f);
        }

        Hlsl.GroupMemoryBarrierWithGroupSync();

        if (x >= width || y >= height)
            return;

        int index = y * width + x;

        if (tileHasCandidate[0] == 0)
        {
            targetForeground[index] = 0;
            return;
        }

        int packed = bgra[index];
        int a = (packed >> 24) & 0xFF;

        if (a == 0)
        {
            targetForeground[index] = 0;
            return;
        }

        int tableBase = a << 8;
        float observedR = premultipliedLinear[tableBase + ((packed >> 16) & 0xFF)];
        float observedG = premultipliedLinear[tableBase + ((packed >> 8) & 0xFF)];
        float observedB = premultipliedLinear[tableBase + ((packed >> 0) & 0xFF)];

        float obr = observedR - bgRl;
        float obg = observedG - bgGl;
        float obb = observedB - bgBl;

        int bestForeground = 0;
        float bestPurity = -1f;

        int centerLocalX = GroupIds.X + ForegroundPropagateConstants.Radius;
        int centerLocalY = GroupIds.Y + ForegroundPropagateConstants.Radius;

        for (int dy = -ForegroundPropagateConstants.Radius; dy <= ForegroundPropagateConstants.Radius; dy++)
        {
            int localY = centerLocalY + dy;

            for (int dx = -ForegroundPropagateConstants.Radius; dx <= ForegroundPropagateConstants.Radius; dx++)
            {
                int slot = localY * ForegroundPropagateConstants.TileSize + centerLocalX + dx;

                int f = foregroundTile[slot];
                if (f == 0)
                    continue;

                float dlen2 = lengthTile[slot];
                if (dlen2 < 1e-8f)
                    continue;

                int tileTriple = slot * 3;
                float dr = deltaTile[tileTriple + 0];
                float dg = deltaTile[tileTriple + 1];
                float db = deltaTile[tileTriple + 2];

                float t = (obr * dr + obg * dg + obb * db) / dlen2;
                float pr = obr - t * dr;
                float pg = obg - t * dg;
                float pb = obb - t * db;
                float distSq = pr * pr + pg * pg + pb * pb;
                if (distSq > sigmaLineSq * dlen2)
                    continue;

                float purity = purityTile[slot];
                if (purity > bestPurity)
                {
                    bestPurity = purity;
                    bestForeground = f;
                }
            }
        }

        if (bestPurity >= 0f)
        {
            targetForeground[index] = bestForeground;
            return;
        }

        targetForeground[index] = 0;
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.X)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct PremultipliedLinearTableShader(
    ReadWriteBuffer<float> table) : IComputeShader
{
    private readonly ReadWriteBuffer<float> table = table;

    public void Execute()
    {
        int index = ThreadIds.X;
        if (index >= PremultipliedLinearConstants.TableLength)
            return;

        int a = index >> 8;
        int channel = index & 0xFF;

        if (a == 0)
        {
            table[index] = 0f;
            return;
        }

        float invA = 1f / a;
        float straight = Hlsl.Saturate(channel * invA);

        table[index] = straight <= 0.04045f ? straight / 12.92f : Hlsl.Pow((straight + 0.055f) / 1.055f, 2.4f);
    }
}
