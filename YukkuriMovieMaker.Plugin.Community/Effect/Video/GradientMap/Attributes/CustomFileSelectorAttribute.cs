using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Views;
using System.Windows;
using System.Windows.Data;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Views.Converters;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public sealed class CustomFileSelectorAttribute : PropertyEditorAttribute2
{
    public string Extensions { get; }
    public string Filter { get; }
    public string? SpecialExtensions { get; set; }
    public string? SpecialTooltipKey { get; set; }
    public Type? ResourceType { get; set; }

    public CustomFileSelectorAttribute(string extensions, string filter)
    {
        Extensions = extensions;
        Filter = filter;
    }

    public override FrameworkElement Create()
    {
        var control = new CustomFileSelector();
        var tooltip = ResolveTooltip();
        control.Initialize(Extensions, Filter, tooltip);
        return control;
    }

    public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
    {
        if (control is not CustomFileSelector selector) return;
        selector.SetBinding(
            CustomFileSelector.FilePathProperty,
            ItemPropertiesBinding.Create2(itemProperties));
    }

    public override void ClearBindings(FrameworkElement control)
    {
        if (control is not CustomFileSelector selector) return;
        BindingOperations.ClearBinding(selector, CustomFileSelector.FilePathProperty);
    }

    private string? ResolveTooltip()
    {
        if (string.IsNullOrWhiteSpace(SpecialTooltipKey)) return null;
        if (ResourceType is null) return SpecialTooltipKey;

        var prop = ResourceType.GetProperty(
            SpecialTooltipKey,
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Static);

        return prop?.GetValue(null) as string ?? SpecialTooltipKey;
    }
}
