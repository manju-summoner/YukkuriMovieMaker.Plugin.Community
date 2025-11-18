using System.Collections.Immutable;
using System.Globalization;
using YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk.API;
using YukkuriMovieMaker.Plugin.Voice;

namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk
{
    internal class VoiSonaTalkVoiceSpeaker(string voiceName) : IVoiceSpeaker
    {
        static readonly SemaphoreSlim semaphore = new(1);
        public string EngineName => "VoiSona Talk";

        public string SpeakerName => VoiSonaTalkAPIHelper.GetVoicesByVoiceName(voiceName).FirstOrDefault()?.GetDisplayName(CultureInfo.CurrentUICulture) ?? string.Empty;

        public string API => "VoiSonaTalk";

        public string ID => voiceName;

        public bool IsVoiceDataCachingRequired => false;

        public SupportedTextFormat Format => SupportedTextFormat.Text;

        public IVoiceLicense? License => null;

        public IVoiceResource? Resource => null;

        public async Task<string> ConvertKanjiToYomiAsync(string text, IVoiceParameter voiceParameter)
        {
            throw new NotImplementedException();
        }

        public async Task<IVoicePronounce?> CreateVoiceAsync(string text, IVoicePronounce? pronounce, IVoiceParameter? parameter, string filePath)
        {
            if (parameter is not VoiSonaTalkVoiceParameter param)
                return null;
            
            // バージョンを確定
            var voices = VoiSonaTalkAPIHelper.GetVoicesByVoiceName(voiceName);
            string version;
            if (voices.Any(v => v.VoiceVersion == param.Version))
                version = param.Version;
            else
                version = voices.Select(v => v.VoiceVersion).FirstOrDefault() ?? string.Empty;
            if(string.IsNullOrEmpty(version))
                return null;

            // 音声情報を取得
            var voiceInfo = voices.FirstOrDefault(v => v.VoiceVersion == version);
            if (voiceInfo is null)
                return null;

            // 言語を確定
            string lang = voiceInfo.Languages.Contains(param.Version) ? param.Version : voiceInfo.Languages.FirstOrDefault() ?? string.Empty;
            if (string.IsNullOrEmpty(lang))
                return null;

            await semaphore.WaitAsync();
            try
            {

                // 発音情報を取得
                if (pronounce is not VoiSonaTalkVoicePronounce pron || pron.TSML is null)
                {
                    var textInfo = await VoiSonaTalkAPIHelper.AnalyzeTextAsync(text, lang);
                    if (textInfo is null || textInfo.Text is null)
                        return null;
                    pron = new VoiSonaTalkVoicePronounce() { TSML = textInfo?.AnalyzedText };
                }
                if (pron.TSML is null)
                    return null;

                // スタイルを確定
                double[] styleWeights = [.. voiceInfo.DefaultStyleWeights];
                foreach (var sw in param.StyleWeights)
                {
                    int index = Array.IndexOf(voiceInfo.StyleNames, sw.Name);
                    if (index >= 0 && index < styleWeights.Length)
                        styleWeights[index] = sw.Weight;
                }

                var request = new SpeechSynthesisRequest(
                    pron.TSML,
                    true,
                    SpeechSynthesisDestination.File,
                    true,
                    new SpeechSynthesisGlobalParameters(
                        param.Alp,
                        param.Huskiness,
                        param.Intonation,
                        param.Pitch,
                        param.Speed,
                        styleWeights,
                        0),
                    lang,
                    filePath,
                    voiceName,
                    version);
                var speechInfo = await VoiSonaTalkAPIHelper.SpeechSynthesisAsync(request);
                pron.TSML = speechInfo?.AnalyzedText;
                if (speechInfo is null)
                    return null;

                return pron;
            }
            finally
            {
                semaphore.Release();
            }
        }

        public IVoiceParameter CreateVoiceParameter()
        {
            var version = VoiSonaTalkAPIHelper.GetVersionsByVoiceName(voiceName).FirstOrDefault() ?? string.Empty;
            var language = VoiSonaTalkAPIHelper.GetLanguagesByVoiceName(voiceName).FirstOrDefault() ?? string.Empty;
            var styleWeights = VoiSonaTalkAPIHelper.GetStyleWeightsByVoiceName(voiceName);
            var param = new VoiSonaTalkVoiceParameter()
            {
                Language = language,
                Version = version,
                StyleWeights = [..styleWeights],
            };
            return param;
        }

        public bool IsMatch(string api, string id)
        {
            return api == API && id == ID;
        }

        public IVoiceParameter MigrateParameter(IVoiceParameter currentParameter)
        {
            if (currentParameter is not VoiSonaTalkVoiceParameter cp)
                return CreateVoiceParameter();

            var versions = VoiSonaTalkAPIHelper.GetVersionsByVoiceName(voiceName);
            if(!versions.Contains(cp.Version))
                cp.Version = versions.FirstOrDefault() ?? string.Empty;

            var languages = VoiSonaTalkAPIHelper.GetLanguagesByVoiceName(voiceName);
            if(!languages.Contains(cp.Language))
                cp.Language = languages.FirstOrDefault() ?? string.Empty;

            var styleWeights = VoiSonaTalkAPIHelper.GetStyleWeightsByVoiceName(voiceName);
            if (!cp.StyleWeights.Select(sw => sw.Name).SequenceEqual(styleWeights.Select(sw => sw.Name)))
            {
                foreach(var cpsw in cp.StyleWeights)
                {
                    var sw = styleWeights.FirstOrDefault(s => s.Name == cpsw.Name);
                    if (sw != null)
                        sw.Weight = cpsw.Weight;
                }
                cp.StyleWeights = [..styleWeights];
            }

            return cp;
        }
    }
}
