using Vortice.Mathematics;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Localize;
using YukkuriMovieMaker.Resources.Localization;
using static System.String;

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
        return ApplyResourceToCategoryName(
            CategoryName == null!
                ? ((INodeCategory?)Activator.CreateInstance(CategoryType))!.Category
                : CategoryName);
    }

    private string ApplyResourceToCategoryName(string categoryName)
    {
        var categories = categoryName.Split('/')
            .Select(ApplyResource);

        return Join('/', categories);
    }

    private string ApplyResource(string str)
    {
        const string nodeGlobalKeyPrefix = "NodeEffectKey_";
        const string ymmGlobalKeyPrefix = "YMM4Key_";
        string? result;

        if (str.StartsWith(nodeGlobalKeyPrefix))
        {
            result = typeof(TextNode)
                .GetProperty(str.Substring(nodeGlobalKeyPrefix.Length,
                    str.Length - nodeGlobalKeyPrefix.Length))?.GetValue(null)?.ToString();
            if (result != null)
                return result;
        }

        else if (str.StartsWith(ymmGlobalKeyPrefix))
        {
            result = typeof(Texts)
                .GetProperty(str.Substring(ymmGlobalKeyPrefix.Length,
                    str.Length - ymmGlobalKeyPrefix.Length))?.GetValue(null)?.ToString();
            if (result != null)
                return result;
        }

        result = (string?)_resourceType?.GetProperty(str)?.GetValue(null);
        return result ?? str;
    }

    public string GetCategoryColor()
    {
        return CategoryName == null!
            ? ((INodeCategory?)Activator.CreateInstance(CategoryType))!.Color
            : nameof(Colors.AliceBlue);
    }
}