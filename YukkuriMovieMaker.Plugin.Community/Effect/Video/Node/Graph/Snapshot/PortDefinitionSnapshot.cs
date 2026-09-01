namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Snapshot;

public sealed class PortDefinitionSnapshot
{
    public string Name { get; init; } = "";
    public string TypeName { get; init; } = "";
    public string Label { get; init; } = "";
    public string Description { get; init; } = "";
    public object? DefaultValue { get; init; }
}