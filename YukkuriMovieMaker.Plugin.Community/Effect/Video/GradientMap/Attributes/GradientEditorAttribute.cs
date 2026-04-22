using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Views;
using System.Windows;
using System.Windows.Data;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Views.Converters;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public sealed class GradientEditorAttribute : PropertyEditorAttribute2
{
    public GradientEditorAttribute()
    {
        PropertyEditorSize = PropertyEditorSize.FullWidth;
    }

    public override FrameworkElement Create()
    {
        var control = new GradientEditor();
        control.Initialize();
        return control;
    }

    public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
    {
        if (control is not GradientEditor editor) return;
        editor.SetBinding(
            GradientEditor.GradientJsonProperty,
            ItemPropertiesBinding.Create2(itemProperties));
    }

    public override void ClearBindings(FrameworkElement control)
    {
        if (control is not GradientEditor editor) return;
        BindingOperations.ClearBinding(editor, GradientEditor.GradientJsonProperty);
    }
}
