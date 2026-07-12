using System.Collections.Concurrent;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    internal static class Vst3EditorProbe
    {
        static readonly ConcurrentDictionary<string, bool> cache = new(StringComparer.OrdinalIgnoreCase);

        public static bool GetHasEditor(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;
            return !cache.TryGetValue(path, out var hasEditor) || hasEditor;
        }

        public static void SetHasEditor(string path, bool hasEditor)
        {
            cache[path] = hasEditor;
        }
    }
}
