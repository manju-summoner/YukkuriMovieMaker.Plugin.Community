using System;
using System.Windows;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

[AttributeUsage(AttributeTargets.Property)]
internal sealed class EffectTabManagerControlAttribute : PropertyEditorAttribute2
{
    public EffectTabManagerControlAttribute()
    {
        PropertyEditorSize = PropertyEditorSize.FullWidth;
    }

    public override FrameworkElement Create() => new EffectTabManagerControl();

    public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
    {
        if (control is EffectTabManagerControl editor)
        {
            editor.SetProperties(itemProperties);
        }
    }

    public override void ClearBindings(FrameworkElement control)
    {
        if (control is EffectTabManagerControl editor)
        {
            editor.ClearProperties();
        }
    }
}
