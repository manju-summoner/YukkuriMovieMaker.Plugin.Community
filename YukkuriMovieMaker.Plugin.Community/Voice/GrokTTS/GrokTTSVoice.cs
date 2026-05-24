using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.GrokTTS
{
    internal class GrokTTSVoice : Bindable
    {
        string? name, id;
        public string? Name { get => name; set => Set(ref name, value); }
        public string? Id { get => id; set => Set(ref id, value); }

        public GrokTTSVoice() { }
        public GrokTTSVoice(string name, string id)
        {
            Name = name;
            Id = id;
        }
    }
}
