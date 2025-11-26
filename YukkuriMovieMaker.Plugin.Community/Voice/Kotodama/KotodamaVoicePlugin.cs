using System;
using System.Collections.Generic;
using System.Text;
using YukkuriMovieMaker.Plugin.Voice;

namespace YukkuriMovieMaker.Plugin.Community.Voice.Kotodama
{
    internal class KotodamaVoicePlugin : IVoicePlugin
    {
        public IEnumerable<IVoiceSpeaker> Voices => string.IsNullOrEmpty(KotodamaSettings.Default.ApiKey) ? [] : KotodamaSettings.Default.Speakers.Where(x => !string.IsNullOrEmpty(x.SpeakerId)).Select(x => new KotodamaVoiceSpeaker(x));

        public bool CanUpdateVoices => false;

        public bool IsVoicesCached => false;

        public string Name => "Kotodama";

        public Task UpdateVoicesAsync()
        {
            return Task.CompletedTask;
        }
    }
}
