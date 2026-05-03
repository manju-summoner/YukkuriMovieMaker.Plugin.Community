namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

public sealed class BookmarkDialogEventArgs : EventArgs
{
    public string InitialName { get; }
    public bool IsEditMode { get; }
    public BookmarkWindowResult Result { get; set; }
    public string BookmarkName { get; set; } = string.Empty;

    public BookmarkDialogEventArgs(string initialName, bool isEditMode)
    {
        InitialName = initialName;
        IsEditMode = isEditMode;
    }
}
