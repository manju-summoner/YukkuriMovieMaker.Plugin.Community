namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Interfaces;

public interface IResourceRegistry : IDisposable
{
    T Track<T>(T resource) where T : IDisposable;
    void Untrack(IDisposable resource);
}
