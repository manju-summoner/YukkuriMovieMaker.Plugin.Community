using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YukkuriMovieMaker.Plugin.Community.Voice.AivisSpeechCloud.API;
using YukkuriMovieMaker.Plugin.Voice;

namespace YukkuriMovieMaker.Plugin.Community.Voice.AivisSpeechCloud
{
    internal class AivisSpeechCloudVoiceSpeaker(string ModelUuid, string SpeakerUuid) : IVoiceSpeaker
    {
        public string EngineName => "AivisSpeech Cloud";

        public string SpeakerName => 
            AivisSpeechCloudSettings.Default.Models
            .FirstOrDefault(m => m.AivmModelUuid == ModelUuid)
            ?.Speakers
            .FirstOrDefault(s => s.AivmSpeakerUuid == SpeakerUuid)
            ?.Name
            ?? string.Empty;

        public string API => "AivisSpeechCloud";

        public string ID => $"{ModelUuid}:{SpeakerUuid}";

        public bool IsVoiceDataCachingRequired => true;

        public SupportedTextFormat Format => SupportedTextFormat.Text;

        public IVoiceLicense? License => null;

        public IVoiceResource? Resource => null;

        public IEnumerable<AivisSpeechCloudAPIStyle> Styles =>
            AivisSpeechCloudSettings.Default
            .Models
            .FirstOrDefault(m => m.AivmModelUuid == ModelUuid)
            ?.Speakers
            .FirstOrDefault(s => s.AivmSpeakerUuid == SpeakerUuid)
            ?.Styles ?? [];

        public Task<string> ConvertKanjiToYomiAsync(string text, IVoiceParameter voiceParameter)
        {
            throw new NotImplementedException();
        }

        public async Task<IVoicePronounce?> CreateVoiceAsync(string text, IVoicePronounce? pronounce, IVoiceParameter? parameter, string filePath)
        {
            if (parameter is not AivisSpeechCloudVoiceParameter ap)
                return null;

            var api = new API.AivisSpeechCloudAPI(AivisSpeechCloudSettings.Default.ApiKey);
            var param = new API.AivisSpeechCloudAPISynthesisParameters(
                ModelUuid,
                SpeakerUuid,
                ap.Style,
                null,
                null,
                text,
                true,
                null,
                ap.Speed / 100,
                ap.EmotionalIntensity / 100,
                ap.TempoDynamics / 100,
                ap.Pitch / 100,
                1,
                ap.LeadingSilenceSeconds,
                ap.TrailingSilenceSeconds,
                ap.LineBreakSilenceSeconds,
                null,
                null,
                null,
                null
                );
            await api.SynthesizeAsync(param, filePath);
            return null;
        }

        public IVoiceParameter CreateVoiceParameter()
        {
            return new AivisSpeechCloudVoiceParameter()
            {
                Style = Styles.Select(s => s.LocalId).FirstOrDefault()
            };
        }

        public bool IsMatch(string api, string id)
        {
            return api == API && id == ID;
        }

        public IVoiceParameter MigrateParameter(IVoiceParameter currentParameter)
        {
            if (currentParameter is not AivisSpeechCloudVoiceParameter aivisSpeechParameter)
                return CreateVoiceParameter();

            var styleIds = Styles.Select(x => x.LocalId).ToList();
            if (!styleIds.Contains(aivisSpeechParameter.Style))
                aivisSpeechParameter.Style = styleIds.FirstOrDefault();
            return aivisSpeechParameter;
        }
    }
}
