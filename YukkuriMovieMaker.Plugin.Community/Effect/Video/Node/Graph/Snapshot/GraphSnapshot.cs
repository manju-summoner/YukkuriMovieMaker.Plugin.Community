namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Snapshot;

public sealed class GraphSnapshot
{
    public List<NodeSnapshot> Nodes { get; init; } = [];
    public List<ConnectionSnapshot> Connections { get; init; } = [];
}