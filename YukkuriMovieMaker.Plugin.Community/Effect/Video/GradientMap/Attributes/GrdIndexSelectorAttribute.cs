using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Views;
using System.Windows;
using System.Windows.Data;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Core;
using YukkuriMovieMaker.Views.Converters;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public sealed class GrdIndexSelectorAttribute : PropertyEditorAttribute2
{
    public override FrameworkElement Create()
    {
        var control = new GrdIndexSelector();
        control.Initialize();
        return control;
    }

    public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
    {
        if (control is not GrdIndexSelector selector) return;

        selector.SetBinding(
            GrdIndexSelector.GradientIndexProperty,
            ItemPropertiesBinding.Create2(itemProperties));

        var items = itemProperties.Select(p => p.Item).ToArray();
        selector.AttachBridge(GrdEffectPropertyBridge.TryCreate(selector, items));
    }

    public override void ClearBindings(FrameworkElement control)
    {
        if (control is not GrdIndexSelector selector) return;
        BindingOperations.ClearBinding(selector, GrdIndexSelector.GradientIndexProperty);
        BindingOperations.ClearBinding(selector, GrdIndexSelector.FilePathProperty);
        selector.DetachBridge();
    }
}
