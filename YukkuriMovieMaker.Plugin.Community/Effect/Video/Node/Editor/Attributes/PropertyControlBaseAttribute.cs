namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public abstract class PropertyControlBaseAttribute : Attribute
{
    public abstract Type ControlType { get; }

    public abstract object? GetDefaultValue();
}