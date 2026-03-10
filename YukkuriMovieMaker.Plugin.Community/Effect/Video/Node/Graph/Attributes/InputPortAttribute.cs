using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public sealed class InputPortAttribute(string label, string description, string color = nameof(Colors.SlateGray))
    : Attribute
{
    public string Label { get; } = label;
    public string Description { get; } = description;
    public string Color { get; } = color;
}