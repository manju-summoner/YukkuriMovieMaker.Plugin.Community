using Vortice.Mathematics;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public sealed class NodeAttribute : Attribute
{
    private readonly Type? _resourceType;

    public NodeAttribute(
        Type categoryType,
        string label,
        string description,
        Type? resourceType = null)
    {
        _resourceType = resourceType;
        CategoryType = categoryType;
        Label = label;
        Description = description;
    }

    public NodeAttribute(
        string category,
        string label,
        string description,
        Type? resourceType = null)
    {
        _resourceType = resourceType;
        CategoryType = null!;
        CategoryName = category;
        Label = label;
        Description = description;
    }

    public Type CategoryType { get; }
    public string CategoryName { get; } = null!;
    public string Label { get; }
    public string Description { get; }

    public string GetLabel()
    {
        return _resourceType is null
            ? Label
            : _resourceType.GetProperty(Label)?.GetValue(null)?.ToString() ?? Label;
    }

    public string GetDescription()
    {
        return _resourceType is null
            ? Description
            : _resourceType.GetProperty(Description)?.GetValue(null)?.ToString() ?? Description;
    }

    public string GetCategoryName()
    {
        return CategoryName == null!
            ? ((INodeCategory?)Activator.CreateInstance(CategoryType))!.Category
            : CategoryName;
    }

    public string GetCategoryColor()
    {
        return CategoryName == null!
            ? ((INodeCategory?)Activator.CreateInstance(CategoryType))!.Color
            : nameof(Colors.AliceBlue);
    }
}