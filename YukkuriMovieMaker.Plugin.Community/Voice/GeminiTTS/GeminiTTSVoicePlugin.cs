using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YukkuriMovieMaker.Plugin.Voice;
using YukkuriMovieMaker.UndoRedo;

namespace YukkuriMovieMaker.Plugin.Community.Voice.GeminiTTS
{
    internal class GeminiTTSVoicePlugin : IVoicePlugin
    {
        public IEnumerable<IVoiceSpeaker> Voices => !string.IsNullOrWhiteSpace(GeminiTTSSettings.Default.ApiKey) ? GeminiTTSVoices.Voices.Select(v => new GeminiTTSVoiceSpeaker(v)) : [];

        public bool CanUpdateVoices => false;

        public bool IsVoicesCached => true;

        public string Name => "GeminiTTS";

        public Task UpdateVoicesAsync()
        {
            throw new NotImplementedException();
        }
    }
}
