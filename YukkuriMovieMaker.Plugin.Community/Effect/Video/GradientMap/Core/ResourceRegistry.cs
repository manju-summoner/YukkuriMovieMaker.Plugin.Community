using System.Collections.Concurrent;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Interfaces;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Core;

public sealed class ResourceRegistry : IResourceRegistry
{
    private readonly ConcurrentDictionary<IDisposable, byte> _tracked = new();
    private volatile bool _disposed;

    public T Track<T>(T resource) where T : IDisposable
    {
        ArgumentNullException.ThrowIfNull(resource);
        _tracked.TryAdd(resource, 0);
        return resource;
    }

    public void Untrack(IDisposable resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        _tracked.TryRemove(resource, out _);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var resource in _tracked.Keys)
            resource.Dispose();
        _tracked.Clear();
    }
}
