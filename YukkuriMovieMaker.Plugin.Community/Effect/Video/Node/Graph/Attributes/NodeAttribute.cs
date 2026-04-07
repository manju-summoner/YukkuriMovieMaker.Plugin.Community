namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public sealed class NodeAttribute(
    Type categoryType,
    string label,
    string description,
    Type? resourceType = null) : Attribute
{
    public Type CategoryType { get; } = categoryType;
    public string Label { get; } = label;
    public string Description { get; } = description;

    public string GetLabel()
    {
        return resourceType is null
            ? Label
            : resourceType.GetProperty(Label)?.GetValue(null)?.ToString() ?? Label;
    }

    public string GetDescription()
    {
        return resourceType is null
            ? Description
            : resourceType.GetProperty(Description)?.GetValue(null)?.ToString() ?? Description;
    }
}