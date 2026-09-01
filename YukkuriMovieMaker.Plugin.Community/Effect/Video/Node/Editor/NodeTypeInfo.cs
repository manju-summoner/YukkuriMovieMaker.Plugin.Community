namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor;

public record NodeTypeInfo
{
    public Type Type { get; set; } = null!;
    public string Category { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
}