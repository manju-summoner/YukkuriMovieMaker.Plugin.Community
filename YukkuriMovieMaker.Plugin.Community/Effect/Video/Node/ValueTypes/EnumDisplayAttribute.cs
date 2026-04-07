namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.ValueTypes;

[AttributeUsage(AttributeTargets.Field)]
public class EnumDisplayAttribute(string name, Type? resourceType = null) : Attribute
{
    public string Name { get; set; } = name;

    public string GetName()
    {
        return resourceType is null
            ? Name
            : resourceType.GetProperty(Name)?.GetValue(null)?.ToString() ?? Name;
    }
}