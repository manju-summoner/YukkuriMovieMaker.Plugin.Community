using YukkuriMovieMaker.Plugin.Voice;
using Windows.Media.SpeechSynthesis;

namespace YukkuriMovieMaker.Plugin.Community.Voice.WinRTTTS
{
    internal class WinRTTTSVoicePlugin : IVoicePlugin
    {
        public string Name => "WinRT TTS";

        public IEnumerable<IVoiceSpeaker> Voices =>
            SpeechSynthesizer.AllVoices.Select(v => new WinRTTTSVoiceSpeaker(v));

        public bool CanUpdateVoices => false;

        public bool IsVoicesCached => true;

        public Task UpdateVoicesAsync() => throw new NotImplementedException();
    }
}
