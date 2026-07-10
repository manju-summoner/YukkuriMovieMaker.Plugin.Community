namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D;

internal static class DisposeUtility
{
    public static void SafeDispose(IDisposable? disposable)
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

    public static void SafeDispose<T>(ref T? disposable) where T : class, IDisposable
    {
        var target = disposable;
        disposable = null;
        SafeDispose(target);
    }
}
