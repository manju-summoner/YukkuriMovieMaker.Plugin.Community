namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

public sealed class TemplateDialogEventArgs : EventArgs
{
    public string InitialName { get; }
    public bool IsEditMode { get; }
    public TemplateWindowResult Result { get; set; }
    public string TemplateName { get; set; } = string.Empty;

    public TemplateDialogEventArgs(string initialName, bool isEditMode)
    {
        InitialName = initialName;
        IsEditMode = isEditMode;
    }
}
