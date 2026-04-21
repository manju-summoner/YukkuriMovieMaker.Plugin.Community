using System.Collections.Immutable;
using System.IO;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Interfaces;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Models;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Services;

public sealed class GrdFormatParser : IGradientFormatParser
{
    private const int MaxManifestCacheSize = 32;

    private readonly Lock _cacheLock = new();
    private readonly LinkedList<KeyValuePair<string, GrdManifest>> _cacheOrder = new();
    private readonly Dictionary<string, LinkedListNode<KeyValuePair<string, GrdManifest>>> _cacheMap =
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

        lock (_cacheLock)
        {
            if (_cacheMap.TryGetValue(filePath, out var cached))
            {
                _cacheOrder.Remove(cached);
                _cacheOrder.AddFirst(cached);
                return cached.Value.Value;
            }
        }

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

        lock (_cacheLock)
        {
            if (_cacheMap.TryGetValue(filePath, out var existing))
            {
                _cacheOrder.Remove(existing);
                _cacheOrder.AddFirst(existing);
                return existing.Value.Value;
            }

            var node = new LinkedListNode<KeyValuePair<string, GrdManifest>>(
                new KeyValuePair<string, GrdManifest>(filePath, hydrated));
            _cacheOrder.AddFirst(node);
            _cacheMap[filePath] = node;

            if (_cacheMap.Count > MaxManifestCacheSize)
            {
                var oldest = _cacheOrder.Last!;
                _cacheOrder.RemoveLast();
                _cacheMap.Remove(oldest.Value.Key);
            }
        }

        return hydrated;
    }
}
