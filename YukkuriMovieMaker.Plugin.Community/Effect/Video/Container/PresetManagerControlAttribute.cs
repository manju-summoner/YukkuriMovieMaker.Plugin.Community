using System.Windows;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

[AttributeUsage(AttributeTargets.Property)]
internal sealed class PresetManagerControlAttribute : PropertyEditorAttribute2
{
    public PresetManagerControlAttribute()
    {
        PropertyEditorSize = PropertyEditorSize.FullWidth;
    }

    public override FrameworkElement Create() => new PresetManagerControl();

    public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
    {
        if (control is PresetManagerControl editor)
        {
            editor.SetProperties(itemProperties);
        }
    }

    public override void ClearBindings(FrameworkElement control)
    {
        if (control is PresetManagerControl editor)
        {
            editor.ClearProperties();
        }
    }
}
