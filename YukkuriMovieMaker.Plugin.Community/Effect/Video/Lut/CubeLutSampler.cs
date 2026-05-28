namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Lut;

internal static class CubeLutSampler
{
    public static (float R, float G, float B) Sample(CubeLut lut, float r, float g, float b)
    {
        var (nr, ng, nb) = Normalize(lut, r, g, b);
        return SampleNormalized(lut, nr, ng, nb);
    }

    public static (float R, float G, float B) SampleNormalized(CubeLut lut, float r, float g, float b)
    {
        if (lut.HasShaper)
            (r, g, b) = ApplyShaper(lut, r, g, b);
        return Trilinear(lut.Data3D, lut.Size3D, r, g, b);
    }

    private static (float R, float G, float B) Normalize(CubeLut lut, float r, float g, float b)
    {
        var (sr, sg, sb) = lut.DomainScale;
        return (
            Math.Clamp((r - lut.DomainMin.R) * sr, 0f, 1f),
            Math.Clamp((g - lut.DomainMin.G) * sg, 0f, 1f),
            Math.Clamp((b - lut.DomainMin.B) * sb, 0f, 1f));
    }

    private static (float R, float G, float B) ApplyShaper(CubeLut lut, float r, float g, float b)
    {
        var data = lut.Data1D!;
        var size = lut.Size1D;
        return (
            SampleShaperChannel(data, size, r, 0),
            SampleShaperChannel(data, size, g, 1),
            SampleShaperChannel(data, size, b, 2));
    }

    private static float SampleShaperChannel(float[] data, int size, float t, int channel)
    {
        if (t <= 0f) return data[channel];
        if (t >= 1f) return data[(size - 1) * 3 + channel];
        var scaled = t * (size - 1);
        var i0 = (int)scaled;
        var i1 = Math.Min(i0 + 1, size - 1);
        var f = scaled - i0;
        var v0 = data[i0 * 3 + channel];
        var v1 = data[i1 * 3 + channel];
        return v0 + (v1 - v0) * f;
    }

    private static (float R, float G, float B) Trilinear(float[] data, int n, float r, float g, float b)
    {
        var last = n - 1f;
        var rs = Math.Clamp(r, 0f, 1f) * last;
        var gs = Math.Clamp(g, 0f, 1f) * last;
        var bs = Math.Clamp(b, 0f, 1f) * last;
        var r0 = (int)rs;
        var g0 = (int)gs;
        var b0 = (int)bs;
        var r1 = Math.Min(r0 + 1, n - 1);
        var g1 = Math.Min(g0 + 1, n - 1);
        var b1 = Math.Min(b0 + 1, n - 1);
        var fr = rs - r0;
        var fg = gs - g0;
        var fb = bs - b0;

        static (float R, float G, float B) Get(float[] d, int n, int r, int g, int b)
        {
            var i = (r + g * n + b * n * n) * 3;
            return (d[i], d[i + 1], d[i + 2]);
        }

        var c000 = Get(data, n, r0, g0, b0);
        var c100 = Get(data, n, r1, g0, b0);
        var c010 = Get(data, n, r0, g1, b0);
        var c110 = Get(data, n, r1, g1, b0);
        var c001 = Get(data, n, r0, g0, b1);
        var c101 = Get(data, n, r1, g0, b1);
        var c011 = Get(data, n, r0, g1, b1);
        var c111 = Get(data, n, r1, g1, b1);

        static (float R, float G, float B) Lerp((float R, float G, float B) a, (float R, float G, float B) b, float t) =>
            (a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);

        var c00 = Lerp(c000, c100, fr);
        var c10 = Lerp(c010, c110, fr);
        var c01 = Lerp(c001, c101, fr);
        var c11 = Lerp(c011, c111, fr);
        var c0 = Lerp(c00, c10, fg);
        var c1 = Lerp(c01, c11, fg);
        return Lerp(c0, c1, fb);
    }
}
