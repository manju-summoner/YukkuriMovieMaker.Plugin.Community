using System.Windows;
using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

internal static class WpfTreeHelper
{
    public static T? FindDescendantByName<T>(DependencyObject root, string name)
        where T : FrameworkElement
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T element && element.Name == name)
                return element;

            var nested = FindDescendantByName<T>(child, name);
            if (nested is not null)
                return nested;
        }

        return null;
    }
}
