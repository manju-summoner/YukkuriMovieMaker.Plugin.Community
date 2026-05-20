using System;
using System.IO;
using System.Threading.Tasks;
using YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services;
using YukkuriMovieMaker.Plugin.Voice;

namespace YukkuriMovieMaker.Plugin.Community.Voice.Recording
{
    public class RecordedVoiceSpeaker : IVoiceSpeaker
    {
        public const string ApiName = "CommunityRecording";
        public const string SpeakerId = "CommunitMicRecording";

        public static RecordedVoiceSpeaker Instance { get; } = new RecordedVoiceSpeaker();
        public static VoiceDescription Description { get; } = new VoiceDescription(Instance);

        static RecordedVoiceSpeaker()
        {
        }

        public string EngineName => "録音ツール";
        public string SpeakerName => "録音";
        public string API => ApiName;
        public string ID => SpeakerId;
        public bool IsVoiceDataCachingRequired => false;
        public SupportedTextFormat Format => SupportedTextFormat.Text;
        public IVoiceLicense License => new NoVoiceLicense();
        public IVoiceResource Resource => new NoVoiceResource();
        public string SpeakerAuthor => string.Empty;
        public string SpeakerContentId => string.Empty;
        public string EngineAuthor => string.Empty;
        public string EngineContentId => string.Empty;

        public Task<string> ConvertKanjiToYomiAsync(string text, IVoiceParameter parameter)
        {
            return Task.FromResult(text);
        }

        public Task<IVoicePronounce?> CreateVoiceAsync(string text, IVoicePronounce? pronounce, IVoiceParameter? parameter, string outputFilePath)
        {
            if (parameter is not RecordedVoiceParameter recorded)
                throw new InvalidOperationException("録音パラメータが不正です。");

            if (string.IsNullOrWhiteSpace(recorded.AudioFilePath))
                throw new InvalidOperationException("録音済み wav のパスが空です。");

            if (!File.Exists(recorded.AudioFilePath))
                throw new FileNotFoundException("録音済み wav が見つかりません。", recorded.AudioFilePath);

            Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath) ?? ".");
            File.Copy(recorded.AudioFilePath, outputFilePath, overwrite: true);

            var result = pronounce ?? new RecordedVoicePronounce();
            return Task.FromResult<IVoicePronounce?>(result);
        }

        public IVoiceParameter CreateVoiceParameter()
        {
            var parameter = new RecordedVoiceParameter();
            var silentPath = new RecordPathService().GetOrCreateSilentWavPath(TimeSpan.FromSeconds(5));
            if (!string.IsNullOrWhiteSpace(silentPath))
            {
                parameter.AudioFilePath = silentPath;
                parameter.Duration = TimeSpan.FromSeconds(5);
                parameter.CreatedAt = DateTime.Now;
            }
            return parameter;
        }

        public bool IsMatch(string api, string id)
        {
            return string.Equals(api, API, StringComparison.Ordinal) && string.Equals(id, ID, StringComparison.Ordinal);
        }

        public IVoiceParameter MigrateParameter(IVoiceParameter parameter)
        {
            if (parameter is RecordedVoiceParameter recorded)
            {
                if (string.IsNullOrWhiteSpace(recorded.AudioFilePath) || !File.Exists(recorded.AudioFilePath))
                {
                    var silentPath = new RecordPathService().GetOrCreateSilentWavPath(TimeSpan.FromSeconds(5));
                    if (!string.IsNullOrWhiteSpace(silentPath))
                    {
                        recorded.AudioFilePath = silentPath;
                        recorded.Duration ??= TimeSpan.FromSeconds(5);
                        recorded.CreatedAt ??= DateTime.Now;
                    }
                }
                return recorded;
            }

            return CreateVoiceParameter();
        }
    }
}


