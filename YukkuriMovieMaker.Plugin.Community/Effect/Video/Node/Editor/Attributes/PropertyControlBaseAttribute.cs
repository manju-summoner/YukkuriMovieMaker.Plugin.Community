namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;

public abstract class PropertyControlBaseAttribute : Attribute
{
    public abstract string ControlType { get; }
}