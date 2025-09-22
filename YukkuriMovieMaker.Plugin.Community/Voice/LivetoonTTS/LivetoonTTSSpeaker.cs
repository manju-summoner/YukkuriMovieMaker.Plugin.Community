using YukkuriMovieMaker.Plugin.Voice;

namespace YukkuriMovieMaker.Plugin.Community.Voice.LivetoonTTS
{
    internal class LivetoonTTSSpeaker(string voice) : IVoiceSpeaker
    {
        public string EngineName => "Livetoon TTS";

        public string SpeakerName => voice;

        public string API => "LivetoonAPI";

        public string ID => voice;

        public bool IsVoiceDataCachingRequired => true;

        public SupportedTextFormat Format => SupportedTextFormat.Text;

        public IVoiceLicense? License => null;

        public IVoiceResource? Resource => null;

        public Task<string> ConvertKanjiToYomiAsync(string text, IVoiceParameter voiceParameter)
        {
            throw new NotImplementedException();
        }

        public async Task<IVoicePronounce?> CreateVoiceAsync(string text, IVoicePronounce? pronounce, IVoiceParameter? parameter, string filePath)
        {
            if (parameter is not LivetoonTTSVoiceParameter p)
                return null;

            var api = new LivetoonTTSAPI(LivetoonTTSSettings.Default.APIKey);
            await api.SynthesizeAsync(text, voice, p.Speed / 100d, filePath);
            return null;
        }

        public IVoiceParameter CreateVoiceParameter()
        {
            return new LivetoonTTSVoiceParameter();
        }

        public bool IsMatch(string api, string id)
        {
            return api == API && id == ID;
        }

        public IVoiceParameter MigrateParameter(IVoiceParameter currentParameter)
        {
            if (currentParameter is not LivetoonTTSVoiceParameter)
                return CreateVoiceParameter();
            return currentParameter;
        }
    }
}