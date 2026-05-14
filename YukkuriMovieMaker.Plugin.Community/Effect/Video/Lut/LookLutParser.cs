using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Xml;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Lut;

internal sealed class LookLutParser : ILutParser
{
    private const int MaxLutSize = 128;

    public bool CanParse(string filePath) =>
        string.Equals(Path.GetExtension(filePath), ".look", StringComparison.OrdinalIgnoreCase);

    public CubeLut? Parse(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return null;

        try
        {
            using var stream = File.OpenRead(filePath);
            return ParseCore(stream, Path.GetFileNameWithoutExtension(filePath));
        }
        catch
        {
            return null;
        }
    }

    private static CubeLut? ParseCore(Stream stream, string fallbackTitle)
    {
        var doc = new XmlDocument();
        doc.Load(stream);

        var lutNode = doc.SelectSingleNode("/look/LUT");
        if (lutNode is null)
            return null;

        var sizeText = StripQuotes(lutNode.SelectSingleNode("size")?.InnerText);
        if (!int.TryParse(sizeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var size) || size < 2 || size > MaxLutSize)
            return null;

        var dataText = StripQuotes(lutNode.SelectSingleNode("data")?.InnerText);
        if (string.IsNullOrWhiteSpace(dataText))
            return null;

        var count = checked(size * size * size);
        var data3D = DecodeHexFloats(dataText, count);
        if (data3D is null)
            return null;

        return new CubeLut(
            fallbackTitle,
            size,
            data3D,
            0,
            null,
            (0f, 0f, 0f),
            (1f, 1f, 1f));
    }

    private static float[]? DecodeHexFloats(string hexText, int count)
    {
        var hex = StripWhitespace(hexText);
        var expectedBytes = count * 12;

        if (hex.Length != expectedBytes * 2)
            return null;

        var result = new float[count * 3];
        Span<byte> tripletBytes = stackalloc byte[12];

        for (var i = 0; i < count; i++)
        {
            var hexOffset = i * 24;
            for (var b = 0; b < 12; b++)
            {
                tripletBytes[b] = byte.Parse(hex.AsSpan(hexOffset + b * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            var dataOffset = i * 3;
            result[dataOffset + 0] = MemoryMarshal.Read<float>(tripletBytes[0..4]);
            result[dataOffset + 1] = MemoryMarshal.Read<float>(tripletBytes[4..8]);
            result[dataOffset + 2] = MemoryMarshal.Read<float>(tripletBytes[8..12]);
        }

        return result;
    }

    private static string StripWhitespace(string text)
    {
        var chars = new char[text.Length];
        var length = 0;
        foreach (var ch in text)
        {
            if (ch is not (' ' or '\t' or '\r' or '\n'))
                chars[length++] = ch;
        }
        return new string(chars, 0, length);
    }

    private static string? StripQuotes(string? value)
    {
        if (value is null) return null;
        var trimmed = value.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
            return trimmed[1..^1];
        return trimmed;
    }
}
