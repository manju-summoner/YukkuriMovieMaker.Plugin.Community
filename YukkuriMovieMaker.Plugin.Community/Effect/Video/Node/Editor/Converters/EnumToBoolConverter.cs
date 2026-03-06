using System.Globalization;
using System.Windows.Data;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Converters;

public class EnumToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false;

        var enumValue = value.ToString();
        var targetValue = parameter.ToString();

        return enumValue?.Equals(targetValue, StringComparison.OrdinalIgnoreCase) ?? false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true && parameter != null) return Enum.Parse(targetType, parameter.ToString()!);

        return Binding.DoNothing;
    }
}