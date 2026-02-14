namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class SubGraphAttribute(string label, string description) : Attribute
{
    public string Label { get; } = label;
    public string Description { get; } = description;
}