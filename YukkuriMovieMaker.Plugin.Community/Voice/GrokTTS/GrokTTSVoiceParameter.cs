using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Voice;

namespace YukkuriMovieMaker.Plugin.Community.Voice.GrokTTS
{
    internal class GrokTTSVoiceParameter : VoiceParameterBase
    {
        string language = "auto";

        [Display(Name = nameof(Texts.Language), Description = nameof(Texts.LanguageDesc), ResourceType = typeof(Texts))]
        [TextEditor]
        public string Language { get => language; set => Set(ref language, value); }
    }
}
