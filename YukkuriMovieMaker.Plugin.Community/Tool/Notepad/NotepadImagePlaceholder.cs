using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Notepad
{
    internal static partial class NotepadImagePlaceholder
    {
        [GeneratedRegex(@"\ufffc\[image:([0-9a-fA-F]{1,128})\]")]
        private static partial Regex PlaceholderRegex();

        public static Regex Pattern => PlaceholderRegex();

        public static IEnumerable<(int Index, int Length, string Id)> EnumeratePlaceholders(string text)
        {
            if (string.IsNullOrEmpty(text))
                yield break;
            foreach (Match match in PlaceholderRegex().Matches(text))
            {
                yield return (match.Index, match.Length, match.Groups[1].Value.ToLowerInvariant());
            }
        }

        public static IReadOnlyList<string> CollectImageIds(string text)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in EnumeratePlaceholders(text))
                ids.Add(item.Id);
            return [.. ids];
        }
    }
}
