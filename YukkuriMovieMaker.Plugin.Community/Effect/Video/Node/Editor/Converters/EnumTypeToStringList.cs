using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Windows.Data;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Converters;

public sealed record EnumPortItem(int Value, string Label);

public class EnumTypeToStringList : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Type { IsEnum: true } type)
            return new List<EnumPortItem>();

        return type.GetFields()
            .Where(field => field.IsStatic)
            .Select(field => (Field: field, Attributes: field.GetCustomAttributes(typeof(DisplayAttribute), false)))
            .Where(x => x.Attributes.Length != 0)
            .Select(x => new EnumPortItem(
                System.Convert.ToInt32(x.Field.GetRawConstantValue()),
                ((DisplayAttribute)x.Attributes[0]).GetName() ?? x.Field.Name))
            .ToList();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}