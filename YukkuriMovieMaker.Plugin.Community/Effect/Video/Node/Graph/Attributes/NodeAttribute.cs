namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public sealed class NodeAttribute(string category, string label, string description) : Attribute
{
    public string Category { get; } = category;
    public string Label { get; } = label;
    public string Description { get; } = description;
}