using System;
using System.Reflection;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services
{
    internal static class TimelineMemberReader
    {
        public static int GetCurrentFrame(object timeline)
        {
            return GetIntMember(timeline, "CurrentFrame");
        }

        public static int GetIntMember(object instance, string name)
        {
            if (TryGetIntProperty(instance, name, out var value))
                return value;
            return int.MinValue;
        }

        public static string? GetStringProperty(object instance, string propertyName)
        {
            var property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property is not null && property.GetIndexParameters().Length > 0)
                return null;
            if (property?.GetValue(instance) is string value)
                return value;

            return null;
        }

        public static bool TryGetIntProperty(object instance, string propertyName, out int value)
        {
            value = 0;
            var property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property is not null)
            {
                if (property.GetIndexParameters().Length > 0)
                    return false;
                var raw = property.GetValue(instance);
                if (TryConvertToInt(raw, out value))
                    return true;
            }

            var field = instance.GetType().GetField(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field is not null)
            {
                var raw = field.GetValue(instance);
                if (TryConvertToInt(raw, out value))
                    return true;
            }

            return false;
        }

        public static bool TryConvertToInt(object? raw, out int value)
        {
            value = 0;
            if (raw is null)
                return false;

            switch (raw)
            {
                case int i:
                    value = i;
                    return true;
                case long l:
                    value = (int)l;
                    return true;
                case short s:
                    value = s;
                    return true;
                case byte b:
                    value = b;
                    return true;
                case string str when int.TryParse(str, out var parsed):
                    value = parsed;
                    return true;
                default:
                    return false;
            }
        }
    }
}
