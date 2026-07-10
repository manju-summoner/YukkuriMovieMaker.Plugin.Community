using System.Diagnostics.CodeAnalysis;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Cache;

internal sealed class GpuResourceCache
{
    private static readonly TimeSpan ReleaseDelay = TimeSpan.FromSeconds(5);
    private static readonly Lazy<GpuResourceCache> LazyInstance = new(() => new GpuResourceCache());

    private readonly Dictionary<string, GpuResourceCacheItem> _cache = [];
    private readonly Dictionary<string, ReleaseRegistration> _pendingReleases = [];
    private readonly Lock _lock = new();

    public static GpuResourceCache Instance => LazyInstance.Value;

    private GpuResourceCache()
    {
    }

    public bool TryGetValue(string key, [NotNullWhen(true)] out GpuResourceCacheItem? item)
    {
        item = null;
        if (string.IsNullOrEmpty(key)) return false;

        lock (_lock)
        {
            CancelPendingRelease(key);
            return _cache.TryGetValue(key, out item);
        }
    }

    public void AddOrUpdate(string key, GpuResourceCacheItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (string.IsNullOrEmpty(key)) return;

        GpuResourceCacheItem? replaced = null;

        lock (_lock)
        {
            CancelPendingRelease(key);

            if (_cache.TryGetValue(key, out var existing) && !ReferenceEquals(existing, item))
                replaced = existing;

            _cache[key] = item;
        }

        SafeDispose(replaced);
    }

    public void ScheduleRelease(string key)
    {
        if (string.IsNullOrEmpty(key)) return;

        lock (_lock)
        {
            if (!_cache.ContainsKey(key)) return;

            CancelPendingRelease(key);

            var registration = new ReleaseRegistration();
            _pendingReleases[key] = registration;
            registration.Timer = new Timer(_ => Release(key, registration), null, ReleaseDelay, Timeout.InfiniteTimeSpan);
        }
    }

    private void Release(string key, ReleaseRegistration registration)
    {
        GpuResourceCacheItem? released = null;

        lock (_lock)
        {
            if (!_pendingReleases.TryGetValue(key, out var pending) || !ReferenceEquals(pending, registration))
                return;

            _pendingReleases.Remove(key);
            registration.Timer?.Dispose();

            if (_cache.Remove(key, out var item))
                released = item;
        }

        SafeDispose(released);
    }

    private void CancelPendingRelease(string key)
    {
        if (!_pendingReleases.Remove(key, out var registration)) return;
        registration.Timer?.Dispose();
    }

    private static void SafeDispose(IDisposable? disposable)
    {
        if (disposable is null) return;

        try
        {
            disposable.Dispose();
        }
        catch
        {
        }
    }

    private sealed class ReleaseRegistration
    {
        public Timer? Timer;
    }
}
