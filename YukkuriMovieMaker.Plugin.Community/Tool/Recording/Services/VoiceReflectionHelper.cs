using System;
using System.Reflection;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services
{
    internal static class VoiceReflectionHelper
    {
        public static int ToInt(object? value)
        {
            return value switch
            {
                int i => i,
                long l => l > int.MaxValue ? int.MaxValue : (int)l,
                short s => s,
                byte b => b,
                double d => (int)Math.Round(d, MidpointRounding.AwayFromZero),
                float f => (int)Math.Round(f, MidpointRounding.AwayFromZero),
                decimal m => (int)Math.Round(m, MidpointRounding.AwayFromZero),
                string s when int.TryParse(s, out var parsed) => parsed,
                _ => 0
            };
        }

        public static PropertyInfo? FindProperty(Type type, string name)
        {
            for (var current = type; current is not null; current = current.BaseType)
            {
                var prop = current.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (prop is not null)
                    return prop;
            }
            return null;
        }

        public static FieldInfo? FindField(Type type, string name)
        {
            for (var current = type; current is not null; current = current.BaseType)
            {
                var field = current.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field is not null)
                    return field;
            }
            return null;
        }
    }
}
