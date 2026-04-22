using System.Collections.Immutable;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Models;

public sealed record GrdManifest(string FilePath, ImmutableArray<GrdGradientEntry> Gradients)
{
    public static readonly GrdManifest Empty = new(string.Empty, []);
    public bool IsMultiple => Gradients.Length > 1;
    public int Count => Gradients.Length;
}
