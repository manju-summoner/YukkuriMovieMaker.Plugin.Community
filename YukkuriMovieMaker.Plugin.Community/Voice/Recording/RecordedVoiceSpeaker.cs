using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services;
using YukkuriMovieMaker.Plugin.Voice;

namespace YukkuriMovieMaker.Plugin.Community.Voice.Recording
{
    public class RecordedVoiceSpeaker : IVoiceSpeaker
    {
        public static RecordedVoiceSpeaker Instance { get; } = new RecordedVoiceSpeaker();
        public static VoiceDescription Description { get; } = new VoiceDescription(Instance);

        public string EngineName => Texts.EngineName;
        public string SpeakerName => Texts.SpeakerName;
        public string API => RecordingPluginIds.ApiName;
        public string ID => RecordingPluginIds.SpeakerId;
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
                throw new InvalidOperationException(Texts.InvalidParameter);

            if (string.IsNullOrWhiteSpace(recorded.AudioFilePath))
                throw new InvalidOperationException(Texts.AudioFilePathEmpty);

            if (!File.Exists(recorded.AudioFilePath))
                throw new FileNotFoundException(Texts.RecordedWavNotFound, recorded.AudioFilePath);

            Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath) ?? ".");
            File.Copy(recorded.AudioFilePath, outputFilePath, overwrite: true);

            var result = pronounce ?? new RecordedVoicePronounce();
            return Task.FromResult<IVoicePronounce?>(result);
        }

        public IVoiceParameter CreateVoiceParameter()
        {
            return new RecordedVoiceParameter
            {
                AudioFilePath = ResolveInitialAudioFilePath()
            };
        }

        public bool IsMatch(string api, string id)
        {
            if (!string.Equals(api, API, StringComparison.Ordinal))
                return false;

            return string.Equals(id, RecordingPluginIds.SpeakerId, StringComparison.Ordinal)
                || string.Equals(id, RecordingPluginIds.LegacySpeakerId, StringComparison.Ordinal)
                || string.Equals(id, RecordingPluginIds.LegacySpeakerIdTypo, StringComparison.Ordinal);
        }

        public IVoiceParameter MigrateParameter(IVoiceParameter parameter)
        {
            if (parameter is RecordedVoiceParameter recorded)
            {
                if (string.IsNullOrWhiteSpace(recorded.AudioFilePath))
                    recorded.AudioFilePath = ResolveInitialAudioFilePath();
                return recorded;
            }

            return CreateVoiceParameter();
        }

        private static string ResolveInitialAudioFilePath()
        {
            try
            {
                var directory = new RecordPathService().GetRecordsDirectory();
                return Directory.EnumerateFiles(directory, "*.wav", SearchOption.TopDirectoryOnly)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
