namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class SubGraphAttribute(string label, string description, Type? resourceType = null) : Attribute
{
    public string Label { get; } = label;
    public string Description { get; } = description;

    /// <summary>
    ///     ArgumentsNodeのプロパティ名
    /// </summary>
    public string ArgumentsNodeProperty { get; init; } = "";

    /// <summary>
    ///     ReturnNodeのプロパティ名
    /// </summary>
    public string ReturnNodeProperty { get; init; } = "";

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