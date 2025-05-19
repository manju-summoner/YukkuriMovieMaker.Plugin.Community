using Whisper.net;

namespace YukkuriMovieMaker.Plugin.Community.Transcription.Whisper
{
    internal readonly record struct WhisperLanguage
    {
        public static WhisperLanguage Auto { get; } = new("auto");
        public static IReadOnlyList<WhisperLanguage> SupportedLanguages { get; } = [Auto, .. WhisperFactory.GetSupportedLanguages().Select(x => new WhisperLanguage(x))];

        public string Code { get; }
        public bool IsAuto => this == Auto;

        public WhisperLanguage(string code)
        {
            Code = code.ToLowerInvariant();
        }

        public bool EqualsCode(string code) => Code.Equals(code, StringComparison.OrdinalIgnoreCase);

        public static WhisperLanguage GetSystemLanguageOrAuto()
        {
            var systemLanguage = System.Globalization.CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
            return SupportedLanguages.DefaultIfEmpty(Auto).First(x => x.EqualsCode(systemLanguage));
        }
    }
}
