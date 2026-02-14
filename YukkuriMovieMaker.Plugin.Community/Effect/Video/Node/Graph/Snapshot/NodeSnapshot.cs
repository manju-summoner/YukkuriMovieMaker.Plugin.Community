namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Snapshot;

public sealed class NodeSnapshot
{
    public Guid Id { get; init; }
    public string TypeName { get; init; } = "";
    public Dictionary<string, object?> InputsValues { get; init; } = new();
    public double X { get; init; }
    public double Y { get; init; }
}