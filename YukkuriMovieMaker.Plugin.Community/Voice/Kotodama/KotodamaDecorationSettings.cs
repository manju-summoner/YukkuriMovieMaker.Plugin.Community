using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Voice.Kotodama.API;

namespace YukkuriMovieMaker.Plugin.Community.Voice.Kotodama
{
    class KotodamaDecorationSettings : Bindable
    {
        public string? Name { get => field; set => Set(ref field, value); }
        public string? DecorationId { get => field; set => Set(ref field, value); }
        public KotodamaDecorationSettings()
        {

        }
        public KotodamaDecorationSettings(KotodamaDefaultDecoration decoration)
        {
            Name = decoration.Name;
            DecorationId = decoration.Id;
        }
    }
}
