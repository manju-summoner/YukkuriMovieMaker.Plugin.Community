namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

public sealed class ConfirmationEventArgs(string message, string title) : EventArgs
{
    public string Message { get; } = message;
    public string Title { get; } = title;
    public bool Confirmed { get; set; }
}
