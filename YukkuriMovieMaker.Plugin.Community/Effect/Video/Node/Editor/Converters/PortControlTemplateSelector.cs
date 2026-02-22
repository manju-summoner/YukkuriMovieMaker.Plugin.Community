using System.Windows;
using System.Windows.Controls;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.ViewModel;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Converters;

/// <summary>
///     ポートの属性に基づいて適切なDataTemplateを選択
/// </summary>
public class PortControlTemplateSelector : DataTemplateSelector
{
    public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
    {
        if (item is not PortViewModel port)
            return null;

        if (!port.HasControl || port.ControlAttribute == null)
            return null;

        var control = ControlRegistry.CreateControl(port.ControlAttribute, port);
        if (control == null)
            return null;

        return CreateDataTemplate(control, port);
    }

    /// <summary>
    ///     コントロールをラップするDataTemplateを生成
    /// </summary>
    private DataTemplate CreateDataTemplate(FrameworkElement control, PortViewModel port)
    {
        var template = new DataTemplate();

        var factory = new FrameworkElementFactory(control.GetType());
        factory.SetValue(FrameworkElement.DataContextProperty, port);

        template.VisualTree = factory;
        return template;
    }
}