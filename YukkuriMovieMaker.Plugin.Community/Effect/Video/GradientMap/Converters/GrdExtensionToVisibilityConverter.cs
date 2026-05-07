using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Converters;

/// <summary>
/// ファイルパス文字列の拡張子が .grd の場合のみ <see cref="Visibility.Visible"/> を返す。
/// それ以外は <see cref="Visibility.Collapsed"/>。
/// </summary>
public sealed class GrdExtensionToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrEmpty(path))
            return Visibility.Collapsed;
        return string.Equals(Path.GetExtension(path), ".grd", StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
