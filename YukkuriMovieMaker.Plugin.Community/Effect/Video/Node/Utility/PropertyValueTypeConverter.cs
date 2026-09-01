using System.Windows.Media;

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
}