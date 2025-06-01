using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Voice;

namespace YukkuriMovieMaker.Plugin.Community.Voice.GeminiTTS
{
    internal class GeminiTTSVoiceParameter : VoiceParameterBase
    {
        string prompt = string.Empty;

        [Display(Name = nameof(Texts.Prompt), Description = nameof(Texts.Prompt), ResourceType = typeof(Texts))]
        [TextEditor]
        public string Prompt { get => prompt; set => Set(ref prompt, value); }
    }
}
