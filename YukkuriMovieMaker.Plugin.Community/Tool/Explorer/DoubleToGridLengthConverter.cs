using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Explorer
{
    public sealed class DoubleToGridLengthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is double width && !double.IsNaN(width) && !double.IsInfinity(width)
                ? new GridLength(width)
                : new GridLength(150);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is GridLength gridLength && gridLength.IsAbsolute
                ? gridLength.Value
                : 150d;
    }
}
