namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

public sealed class TemplateDialogEventArgs(string initialName, bool isEditMode) : EventArgs
{
    public string InitialName { get; } = initialName;
    public bool IsEditMode { get; } = isEditMode;
    public TemplateWindowResult Result { get; set; }
    public string TemplateName { get; set; } = string.Empty;
}
