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

        return ControlRegistry.CreateControl(port.ControlAttribute);
    }
}