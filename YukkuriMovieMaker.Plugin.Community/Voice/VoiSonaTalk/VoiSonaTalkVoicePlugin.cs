using System;
using System.Collections.Generic;
using System.Text;
using YukkuriMovieMaker.Plugin.Voice;

namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk
{
    internal class VoiSonaTalkVoicePlugin : IVoicePlugin
    {
        public IEnumerable<IVoiceSpeaker> Voices => !string.IsNullOrEmpty(VoiSonaTalkSettings.Default.UserName) && !string.IsNullOrEmpty(VoiSonaTalkSettings.Default.Password) ? VoiSonaTalkSettings.Default.Voices.Select(x => new VoiSonaTalkVoiceSpeaker(x.VoiceName)) : [];

        public bool CanUpdateVoices => true;

        public bool IsVoicesCached => VoiSonaTalkSettings.Default.IsVoicesCached;

        public string Name => "VoiSona Talk";

        public async Task UpdateVoicesAsync()
        {
            await VoiSonaTalkAPIHelper.UpdateVoicesAsync();
        }
    }
}
