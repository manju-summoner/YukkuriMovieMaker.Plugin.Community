using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Windows.Data;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Equalizer2
{
    public sealed class EnumToDisplayNameConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return string.Empty;
            var strValue = value.ToString();
            if (string.IsNullOrEmpty(strValue)) return string.Empty;

            var fieldInfo = value.GetType().GetField(strValue);
            if (fieldInfo == null) return strValue;
            
            var displayAttribute = fieldInfo.GetCustomAttribute<DisplayAttribute>();
            return displayAttribute?.GetName() ?? strValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
