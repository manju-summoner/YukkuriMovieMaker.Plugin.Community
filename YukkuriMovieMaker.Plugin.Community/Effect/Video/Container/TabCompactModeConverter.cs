using System.Globalization;
using System.Windows.Data;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

internal sealed class TabCompactModeConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2)
            return false;

        if (!TryReadCount(values[0], out var tabCount) || tabCount <= 0)
            return false;

        if (!TryReadDouble(values[1], out var availableWidth) || availableWidth <= 0)
            return false;

        var normalTabWidth = 120d;
        if (parameter is not null && double.TryParse(parameter.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
            normalTabWidth = parsed;

        var requiredWidth = tabCount * normalTabWidth;
        return requiredWidth >= availableWidth;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        [];

    private static bool TryReadCount(object value, out int count)
    {
        count = 0;

        if (value is int i)
        {
            count = i;
            return true;
        }

        return int.TryParse(value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out count);
    }

    private static bool TryReadDouble(object value, out double number)
    {
        number = 0;
        if (value is double d)
        {
            number = d;
            return true;
        }

        return double.TryParse(value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number);
    }
}