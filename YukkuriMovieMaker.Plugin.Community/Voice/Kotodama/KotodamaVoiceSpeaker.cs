using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using YukkuriMovieMaker.Plugin.Community.Voice.Kotodama.API;
using YukkuriMovieMaker.Plugin.Voice;

namespace YukkuriMovieMaker.Plugin.Community.Voice.Kotodama
{
    internal class KotodamaVoiceSpeaker(KotodamaSpeakerSettings speaker) : IVoiceSpeaker
    {
        public string EngineName => "Kotodama";

        public string SpeakerName => speaker.Name ?? string.Empty;

        public string API => "Kotodama";

        public string ID => speaker.SpeakerId ?? string.Empty;

        public bool IsVoiceDataCachingRequired => true;

        public SupportedTextFormat Format => SupportedTextFormat.Text;

        public IVoiceLicense? License => new CommonVoiceLicense(speaker.ContentRestrictions, VoiceLicenseDisplayLocation.CharacterEditor, "https://spiralai.notion.site/Kotodama-28e7fe6ac35380b7ad84ded491533c37");

        public IVoiceResource? Resource => null;

        public Task<string> ConvertKanjiToYomiAsync(string text, IVoiceParameter voiceParameter)
        {
            throw new NotImplementedException();
        }

        public async Task<IVoicePronounce?> CreateVoiceAsync(string text, IVoicePronounce? pronounce, IVoiceParameter? parameter, string filePath)
        {
            if (parameter is not KotodamaVoiceParameter kotodamaParameter || string.IsNullOrEmpty(ID))
                return null;

            var apiKey = KotodamaSettings.Default.ApiKey;
            if (string.IsNullOrEmpty(apiKey))
                return null;
            var decorationId = kotodamaParameter.DecorationId;
            if (string.IsNullOrEmpty(decorationId))
                return null;
            var api = new KotodamaAPI(apiKey);
            await api.Generate(text, ID, decorationId, filePath);
            return null;
        }

        public IVoiceParameter CreateVoiceParameter()
        {
            return new KotodamaVoiceParameter()
            { 
                DecorationId = speaker.Decorations.FirstOrDefault()?.DecorationId 
            };
        }

        public bool IsMatch(string api, string id)
        {
            return API == api && ID == id;
        }

        public IVoiceParameter MigrateParameter(IVoiceParameter currentParameter)
        {
            if (currentParameter is not KotodamaVoiceParameter parameter)
                return CreateVoiceParameter();

            if(!speaker.Decorations.Select(x=>x.DecorationId).Contains(parameter.DecorationId))
                parameter.DecorationId = speaker.Decorations.FirstOrDefault()?.DecorationId;

            return parameter;
        }

        internal IEnumerable<KotodamaDecorationSettings> GetDecorations() => speaker.Decorations;
    }
}
