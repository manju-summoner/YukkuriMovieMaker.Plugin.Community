using System.Collections.Immutable;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Voice.Kotodama.API;

namespace YukkuriMovieMaker.Plugin.Community.Voice.Kotodama
{
    class KotodamaSpeakerSettings : Bindable
    {
        public string? Name { get => field; set => Set(ref field, value); }
        public string? SpeakerId { get => field; set => Set(ref field, value); }
        public ImmutableList<KotodamaDecorationSettings> Decorations { get => field; set => Set(ref field, value); } = [];
        public string? ContentRestrictions { get => field; set => Set(ref field, value); }
        public KotodamaSpeakerSettings()
        {
        }
        public KotodamaSpeakerSettings(KotodamaDefaultSpeaker speaker)
        {
            Name = speaker.Name;
            SpeakerId = speaker.Id;
            Decorations = [.. speaker.Decorations.Select(x => new KotodamaDecorationSettings(x))];
            ContentRestrictions = speaker.ContentRestrictions;
        }
    }
}
