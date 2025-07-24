using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YukkuriMovieMaker.Plugin.Voice;

namespace YukkuriMovieMaker.Plugin.Community.Voice.AivisSpeechCloud
{
    internal class AivisSpeechCloudVoicePlugin : IVoicePlugin
    {
        public IEnumerable<IVoiceSpeaker> Voices => string.IsNullOrWhiteSpace(AivisSpeechCloudSettings.Default.ApiKey) ? [] : AivisSpeechCloudSettings.Default.Models.SelectMany(m => m.Speakers.Select(s => (s, m)).Select(x => new AivisSpeechCloudVoiceSpeaker(x.m.AivmModelUuid, x.s.AivmSpeakerUuid)));

        public bool CanUpdateVoices => false;

        public bool IsVoicesCached => false;

        public string Name => "AivisSpeech Cloud";

        public Task UpdateVoicesAsync()
        {
            throw new NotImplementedException();
        }
    }
}
