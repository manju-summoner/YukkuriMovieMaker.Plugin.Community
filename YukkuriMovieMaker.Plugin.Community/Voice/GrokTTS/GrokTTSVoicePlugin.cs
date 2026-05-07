using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YukkuriMovieMaker.Plugin.Voice;

namespace YukkuriMovieMaker.Plugin.Community.Voice.GrokTTS
{
    internal class GrokTTSVoicePlugin : IVoicePlugin
    {
        public IEnumerable<IVoiceSpeaker> Voices =>
            !string.IsNullOrWhiteSpace(GrokTTSSettings.Default.ApiKey)
                ? GrokTTSVoices.Voices.Select(v => new GrokTTSVoiceSpeaker(v))
                : [];

        public bool CanUpdateVoices => false;

        public bool IsVoicesCached => true;

        public string Name => "Grok TTS";

        public Task UpdateVoicesAsync() => Task.CompletedTask;
    }
}
