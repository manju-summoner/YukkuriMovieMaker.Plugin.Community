using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YukkuriMovieMaker.Plugin.Community.Voice.AivisCloudAPI.API;
using YukkuriMovieMaker.Plugin.Voice;

namespace YukkuriMovieMaker.Plugin.Community.Voice.AivisCloudAPI
{
    internal class AivisCloudAPIVoiceSpeaker(string ModelUuid, string SpeakerUuid) : IVoiceSpeaker
    {
        static readonly SemaphoreSlim semaphore = new(1);
        static bool isSubscription = true;
        static DateTime nextAllowedRequestAt = DateTime.MinValue;

        public string EngineName => "Aivis Cloud API";

        public string SpeakerName => 
            AivisCloudAPISettings.Default.Models
            .FirstOrDefault(m => m.AivmModelUuid == ModelUuid)
            ?.Speakers
            .FirstOrDefault(s => s.AivmSpeakerUuid == SpeakerUuid)
            ?.Name
            ?? string.Empty;

        public string API => "AivisCloudAPI";

        public string ID => $"{ModelUuid}:{SpeakerUuid}";

        public bool IsVoiceDataCachingRequired => true;

        public SupportedTextFormat Format => SupportedTextFormat.Text;

        public IVoiceLicense? License => null;

        public IVoiceResource? Resource => null;

        public IEnumerable<StyleContract> Styles =>
            AivisCloudAPISettings.Default
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
            if (parameter is not AivisCloudAPIVoiceParameter ap)
                return null;

            var shouldReleaseSemaphore = isSubscription;
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

                var api = new API.AivisCloudAPI(AivisCloudAPISettings.Default.ApiKey);
                var param = new API.SynthesizeParametersContract(
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
                var response = await api.SynthesizeAsync(param, filePath);
                isSubscription = response.BillingMode is BillingMode.Subscription;
                if (isSubscription && response.Subscription != null)
                {
                    // 月額課金制の場合、レート制限を考慮して次のリクエスト可能時間を設定する。
                    // 一気に残り回数を使い切ってしまうと最大1分程度の待機時間が発生してしまうため、
                    // リクエスト間隔を「リセットまでの残り時間/残り回数」分開け、リクエスト間隔を平準化する。
                    nextAllowedRequestAt = DateTime.Now + response.Subscription.RequestsReset / Math.Max(1, response.Subscription.RequestsRemaining);
                }
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
            return new AivisCloudAPIVoiceParameter()
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
            if (currentParameter is not AivisCloudAPIVoiceParameter aivisSpeechParameter)
                return CreateVoiceParameter();

            var styleIds = Styles.Select(x => x.LocalId).ToList();
            if (!styleIds.Contains(aivisSpeechParameter.Style))
                aivisSpeechParameter.Style = styleIds.FirstOrDefault();
            return aivisSpeechParameter;
        }
    }
}
