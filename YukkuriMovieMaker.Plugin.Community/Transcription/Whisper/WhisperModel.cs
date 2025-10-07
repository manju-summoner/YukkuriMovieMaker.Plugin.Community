using Newtonsoft.Json;
using System.IO;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Transcription.Whisper
{
    public readonly record struct WhisperModel
    {
        public string Name { get; }
        public string FileSize { get; }
        public string? URL { get; }

        [JsonIgnore]
        public string DisplayText => $"{Name} ({FileSize})";

        public WhisperModel(string name, string fileSize, string? url = null)
        {
            Name = name;
            FileSize = fileSize;
            URL = url;
        }


        public string GetFilePath(string baseDir) => Path.Combine(baseDir, Name);

        internal async Task DownloadAsync(string modelDirectory, ProgressMessage progressMessage, CancellationToken token)
        {
            var filePath = GetFilePath(modelDirectory);
            if(string.IsNullOrEmpty(URL))
                throw new InvalidOperationException("URL is null or empty.");
            await Downloader.DownloadAsync(URL, filePath, progressMessage, token);
        }
    }
}
