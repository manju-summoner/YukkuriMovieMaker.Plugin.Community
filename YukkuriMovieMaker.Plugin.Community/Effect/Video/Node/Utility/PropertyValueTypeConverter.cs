using System.Windows.Media;
using Newtonsoft.Json.Linq;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Utility;

public static class PropertyValueTypeConverter
{
    public static object? ConvertPropertyValue(Type targetType, object? value)
    {
        if (value == null)
            return null;

        if (targetType.IsInstanceOfType(value))
            return value;

        if (targetType.IsEnum)
            return Enum.ToObject(targetType, value);

        if (targetType == typeof(Color) &&
            value is string colorString)
            return ColorConverter.ConvertFromString(colorString);

        return Convert.ChangeType(value, targetType);
    }

    public static object? ConvertRestoredValue(Type targetType, object? rawValue)
    {
        if (rawValue is null) return null;

        if (rawValue is JToken token)
            return token.ToObject(targetType);

        if (targetType.IsInstanceOfType(rawValue))
            return rawValue;

        return ConvertPropertyValue(targetType, rawValue);
    }
}