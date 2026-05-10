using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.WaveClipping
{
    internal class SineModeVisibleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is false ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
