using System.Collections.Frozen;
using System.IO;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Lut;

internal static class LutParserRegistry
{
    private static readonly FrozenDictionary<string, ILutParser> Parsers =
        new Dictionary<string, ILutParser>(StringComparer.OrdinalIgnoreCase)
        {
            [".cube"] = new CubeLutParser(),
            [".look"] = new LookLutParser(),
        }
        .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static CubeLut? Parse(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return null;
        var ext = Path.GetExtension(filePath);
        return Parsers.TryGetValue(ext, out var parser) ? parser.Parse(filePath) : null;
    }

    public static bool CanParse(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;
        return Parsers.ContainsKey(Path.GetExtension(filePath));
    }
}
