using System.Globalization;
using System.IO;
using System.Text;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Lut;

internal sealed class CubeLutParser : ILutParser
{
    private const int MaxLutSize = 128;
    private const int MaxShaperSize = 65536;
    private static readonly char[] WhitespaceChars = [' ', '\t', '\r', '\n'];

    public bool CanParse(string filePath) =>
        string.Equals(Path.GetExtension(filePath), ".cube", StringComparison.OrdinalIgnoreCase);

    public CubeLut? Parse(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return null;

        try
        {
            using var stream = File.OpenRead(filePath);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return ParseCore(reader);
        }
        catch
        {
            return null;
        }
    }

    private static CubeLut? ParseCore(TextReader reader)
    {
        string title = string.Empty;
        int size3D = 0;
        int size1D = 0;
        (float R, float G, float B) domainMin = (0f, 0f, 0f);
        (float R, float G, float B) domainMax = (1f, 1f, 1f);
        bool domainSet = false;
        (float Min, float Max)? inputRange1D = null;
        (float Min, float Max)? inputRange3D = null;

        float[]? data1D = null;
        float[]? data3D = null;
        int read1D = 0;
        int read3D = 0;
        int expected1D = 0;
        int expected3D = 0;

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var span = line.AsSpan().Trim();
            if (span.IsEmpty || span[0] == '#') continue;

            if (TryMatchKeyword(span, "TITLE", out var rest))
            {
                title = ExtractTitle(rest);
                continue;
            }
            if (TryMatchKeyword(span, "LUT_3D_SIZE", out rest))
            {
                if (TryReadInt(rest, out var n) && n is >= 2 and <= MaxLutSize)
                {
                    size3D = n;
                    expected3D = n * n * n;
                    data3D = new float[expected3D * 3];
                }
                continue;
            }
            if (TryMatchKeyword(span, "LUT_1D_SIZE", out rest))
            {
                if (TryReadInt(rest, out var n) && n is >= 2 and <= MaxShaperSize)
                {
                    size1D = n;
                    expected1D = n;
                    data1D = new float[expected1D * 3];
                }
                continue;
            }
            if (TryMatchKeyword(span, "LUT_3D_INPUT_RANGE", out rest))
            {
                if (TryReadFloats(rest, 2, out var values))
                    inputRange3D = (values[0], values[1]);
                continue;
            }
            if (TryMatchKeyword(span, "LUT_1D_INPUT_RANGE", out rest))
            {
                if (TryReadFloats(rest, 2, out var values))
                    inputRange1D = (values[0], values[1]);
                continue;
            }
            if (TryMatchKeyword(span, "DOMAIN_MIN", out rest))
            {
                if (TryReadFloats(rest, 3, out var values))
                {
                    domainMin = (values[0], values[1], values[2]);
                    domainSet = true;
                }
                continue;
            }
            if (TryMatchKeyword(span, "DOMAIN_MAX", out rest))
            {
                if (TryReadFloats(rest, 3, out var values))
                {
                    domainMax = (values[0], values[1], values[2]);
                    domainSet = true;
                }
                continue;
            }

            if (!TryReadFloats(span, 3, out var rgb))
                continue;

            if (data1D is not null && read1D < expected1D)
            {
                var i = read1D * 3;
                data1D[i] = rgb[0];
                data1D[i + 1] = rgb[1];
                data1D[i + 2] = rgb[2];
                read1D++;
            }
            else if (data3D is not null && read3D < expected3D)
            {
                var i = read3D * 3;
                data3D[i] = rgb[0];
                data3D[i + 1] = rgb[1];
                data3D[i + 2] = rgb[2];
                read3D++;
            }
        }

        if (data3D is null || read3D != expected3D || size3D < 2)
            return null;

        if (data1D is not null && read1D != expected1D)
            data1D = null;

        if (!domainSet)
        {
            if (data1D is not null && inputRange1D is { } r1)
                (domainMin, domainMax) = ((r1.Min, r1.Min, r1.Min), (r1.Max, r1.Max, r1.Max));
            else if (inputRange3D is { } r3)
                (domainMin, domainMax) = ((r3.Min, r3.Min, r3.Min), (r3.Max, r3.Max, r3.Max));
        }

        return new CubeLut(
            title,
            size3D,
            data3D,
            data1D is null ? 0 : size1D,
            data1D,
            domainMin,
            domainMax);
    }

    private static bool TryMatchKeyword(ReadOnlySpan<char> span, ReadOnlySpan<char> keyword, out ReadOnlySpan<char> remainder)
    {
        if (span.Length <= keyword.Length || !span.StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
        {
            remainder = default;
            return false;
        }
        var next = span[keyword.Length];
        if (next is not (' ' or '\t'))
        {
            remainder = default;
            return false;
        }
        remainder = span[(keyword.Length + 1)..].TrimStart();
        return true;
    }

    private static string ExtractTitle(ReadOnlySpan<char> span)
    {
        var trimmed = span.Trim();
        if (trimmed.IsEmpty) return string.Empty;
        if (trimmed[0] == '"')
        {
            var closing = trimmed[1..].IndexOf('"');
            if (closing >= 0)
                return trimmed.Slice(1, closing).ToString();
        }
        return trimmed.ToString();
    }

    private static bool TryReadInt(ReadOnlySpan<char> span, out int value)
    {
        var token = NextToken(ref span);
        return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadFloats(ReadOnlySpan<char> span, int count, out float[] values)
    {
        var buffer = new float[count];
        for (var i = 0; i < count; i++)
        {
            var token = NextToken(ref span);
            if (token.IsEmpty ||
                !float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
            {
                values = [];
                return false;
            }
            buffer[i] = f;
        }
        values = buffer;
        return true;
    }

    private static ReadOnlySpan<char> NextToken(ref ReadOnlySpan<char> span)
    {
        span = span.TrimStart();
        if (span.IsEmpty) return default;
        var end = span.IndexOfAny(WhitespaceChars);
        if (end < 0)
        {
            var all = span;
            span = default;
            return all;
        }
        var token = span[..end];
        span = span[end..].TrimStart();
        return token;
    }
}
