using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YukkuriMovieMaker.Plugin.Voice;

namespace YukkuriMovieMaker.Plugin.Community.Voice.LivetoonTTS
{
    internal class LivetoonTTSVoicePlugin : IVoicePlugin
    {
        public IEnumerable<IVoiceSpeaker> Voices => 
            string.IsNullOrEmpty(LivetoonTTSSettings.Default.APIKey) ? [] :
            [
                new LivetoonTTSSpeaker("default"),
                new LivetoonTTSSpeaker("char1"),
                new LivetoonTTSSpeaker("char2"),
            ];

        public bool CanUpdateVoices => false;

        public bool IsVoicesCached => false;

        public string Name => "Livetoon TTS";

        public Task UpdateVoicesAsync()
        {
            throw new NotImplementedException();
        }
    }
}
