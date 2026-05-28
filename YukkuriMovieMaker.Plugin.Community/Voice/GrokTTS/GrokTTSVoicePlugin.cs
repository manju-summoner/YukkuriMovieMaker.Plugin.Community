using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YukkuriMovieMaker.Plugin.Voice;

namespace YukkuriMovieMaker.Plugin.Community.Voice.GrokTTS
{
    internal class GrokTTSVoicePlugin : IVoicePlugin
    {
        public IEnumerable<IVoiceSpeaker> Voices
        {
            get
            {
                if (string.IsNullOrWhiteSpace(GrokTTSSettings.Default.ApiKey))
                    return [];

                var seen = new HashSet<string>();
                var result = new List<IVoiceSpeaker>();

                foreach (var v in GrokTTSVoices.Defaults)
                {
                    if (string.IsNullOrWhiteSpace(v.Id) || !seen.Add(v.Id))
                        continue;
                    result.Add(new GrokTTSVoiceSpeaker(v));
                }
                foreach (var v in GrokTTSSettings.Default.Voices)
                {
                    if (string.IsNullOrWhiteSpace(v.Id) || !seen.Add(v.Id))
                        continue;
                    result.Add(new GrokTTSVoiceSpeaker(v));
                }
                return result;
            }
        }

        public bool CanUpdateVoices => false;

        public bool IsVoicesCached => true;

        public string Name => "Grok TTS";

        public Task UpdateVoicesAsync() => Task.CompletedTask;
    }
}
