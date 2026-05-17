using System.IO;
using Windows.Media.SpeechSynthesis;
using YukkuriMovieMaker.Plugin.Voice;

namespace YukkuriMovieMaker.Plugin.Community.Voice.WinRTTTS
{
    internal class WinRTTTSVoiceSpeaker(VoiceInformation voiceInfo) : IVoiceSpeaker
    {
        static readonly SemaphoreSlim semaphore = new(1);

        public string EngineName => API;
        public string SpeakerName => voiceInfo.DisplayName;
        public string API => "WinRTTTS";
        public string ID => voiceInfo.Id;
        public bool IsVoiceDataCachingRequired => false;
        public SupportedTextFormat Format => SupportedTextFormat.Text;
        public IVoiceLicense? License => null;
        public IVoiceResource? Resource => null;

        public bool IsMatch(string api, string id) => api == API && id == ID;

        public IVoiceParameter CreateVoiceParameter() => new WinRTTTSVoiceParameter();

        public IVoiceParameter MigrateParameter(IVoiceParameter currentParameter)
        {
            if (currentParameter is not WinRTTTSVoiceParameter)
                return CreateVoiceParameter();
            return currentParameter;
        }

        public Task<string> ConvertKanjiToYomiAsync(string text, IVoiceParameter voiceParameter)
            => throw new NotImplementedException();

        public async Task<IVoicePronounce?> CreateVoiceAsync(
            string text,
            IVoicePronounce? pronounce,
            IVoiceParameter? parameter,
            string filePath)
        {
            var param = parameter as WinRTTTSVoiceParameter ?? (WinRTTTSVoiceParameter)CreateVoiceParameter();

            await semaphore.WaitAsync();
            try
            {
                using var synthesizer = new SpeechSynthesizer();
                synthesizer.Voice = voiceInfo;
                synthesizer.Options.SpeakingRate = param.SpeakingRate;
                synthesizer.Options.AudioVolume = param.Volume;

                if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 16299))
                {
                    synthesizer.Options.AudioPitch = param.Pitch;
                }

                if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17134))
                {
                    synthesizer.Options.AppendedSilence = param.AppendedSilence;
                    synthesizer.Options.PunctuationSilence = param.PunctuationSilence;
                }

                using var stream = await synthesizer.SynthesizeTextToStreamAsync(text);
                using var fileStream = File.Create(filePath);
                using var reader = stream.AsStreamForRead();
                await reader.CopyToAsync(fileStream);
            }
            finally
            {
                semaphore.Release();
            }

            return null;
        }
    }
}
