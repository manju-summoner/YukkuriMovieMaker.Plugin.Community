using System.Collections.Concurrent;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Interfaces;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Core;

public sealed class ServiceRegistry : IServiceRegistry, IDisposable
{
    private readonly ConcurrentDictionary<Type, object> _singletons = new();
    private readonly ConcurrentDictionary<Type, Func<object>> _factories = new();
    private volatile bool _disposed;

    public void RegisterSingleton<TService>(TService instance) where TService : notnull
    {
        _singletons[typeof(TService)] = instance;
    }

    public void RegisterFactory<TService>(Func<TService> factory) where TService : notnull
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factories[typeof(TService)] = factory as Func<object> ?? (() => factory());
    }

    public TService Resolve<TService>() where TService : notnull
    {
        if (_singletons.TryGetValue(typeof(TService), out var singleton))
            return (TService)singleton;
        if (_factories.TryGetValue(typeof(TService), out var factory))
            return (TService)factory();
        throw new InvalidOperationException(
            $"Service '{typeof(TService).FullName}' is not registered.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var pair in _singletons)
        {
            if (pair.Value is IDisposable disposable)
                disposable.Dispose();
        }
        _singletons.Clear();
        _factories.Clear();
    }
}
