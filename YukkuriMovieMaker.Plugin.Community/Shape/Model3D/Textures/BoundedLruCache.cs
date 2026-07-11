using System.Runtime.CompilerServices;
using static YukkuriMovieMaker.Plugin.Community.Shape.Model3D.DisposeUtility;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Textures;

internal sealed class BoundedLruCache<TKey, TValue> where TKey : notnull
{
    private readonly Dictionary<TKey, (LinkedListNode<TKey> Node, TValue Value, long Bytes)> _map;
    private readonly LinkedList<TKey> _order;
    private readonly Lock _lock = new();
    private readonly long _maxBytes;
    private long _currentBytes;

    public BoundedLruCache(long maxBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
        _maxBytes = maxBytes;
        _map = new Dictionary<TKey, (LinkedListNode<TKey>, TValue, long)>();
        _order = new LinkedList<TKey>();
    }

    public bool TryGetValue(TKey key, out TValue value)
        => TryGetValue(key, out value, out _);

    public bool TryGetValue(TKey key, out TValue value, out long bytes)
    {
        lock (_lock)
        {
            if (_map.TryGetValue(key, out var entry))
            {
                _order.Remove(entry.Node);
                _order.AddFirst(entry.Node);
                value = entry.Value;
                bytes = entry.Bytes;
                return true;
            }
            value = default!;
            bytes = 0;
            return false;
        }
    }

    public TValue GetOrAdd(TKey key, long bytes, Func<TKey, TValue> factory)
    {
        lock (_lock)
        {
            if (_map.TryGetValue(key, out var existing))
            {
                _order.Remove(existing.Node);
                _order.AddFirst(existing.Node);
                return existing.Value;
            }
        }

        var newValue = factory(key);

        lock (_lock)
        {
            if (_map.TryGetValue(key, out var race))
            {
                _order.Remove(race.Node);
                _order.AddFirst(race.Node);
                if (newValue is IDisposable d) SafeDispose(d);
                return race.Value;
            }

            EvictUntilFits(bytes);

            var node = _order.AddFirst(key);
            _map[key] = (node, newValue, bytes);
            _currentBytes += bytes;
            return newValue;
        }
    }

    public bool TryRemove(TKey key, out TValue value)
    {
        lock (_lock)
        {
            if (_map.TryGetValue(key, out var entry))
            {
                _order.Remove(entry.Node);
                _map.Remove(key);
                _currentBytes -= entry.Bytes;
                if (_currentBytes < 0) _currentBytes = 0;
                value = entry.Value;
                return true;
            }
            value = default!;
            return false;
        }
    }

    public List<(TKey Key, TValue Value)> RemoveWhere(Func<TKey, bool> predicate)
    {
        var removed = new List<(TKey, TValue)>();
        lock (_lock)
        {
            var keysToRemove = new List<TKey>();
            foreach (var key in _map.Keys)
            {
                if (predicate(key)) keysToRemove.Add(key);
            }
            foreach (var key in keysToRemove)
            {
                var entry = _map[key];
                _order.Remove(entry.Node);
                _map.Remove(key);
                _currentBytes -= entry.Bytes;
                removed.Add((key, entry.Value));
            }
            if (_currentBytes < 0) _currentBytes = 0;
        }
        return removed;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EvictUntilFits(long incomingBytes)
    {
        while (_order.Count > 0 && _currentBytes + incomingBytes > _maxBytes)
        {
            var lru = _order.Last!;
            var lruKey = lru.Value;
            if (_map.TryGetValue(lruKey, out var evicted))
            {
                _order.RemoveLast();
                _map.Remove(lruKey);
                _currentBytes -= evicted.Bytes;
                if (_currentBytes < 0) _currentBytes = 0;
                if (evicted.Value is IDisposable d) SafeDispose(d);
            }
            else
            {
                _order.RemoveLast();
            }
        }
    }
}
