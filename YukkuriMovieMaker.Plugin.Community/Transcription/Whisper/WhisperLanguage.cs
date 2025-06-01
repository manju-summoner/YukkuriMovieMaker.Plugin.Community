using System.Globalization;
using Whisper.net;

namespace YukkuriMovieMaker.Plugin.Community.Transcription.Whisper
{
    internal readonly record struct WhisperLanguage
    {
        public static WhisperLanguage Auto { get; } = new("auto");
        
        public string Code { get; }
        public bool IsAuto => this == Auto;

        public WhisperLanguage(string code)
        {
            Code = code.ToLowerInvariant();
        }

        public bool EqualsCode(string code) => Code.Equals(code, StringComparison.OrdinalIgnoreCase);

        static IEnumerable<string> GetSupportedLanguagesSafe() 
        {            
            try
            {
                return WhisperFactory.GetSupportedLanguages();
            }
            catch
            {
                return [];
            }
        }

        public static WhisperLanguage GetSystemLanguageOrAuto()
        {
            try
            {
                var systemLanguage = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
                return GetSupportedLanguages().DefaultIfEmpty(Auto).First(x => x.EqualsCode(systemLanguage));
            }
            catch
            {
                return Auto;
            }
        }
        public static IReadOnlyList<WhisperLanguage> GetSupportedLanguages() => [Auto, .. GetSupportedLanguagesSafe().Select(x => new WhisperLanguage(x))];

    }
}
