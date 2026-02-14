namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;

public sealed class NodeConnection
{
    // Output側
    public Guid FromId { get; init; }
    public string FromPort { get; init; } = "";

    // Input側
    public Guid ToId { get; init; }
    public string ToPort { get; init; } = "";
}