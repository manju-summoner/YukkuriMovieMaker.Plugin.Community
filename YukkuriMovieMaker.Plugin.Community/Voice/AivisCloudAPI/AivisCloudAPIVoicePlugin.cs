using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YukkuriMovieMaker.Plugin.Voice;

namespace YukkuriMovieMaker.Plugin.Community.Voice.AivisCloudAPI
{
    internal class AivisCloudAPIVoicePlugin : IVoicePlugin
    {
        public IEnumerable<IVoiceSpeaker> Voices => string.IsNullOrWhiteSpace(AivisCloudAPISettings.Default.ApiKey) ? [] : AivisCloudAPISettings.Default.Models.SelectMany(m => m.Speakers.Select(s => (s, m)).Select(x => new AivisCloudAPIVoiceSpeaker(x.m.AivmModelUuid, x.s.AivmSpeakerUuid)));

        public bool CanUpdateVoices => false;

        public bool IsVoicesCached => false;

        public string Name => "Aivis Cloud API";

        public Task UpdateVoicesAsync()
        {
            throw new NotImplementedException();
        }
    }
}
