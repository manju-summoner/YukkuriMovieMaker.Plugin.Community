using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.IO;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Interfaces;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Models;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Services;

public sealed class GrdFormatParser : IGradientFormatParser
{
    private readonly ConcurrentDictionary<string, GrdManifest> _manifestCache =
        new(StringComparer.OrdinalIgnoreCase);

    public bool CanParse(string filePath) =>
        string.Equals(Path.GetExtension(filePath), ".grd", StringComparison.OrdinalIgnoreCase);

    public ID2D1Bitmap? CreateBitmap(
        ID2D1DeviceContext deviceContext,
        string filePath,
        int gradientIndex)
    {
        var pixels = GrdParser.ParseToPixels(filePath, gradientIndex);
        if (pixels is null) return null;
        return GradientTextureFactory.CreateD2DBitmap(deviceContext, pixels, GrdParser.Resolution);
    }

    public GrdManifest ReadManifest(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return GrdManifest.Empty;

        if (_manifestCache.TryGetValue(filePath, out var cached))
            return cached;

        var manifest = GrdParser.ReadManifest(filePath);
        if (manifest == GrdManifest.Empty)
            return manifest;

        var builder = ImmutableArray.CreateBuilder<GrdGradientEntry>(manifest.Gradients.Length);
        for (var i = 0; i < manifest.Gradients.Length; i++)
        {
            var e = manifest.Gradients[i];
            builder.Add(new GrdGradientEntry(e.Index, e.Name, filePath));
        }

        var hydrated = new GrdManifest(filePath, builder.MoveToImmutable());
        _manifestCache[filePath] = hydrated;
        return hydrated;
    }
}
