using System.Globalization;
using Whisper.net;
using YukkuriMovieMaker.Plugin.Community.Transcription.Whisper.Installers;

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

        static List<string> GetSupportedLanguagesSafe() 
        {
            //環境によってはGetSupportedLanguagesに失敗して文字起こしツール自体が開けなくなる（？）みたいなので、固定値を返す。
            //そうそう頻繁に変わるような物でもなく、既に列挙されている言語でほとんどのケースをカバー出来るため、問題にならないはず。
            return [
                "en","zh","de","es","ru","ko","fr","ja","pt","tr",
                "pl","ca","nl","ar","sv","it","id","hi","fi","vi",
                "he","uk","el","ms","cs","ro","da","hu","ta","no",
                "th","ur","hr","bg","lt","la","mi","ml","cy","sk",
                "te","fa","lv","bn","sr","az","sl","kn","et","mk",
                "br","eu","is","hy","ne","mn","bs","kk","sq","sw",
                "gl","mr","pa","si","km","sn","yo","so","af","oc",
                "ka","be","tg","sd","gu","am","yi","lo","uz","fo",
                "ht","ps","tk","nn","mt","sa","lb","my","bo","tl",
                "mg","as","tt","haw","ln","ha","ba","jw","su",];
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
