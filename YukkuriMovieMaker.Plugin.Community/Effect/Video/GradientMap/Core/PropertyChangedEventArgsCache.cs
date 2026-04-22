using System.Collections.Concurrent;
using System.ComponentModel;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Core;

internal static class PropertyChangedEventArgsCache
{
    private static readonly ConcurrentDictionary<string, PropertyChangedEventArgs> Cache =
        new(StringComparer.Ordinal);

    internal static PropertyChangedEventArgs Get(string propertyName) =>
        Cache.GetOrAdd(propertyName, static name => new PropertyChangedEventArgs(name));
}
