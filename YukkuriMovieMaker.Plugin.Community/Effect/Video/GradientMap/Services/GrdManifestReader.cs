using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.IO;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Interfaces;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Models;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Services;

public sealed class GrdManifestReader : IGrdManifestReader
{
    private readonly ConcurrentDictionary<string, GrdManifest> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    public GrdManifest Read(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return GrdManifest.Empty;

        if (_cache.TryGetValue(filePath, out var cached))
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
        _cache[filePath] = hydrated;
        return hydrated;
    }
}
