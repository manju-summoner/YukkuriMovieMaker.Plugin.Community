using System.Globalization;
using System.Windows;
using System.Windows.Data;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Fog
{
    internal sealed class DepthSettingsVisibleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double amount && amount > 0)
                return Visibility.Visible;
            if (parameter is Animation animation && animation.Values.Count > 1)
                return Visibility.Visible;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
