using System.Globalization;
using System.Windows.Data;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Converters
{
    public sealed class BoolToStarConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is true ? "\u2605" : "\u2606";

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
