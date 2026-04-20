namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Interfaces;

public interface IObjectPool<T> where T : class
{
    T Rent();
    void Return(T item);
}
