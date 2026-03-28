namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public sealed class NodeAttribute(
    Type categoryType,
    string label,
    string description) : Attribute
{
    public Type CategoryType { get; } = categoryType;
    public string Label { get; } = label;
    public string Description { get; } = description;
}