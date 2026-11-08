using System.Globalization;
using System.Windows;
using System.Windows.Data;
using YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal.Model;
using YukkuriMovieMaker.Plugin.Update;

namespace YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal
{
    public class UpdateBadgeVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            return values is [PluginCatalogItem plugin, IReadOnlyDictionary<PluginCatalogItem, PluginUpdateStatus> statuses]
                && statuses.TryGetValue(plugin, out var status)
                && status.HasUpdate
                ? Visibility.Visible
                : Visibility.Hidden;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
