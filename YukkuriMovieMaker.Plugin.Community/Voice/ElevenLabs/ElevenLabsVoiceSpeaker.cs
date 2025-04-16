using YukkuriMovieMaker.Plugin.Voice;

namespace YukkuriMovieMaker.Plugin.Community.Voice.ElevenLabs
{
    internal class ElevenLabsVoiceSpeaker(ElevenLabsVoice voice) : IVoiceSpeaker
    {
        static readonly SemaphoreSlim semaphore = new(2);
        readonly ElevenLabsVoice voice = voice;

        public string EngineName => "ElevenLabs";

        public string SpeakerName => voice.Name ?? string.Empty;

        public string API => "ElevenLabs";

        public string ID => voice.Id ?? string.Empty;

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
            if (parameter is not ElevenLabsVoiceParameter elevenLabsParameter || string.IsNullOrEmpty(voice.Id))
                return null;

            await semaphore.WaitAsync();
            try
            {
                var api = new ElevenLabsAPI();
                var model = string.IsNullOrWhiteSpace(ElevenLabsSettings.Default.TTSModel) ? ElevenLabsTTSModels.Models.First() : ElevenLabsSettings.Default.TTSModel;
                await api.TextToSpeechAsync(model, voice.Id, text, elevenLabsParameter, filePath);
            }
            finally
            {
                semaphore.Release();
            }
            return null;
        }

        public IVoiceParameter CreateVoiceParameter()
        {
            return new ElevenLabsVoiceParameter();
        }

        public bool IsMatch(string api, string id)
        {
            return api == API && id == ID;
        }

        public IVoiceParameter MigrateParameter(IVoiceParameter currentParameter)
        {
            if(currentParameter is not ElevenLabsVoiceParameter)
                currentParameter = CreateVoiceParameter();
            return currentParameter;
        }
    }
}