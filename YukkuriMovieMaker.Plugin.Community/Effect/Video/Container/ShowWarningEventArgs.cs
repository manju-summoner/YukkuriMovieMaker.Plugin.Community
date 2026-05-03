namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

public sealed class ShowWarningEventArgs : EventArgs
{
    public string Message { get; }
    public string Title { get; }

    public ShowWarningEventArgs(string message, string title)
    {
        Message = message;
        Title = title;
    }
}
