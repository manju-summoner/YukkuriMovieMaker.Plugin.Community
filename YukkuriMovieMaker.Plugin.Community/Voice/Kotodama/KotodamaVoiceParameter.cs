using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Voice;

namespace YukkuriMovieMaker.Plugin.Community.Voice.Kotodama
{
    internal class KotodamaVoiceParameter : VoiceParameterBase
    {
        [Display(Name = nameof(Texts.Decoration), ResourceType = typeof(Texts))]
        [KotodamaDecorationIdComboBox]
        public string? DecorationId { get => field; set => Set(ref field, value); }
    }
}