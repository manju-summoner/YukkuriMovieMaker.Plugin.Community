using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Converters;

public class ColorToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string colorName)
            return false;

        var color = (Color)(colorName.StartsWith('#')
            ? ColorConverter.ConvertFromString(colorName)
            : typeof(Colors).GetProperty(colorName)?.GetValue(null) ??
              throw new InvalidOperationException($"Unknown color: {colorName}"));

        return new SolidColorBrush(color);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null)
            return false;
        return value is not SolidColorBrush brush
            ? nameof(Colors.SlateGray)
            : $"#{brush.Color.A:x2}{brush.Color.R:x2}{brush.Color.G:x2}{brush.Color.B:x2}";
    }
}