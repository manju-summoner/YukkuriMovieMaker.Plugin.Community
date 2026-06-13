using System.Windows;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Views;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public sealed class GradientEditorAttribute : PropertyEditorAttribute2
{
    public GradientEditorAttribute()
    {
        PropertyEditorSize = PropertyEditorSize.FullWidth;
    }

    public override FrameworkElement Create() => new GradientEditor();

    public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
    {
        if (control is not GradientEditor editor) return;
        editor.ItemProperties = itemProperties;
    }

    public override void ClearBindings(FrameworkElement control)
    {
        if (control is not GradientEditor editor) return;
        editor.ItemProperties = [];
    }
}
