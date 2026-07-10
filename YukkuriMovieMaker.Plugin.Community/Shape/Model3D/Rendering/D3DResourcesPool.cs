using Vortice.Direct3D11;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Rendering;

internal static class D3DResourcesPool
{
    private static readonly TimeSpan ReleaseDelay = TimeSpan.FromSeconds(5);
    private static readonly Dictionary<nint, PoolEntry> Pool = [];
    private static readonly Lock PoolLock = new();

    public static D3DResources Acquire(ID3D11Device device)
    {
        ArgumentNullException.ThrowIfNull(device);
        var key = device.NativePointer;

        lock (PoolLock)
        {
            if (Pool.TryGetValue(key, out var entry) && entry.IsDisposed)
            {
                Pool.Remove(key);
                entry = null;
            }

            entry ??= Pool[key] = new PoolEntry(new D3DResources(device));

            entry.RefCount++;
            entry.Generation++;
            entry.CancelRelease();

            return entry.Resources;
        }
    }

    public static void Release(ID3D11Device device)
    {
        if (device is null) return;
        var key = device.NativePointer;

        lock (PoolLock)
        {
            if (!Pool.TryGetValue(key, out var entry)) return;

            entry.RefCount--;
            if (entry.RefCount > 0) return;

            int generation = ++entry.Generation;
            entry.CancelRelease();
            entry.ReleaseTimer = new Timer(_ => ReleaseNow(key, entry, generation), null, ReleaseDelay, Timeout.InfiniteTimeSpan);
        }
    }

    private static void ReleaseNow(nint key, PoolEntry entry, int generation)
    {
        lock (PoolLock)
        {
            if (entry.IsDisposed || entry.RefCount > 0 || entry.Generation != generation) return;

            entry.IsDisposed = true;
            Pool.Remove(key);
            entry.CancelRelease();
            entry.Resources.Dispose();
        }
    }

    private sealed class PoolEntry(D3DResources resources)
    {
        public D3DResources Resources { get; } = resources;
        public int RefCount;
        public int Generation;
        public bool IsDisposed;
        public Timer? ReleaseTimer;

        public void CancelRelease()
        {
            ReleaseTimer?.Dispose();
            ReleaseTimer = null;
        }
    }
}
