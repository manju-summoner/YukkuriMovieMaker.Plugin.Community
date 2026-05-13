namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

public sealed class ConfirmationEventArgs : EventArgs
{
    public string Message { get; }
    public string Title { get; }
    public bool Confirmed { get; set; }

    public ConfirmationEventArgs(string message, string title)
    {
        Message = message;
        Title = title;
    }
}
