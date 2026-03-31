using System.Globalization;
using System.Windows.Data;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.ValueTypes;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Converters;

public class EnumTypeToStringList : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is not Type { IsEnum: true } type
            ? []
            : type.GetFields()
                .Select(field => field.GetCustomAttributes(typeof(EnumDisplayAttribute), false))
                .Where(attributes => attributes.Length != 0)
                .Select(attributes => ((EnumDisplayAttribute)attributes[0]).Name)
                .ToList();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}