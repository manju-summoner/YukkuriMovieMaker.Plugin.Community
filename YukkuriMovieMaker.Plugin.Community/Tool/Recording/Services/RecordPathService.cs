using System;
using System.IO;
using YukkuriMovieMaker.Plugin.Community.Tool.Recording;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services
{
    public class RecordPathService
    {
        private readonly string? customBaseDirectory;
        private readonly Func<string?>? customBaseDirectoryProvider;

        public RecordPathService(string? customBaseDirectory = null)
        {
            this.customBaseDirectory = customBaseDirectory;
        }

        public RecordPathService(Func<string?> customBaseDirectoryProvider)
        {
            this.customBaseDirectoryProvider = customBaseDirectoryProvider;
        }

        public string GetRecordsDirectory()
        {
            var selectedBaseDirectory = customBaseDirectoryProvider?.Invoke() ?? customBaseDirectory;
            if (string.IsNullOrWhiteSpace(selectedBaseDirectory))
                selectedBaseDirectory = RecordingSettings.Default.OutputDirectory;
            if (string.IsNullOrWhiteSpace(selectedBaseDirectory))
                throw new InvalidOperationException(Texts.OutputFolderUnavailable);

            Directory.CreateDirectory(selectedBaseDirectory);
            return selectedBaseDirectory;
        }

        public string CreateRecordFilePath()
        {
            var recordsDirectory = GetRecordsDirectory();
            var fileName = $"Record_{DateTime.Now:yyyyMMdd_HHmmss}";
            var filePath = Path.Combine(recordsDirectory, $"{fileName}.wav");
            var sequence = 1;

            while (File.Exists(filePath))
            {
                filePath = Path.Combine(recordsDirectory, $"{fileName}_{sequence:000}.wav");
                sequence++;
            }
            return filePath;
        }
    }
}
