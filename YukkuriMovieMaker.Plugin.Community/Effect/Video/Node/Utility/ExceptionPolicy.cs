namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Utility;

internal static class ExceptionPolicy
{
    public static bool IsFatal(Exception ex)
    {
        return ex is OutOfMemoryException or StackOverflowException or AccessViolationException;
    }
}