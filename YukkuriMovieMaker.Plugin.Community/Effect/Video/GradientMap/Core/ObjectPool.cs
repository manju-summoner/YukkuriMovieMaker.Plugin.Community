using System.Collections.Concurrent;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Interfaces;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Core;

public sealed class ObjectPool<T>(
    Func<T> factory,
    Action<T>? onReturn = null,
    Action<T>? onDestroy = null,
    int maxCapacity = 16) : IObjectPool<T>, IDisposable where T : class
{
    private readonly ConcurrentBag<T> _pool = [];
    private volatile int _count;
    private volatile bool _disposed;

    public T Rent()
    {
        if (_pool.TryTake(out var item))
        {
            Interlocked.Decrement(ref _count);
            return item;
        }
        return factory();
    }

    public void Return(T item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (_disposed)
        {
            onDestroy?.Invoke(item);
            return;
        }
        onReturn?.Invoke(item);
        if (_count < maxCapacity)
        {
            _pool.Add(item);
            Interlocked.Increment(ref _count);
        }
        else
        {
            onDestroy?.Invoke(item);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        while (_pool.TryTake(out var item))
            onDestroy?.Invoke(item);
    }
}
