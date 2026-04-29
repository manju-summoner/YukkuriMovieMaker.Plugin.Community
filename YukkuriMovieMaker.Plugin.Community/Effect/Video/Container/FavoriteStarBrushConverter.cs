using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

internal sealed class FavoriteStarBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush FavoriteBrush;

    static FavoriteStarBrushConverter()
    {
        FavoriteBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00));
        FavoriteBrush.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? FavoriteBrush : SystemColors.GrayTextBrush;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}