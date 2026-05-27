namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public sealed class InputPortAttribute(
    string label,
    string description,
    Type? resourceType = null,
    bool isDynamic = false)
    : Attribute
{
    public string Label { get; } = label;
    public string Description { get; } = description;
    public bool IsDynamic { get; } = isDynamic;

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