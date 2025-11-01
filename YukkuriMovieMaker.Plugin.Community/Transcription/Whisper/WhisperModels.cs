using System.IO;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Transcription.Whisper
{
    public class WhisperModels
    {
        public static string ModelDirectory => Path.Combine(AppDirectories.ResourceDirectory, "models", "whisper");

        const string whisperCppBaseURL = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/";
        const string kotobaWhisper2BaseURL = "https://huggingface.co/kotoba-tech/kotoba-whisper-v2.0-ggml/resolve/main/";

        public static readonly IReadOnlyList<WhisperModel> DefaultModels =
            [
                new WhisperModel("ggml-large-v3.bin","3.1 GB", whisperCppBaseURL + "ggml-large-v3.bin"),
                new WhisperModel("ggml-large-v3-turbo.bin","1.62 GB", whisperCppBaseURL + "ggml-large-v3-turbo.bin"),
                new WhisperModel("ggml-large-v3-turbo-q8_0.bin","874 MB", whisperCppBaseURL + "ggml-large-v3-turbo-q8_0.bin"),
                new WhisperModel("ggml-large-v3-turbo-q5_0.bin","574 MB", whisperCppBaseURL + "ggml-large-v3-turbo-q5_0.bin"),
                new WhisperModel("ggml-medium-q8_0.bin","823 MB", whisperCppBaseURL + "ggml-medium-q8_0.bin"),
                new WhisperModel("ggml-small-q8_0.bin","264 MB", whisperCppBaseURL + "ggml-small-q8_0.bin"),

                new WhisperModel("ggml-kotoba-whisper-v2.0.bin","1.52 GB", kotobaWhisper2BaseURL + "ggml-kotoba-whisper-v2.0.bin"),
                new WhisperModel("ggml-kotoba-whisper-v2.0-q5_0.bin", "538 MB", kotobaWhisper2BaseURL + "ggml-kotoba-whisper-v2.0-q5_0.bin"),
            ];

        public static WhisperModel GetDefaultModel() => DefaultModels.DefaultIfEmpty(DefaultModels[0]).First(x => x.Name == "ggml-large-v3-turbo.bin");

        public static IReadOnlyList<WhisperModel> GetDefaultAndUserModels()
        {
            if (!Directory.Exists(ModelDirectory))
                Directory.CreateDirectory(ModelDirectory);

            var userModels =
                Directory.GetFiles(ModelDirectory, "*.bin", SearchOption.TopDirectoryOnly)
                .Where(x => !DefaultModels.Where(model => model.Name == Path.GetFileName(x)).Any())
                .Select(x => new WhisperModel(Path.GetFileName(x), GetFileSize(x)));

            return [.. DefaultModels, .. userModels];
        }

        static string GetFileSize(string filePath)
        {
            string[] units = ["B", "KB", "MB", "GB", "TB"];

            var size = (double)new FileInfo(filePath).Length;
            int unitIndex = 0;

            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }

            return $"{size:0.##} {units[unitIndex]}";
        }
    }
}
