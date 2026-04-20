using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Converters
{
    public sealed class BoolToStarBrushConverter : IValueConverter
    {
        private static readonly System.Windows.Media.Brush FavoriteBrush = CreateFrozen(Color.FromRgb(255, 193, 7));
        private static readonly System.Windows.Media.Brush DefaultBrush = CreateFrozen(Color.FromRgb(160, 160, 160));

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is true ? FavoriteBrush : DefaultBrush;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();

        private static SolidColorBrush CreateFrozen(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
