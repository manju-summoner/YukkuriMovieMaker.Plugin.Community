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
        const int rateLimitPerMinute = 10;
        static readonly SemaphoreSlim semaphore = new SemaphoreSlim(1);
        static bool isMonthlyBilling = true;
        static DateTime nextAllowedRequestAt = DateTime.MinValue;

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

            var shouldReleaseSemaphore = isMonthlyBilling;
            if (shouldReleaseSemaphore)
            {
                //月額課金制の場合のみ、同時接続数を1に制限する。
                //従量課金制の場合、初回音声合成時に従量課金制であることが確認された後（isMonthlyBillingにfalseが代入された後）、同時接続数の制限が解除される。
                await semaphore.WaitAsync();
            }
            try
            {
                var delay = nextAllowedRequestAt - DateTime.Now;
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay);

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
                isMonthlyBilling = await api.SynthesizeAsync(param, filePath);
                if (isMonthlyBilling)
                    nextAllowedRequestAt = DateTime.Now.AddMinutes(1d / rateLimitPerMinute);
            }
            finally
            {
                if(shouldReleaseSemaphore)
                    semaphore.Release();
            }
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
