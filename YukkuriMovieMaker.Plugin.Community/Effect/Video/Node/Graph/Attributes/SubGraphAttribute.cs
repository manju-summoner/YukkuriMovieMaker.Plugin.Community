namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class SubGraphAttribute(string label, string description) : Attribute
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
}