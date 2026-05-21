using System;
using System.IO;
using NAudio.Wave;
using YukkuriMovieMaker.Plugin.Community.Tool.Recording;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services
{
    public class RecordPathService
    {
        private readonly string? customBaseDirectory;

        public RecordPathService(string? customBaseDirectory = null)
        {
            this.customBaseDirectory = customBaseDirectory;
        }

        public string GetRecordsDirectory()
        {
            var selectedBaseDirectory = customBaseDirectory;
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

        public string GetOrCreateSilentWavPath(TimeSpan duration)
        {
            var recordsDirectory = GetRecordsDirectory();
            var seconds = Math.Max(1, (int)Math.Round(duration.TotalSeconds, MidpointRounding.AwayFromZero));
            var filePath = Path.Combine(recordsDirectory, $"Silent_{seconds}s.wav");

            try
            {
                var format = new WaveFormat(RecordingAudioFormat.SampleRate, RecordingAudioFormat.BitDepth, RecordingAudioFormat.Channels);
                var bytesPerSecond = format.AverageBytesPerSecond;
                var totalBytes = (long)seconds * bytesPerSecond;
                var buffer = new byte[bytesPerSecond];

                // Avoid TOCTOU race by opening with CreateNew and falling back to existing file when already created.
                using (var stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                {
                    using var writer = new WaveFileWriter(stream, format);
                    var remaining = totalBytes;
                    while (remaining > 0)
                    {
                        var toWrite = (int)Math.Min(buffer.Length, remaining);
                        writer.Write(buffer, 0, toWrite);
                        remaining -= toWrite;
                    }
                    writer.Flush();
                }
            }
            catch (IOException) when (File.Exists(filePath))
            {
                return filePath;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(Texts.SilentWavCreateFailed, ex);
            }

            return filePath;
        }
    }
}




