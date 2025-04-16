using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YukkuriMovieMaker.Plugin.Voice;

namespace YukkuriMovieMaker.Plugin.Community.Voice.ElevenLabs
{
    internal class ElevenLabsVoicePlugin : IVoicePlugin
    {
        public IEnumerable<IVoiceSpeaker> Voices => 
            string.IsNullOrEmpty(ElevenLabsSettings.Default.APIKey)
            ?[] 
            : ElevenLabsSettings.Default.Voices
                .Where(x => !string.IsNullOrEmpty(x.Id))
                .Select(voice => new ElevenLabsVoiceSpeaker(voice));

        public bool CanUpdateVoices => false;

        public bool IsVoicesCached => false;

        public string Name => "ElevenLabs";

        public Task UpdateVoicesAsync()
        {
            return Task.CompletedTask;
        }
    }
}
