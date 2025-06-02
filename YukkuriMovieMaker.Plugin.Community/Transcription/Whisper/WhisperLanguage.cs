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
            // Whisperの実行可能なランタイムが存在しない場合、空を返して抜ける。
            // WhisperFactory.GetSupportedLanguages()呼び出しによるライブラリの読み込みを回避し、WhisperFactory.libraryLoadedが読み込み失敗状態で固定されるのを防ぐ。
            if (!CUDAToolkit.IsInstalled() && !VulkanRuntime.IsInstalled() && !VisualCppRuntime.IsInstalled())
                return [];
            try
            {
                // 実行可能なランタイムが存在しない場合、WhisperFactory.GetSupportedLanguages()で返されるIEnumerableの列挙中に例外が発生する。
                // IEnumerableをそのまま返すと、この関数を抜けた後の列挙中に例外が発生する。そのため、ここでListに変換して返す。
                return [.. WhisperFactory.GetSupportedLanguages()];
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
