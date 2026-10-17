using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.ElevenLabs
{
    internal class ElevenLabsVoice : Bindable
    {
        string? name, id;
        public string? Name { get => name; set => Set(ref name, value); }
        public string? Id { get => id; set => Set(ref id, value); }

        public ElevenLabsVoice() { }
        public ElevenLabsVoice(string name, string id)
        {
            Name = name;
            Id = id;
        }
    }
}
