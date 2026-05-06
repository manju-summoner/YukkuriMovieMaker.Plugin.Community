using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Equalizer2
{
    public sealed class DoubleToGridLengthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is double size && !double.IsNaN(size) && !double.IsInfinity(size)
                ? new GridLength(size)
                : new GridLength(200);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is GridLength gridLength && gridLength.IsAbsolute
                ? gridLength.Value
                : 200d;
    }
}
