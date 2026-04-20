namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Interfaces;

public interface IServiceRegistry
{
    void RegisterSingleton<TService>(TService instance) where TService : notnull;
    void RegisterFactory<TService>(Func<TService> factory) where TService : notnull;
    TService Resolve<TService>() where TService : notnull;
}
