using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Converters;

public sealed class ColorStringConverter : IValueConverter
{
    public object Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        return value is Color color
            ? ToString(color)
            : string.Empty;
    }

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        return value is string text
            ? ToColor(text)
            : Colors.Transparent;
    }

    public static string ToString(Color color)
    {
        return color.ToString();
    }

    public static Color ToColor(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Colors.Transparent;

        try
        {
            return (Color)ColorConverter.ConvertFromString(text.Trim());
        }
        catch
        {
            return Colors.Transparent;
        }
    }
}