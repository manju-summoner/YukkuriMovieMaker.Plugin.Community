using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Voice;

namespace YukkuriMovieMaker.Plugin.Community.Voice.LivetoonTTS
{
    internal class LivetoonTTSVoiceParameter : VoiceParameterBase
    {
        double speed = 100;
        [Display(Name = nameof(Texts.Speed), Description = nameof(Texts.Speed), ResourceType = typeof(Texts))]
        [TextBoxSlider("F1", "%", 20, 300)]
        [Range(20d, 300d)]//
        [DefaultValue(100d)]
        public double Speed { get=> speed; set => Set(ref speed, value); }
    }
}