namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.ValueTypes;

[AttributeUsage(AttributeTargets.Field)]
public class EnumDisplayAttribute(string name) : Attribute
{
    public string Name { get; set; } = name;
}