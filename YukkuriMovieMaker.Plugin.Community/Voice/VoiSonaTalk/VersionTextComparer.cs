using System.Text.RegularExpressions;

namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk
{
    partial class VersionTextComparer : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            var xParts = ExtractNumberParts(x);
            var yParts = ExtractNumberParts(y);
            for (int i = 0; i < Math.Min(xParts.Length, yParts.Length); i++)
            {
                var cmp = xParts[i].CompareTo(yParts[i]);
                if (cmp != 0)
                    return cmp;
            }
            return xParts.Length.CompareTo(yParts.Length);
        }

        private static int[] ExtractNumberParts(string? input)
        {
            if (string.IsNullOrEmpty(input))
                return [];
            var parts = NumberPartRegex().Matches(input)
                .Cast<Match>()
                .Select(m => m.Value)
                .Select(int.Parse);
            return [.. parts];
        }

        [GeneratedRegex(@"\d+")]
        private static partial Regex NumberPartRegex();
    }
}
