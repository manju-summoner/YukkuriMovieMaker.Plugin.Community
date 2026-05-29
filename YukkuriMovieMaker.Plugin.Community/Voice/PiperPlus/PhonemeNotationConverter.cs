using System.Text.RegularExpressions;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus;

internal static partial class PhonemeNotationConverter
{
    [GeneratedRegex(@"<<\s*(.*?)\s*>>", RegexOptions.Singleline)]
    private static partial Regex PhonemePattern();

    public static string Convert(string text)
        => PhonemePattern().Replace(text, m => $"[[ {m.Groups[1].Value.Trim()} ]]");
}
