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
        private const string InitialSilentFileName = "Silent_5s.wav";

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
            var recordsDirectory = !string.IsNullOrWhiteSpace(recorded.RecordsDirectory)
                ? recorded.RecordsDirectory
                : ResolveRecordsDirectory();

            if (string.Equals(recorded.AudioFilePath, RecordedVoiceParameter.ExplicitUnselectedToken, StringComparison.Ordinal))
                return GetOrCreateSilentFilePath(recordsDirectory);

            if (!string.IsNullOrWhiteSpace(recorded.AudioFilePath))
            {
                var resolvedPath = ResolveExistingPath(recorded.AudioFilePath, recordsDirectory);
                if (!string.IsNullOrWhiteSpace(resolvedPath))
                    return resolvedPath;
                return string.Empty;
            }

            var defaultPath = ResolveDefaultVoiceAudioFilePath(recordsDirectory);
            if (!string.IsNullOrWhiteSpace(defaultPath))
                return defaultPath;

            if (string.IsNullOrWhiteSpace(recordsDirectory) || !Directory.Exists(recordsDirectory))
                return string.Empty;

            return Directory.EnumerateFiles(recordsDirectory, "*.wav", SearchOption.TopDirectoryOnly)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault() ?? string.Empty;
        }

        private static string ResolveExistingPath(string path, string recordsDirectory)
        {
            try
            {
                // 相対パスは CWD 上の同名ファイルを誤って拾わないよう、必ず recordsDirectory 基準で解決する。
                if (Path.IsPathRooted(path) && File.Exists(path))
                    return path;

                if (!string.IsNullOrWhiteSpace(recordsDirectory) && Directory.Exists(recordsDirectory))
                {
                    // Support legacy/serialized relative file names and moved folders.
                    var fileName = Path.GetFileName(path);
                    if (!string.IsNullOrWhiteSpace(fileName))
                    {
                        var combined = Path.Combine(recordsDirectory, fileName);
                        if (File.Exists(combined))
                            return combined;
                    }

                    if (!Path.IsPathRooted(path))
                    {
                        var full = Path.GetFullPath(Path.Combine(recordsDirectory, path));
                        if (File.Exists(full))
                            return full;
                    }
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private static string ResolveInitialAudioFilePath(string recordsDirectory)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(recordsDirectory))
                    recordsDirectory = ResolveRecordsDirectory();

                if (string.IsNullOrWhiteSpace(recordsDirectory))
                    return string.Empty;

                var defaultPath = ResolveDefaultVoiceAudioFilePath(recordsDirectory);
                if (!string.IsNullOrWhiteSpace(defaultPath))
                    return defaultPath;

                return RecordedVoiceParameter.ExplicitUnselectedToken;
            }
            catch
            {
                return string.Empty;
            }
        }

        internal static string GetOrCreateSilentFilePath(string? recordsDirectory)
        {
            var directory = string.IsNullOrWhiteSpace(recordsDirectory)
                ? ResolveRecordsDirectory()
                : recordsDirectory;
            return GetOrCreateSilentWavInDirectory(directory);
        }

        private static string GetOrCreateSilentWavInDirectory(string? recordsDirectory)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(recordsDirectory))
                    return string.Empty;

                Directory.CreateDirectory(recordsDirectory);
                var path = Path.Combine(recordsDirectory, InitialSilentFileName);

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
                if (string.IsNullOrWhiteSpace(path))
                    return string.Empty;

                return ResolveExistingPath(path, recordsDirectory);
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
