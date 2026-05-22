using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NAudio.Wave;
using YukkuriMovieMaker.Plugin.Community.Tool.Recording;
using YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services;
using YukkuriMovieMaker.Plugin.Voice;

namespace YukkuriMovieMaker.Plugin.Community.Voice.Recording
{
    public class RecordedVoiceSpeaker : IVoiceSpeaker
    {
        private const string InitialUnselectedFileName = "Unselected.wav";
        private const string LegacyJapaneseUnselectedFileName = "未選択.wav";
        private const string LegacySilentFileName = "Silent_5s.wav";

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

            var sourceWavPath = ResolveRecordedWavPath(recorded);
            if (string.IsNullOrWhiteSpace(sourceWavPath))
                throw new InvalidOperationException(Texts.RecordedWavNotFound);

            if (!File.Exists(sourceWavPath))
                throw new FileNotFoundException(Texts.RecordedWavNotFound, sourceWavPath);

            Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath) ?? ".");
            File.Copy(sourceWavPath, outputFilePath, overwrite: true);

            var result = pronounce ?? new RecordedVoicePronounce();
            return Task.FromResult<IVoicePronounce?>(result);
        }

        public IVoiceParameter CreateVoiceParameter()
        {
            var directory = ResolveRecordsDirectory();
            return new RecordedVoiceParameter
            {
                RecordsDirectory = directory,
                AudioFilePath = ResolveInitialAudioFilePath(directory)
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
                if (string.IsNullOrWhiteSpace(recorded.RecordsDirectory))
                    recorded.RecordsDirectory = ResolveRecordsDirectory();

                if (string.IsNullOrWhiteSpace(recorded.AudioFilePath))
                    recorded.AudioFilePath = ResolveInitialAudioFilePath(recorded.RecordsDirectory);

                return recorded;
            }

            return CreateVoiceParameter();
        }

        private static string ResolveRecordedWavPath(RecordedVoiceParameter recorded)
        {
            if (!string.IsNullOrWhiteSpace(recorded.AudioFilePath) && File.Exists(recorded.AudioFilePath))
                return recorded.AudioFilePath;

            var defaultPath = ResolveDefaultVoiceAudioFilePath(recorded.RecordsDirectory);
            if (!string.IsNullOrWhiteSpace(defaultPath))
                return defaultPath;

            if (string.IsNullOrWhiteSpace(recorded.RecordsDirectory) || !Directory.Exists(recorded.RecordsDirectory))
                return string.Empty;

            return Directory.EnumerateFiles(recorded.RecordsDirectory, "*.wav", SearchOption.TopDirectoryOnly)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault() ?? string.Empty;
        }

        private static string ResolveInitialAudioFilePath(string recordsDirectory)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(recordsDirectory))
                    return string.Empty;

                var defaultPath = ResolveDefaultVoiceAudioFilePath(recordsDirectory);
                if (!string.IsNullOrWhiteSpace(defaultPath))
                    return defaultPath;

                Directory.CreateDirectory(recordsDirectory);
                var path = Path.Combine(recordsDirectory, InitialUnselectedFileName);
                var legacyJapanesePath = Path.Combine(recordsDirectory, LegacyJapaneseUnselectedFileName);
                var legacyPath = Path.Combine(recordsDirectory, LegacySilentFileName);

                if (!File.Exists(path) && File.Exists(legacyJapanesePath))
                {
                    File.Move(legacyJapanesePath, path);
                    return path;
                }

                if (!File.Exists(path) && File.Exists(legacyPath))
                {
                    File.Move(legacyPath, path);
                    return path;
                }

                if (File.Exists(path) && File.Exists(legacyJapanesePath))
                    File.Delete(legacyJapanesePath);

                if (File.Exists(path) && File.Exists(legacyPath))
                    File.Delete(legacyPath);

                if (File.Exists(path))
                    return path;

                var format = new WaveFormat(44100, 16, 1);
                var totalBytes = format.AverageBytesPerSecond * 5;
                var buffer = new byte[4096];

                using var writer = new WaveFileWriter(path, format);
                var remaining = totalBytes;
                while (remaining > 0)
                {
                    var writeBytes = Math.Min(buffer.Length, remaining);
                    writer.Write(buffer, 0, writeBytes);
                    remaining -= writeBytes;
                }

                return path;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string ResolveRecordsDirectory()
        {
            try
            {
                return new RecordPathService().GetRecordsDirectory();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string ResolveDefaultVoiceAudioFilePath(string recordsDirectory)
        {
            try
            {
                var path = RecordingSettings.Default.DefaultVoiceAudioFilePath;
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    return string.Empty;

                if (!string.IsNullOrWhiteSpace(recordsDirectory))
                {
                    var directoryFullPath = Path.GetFullPath(recordsDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    var fileFullPath = Path.GetFullPath(path);
                    if (!fileFullPath.StartsWith(directoryFullPath, StringComparison.OrdinalIgnoreCase))
                        return string.Empty;
                }

                return path;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
