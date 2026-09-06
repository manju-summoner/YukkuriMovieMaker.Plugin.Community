using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using YukkuriMovieMaker.Settings;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Explorer
{
    internal record AudioPreview(TimeSpan Duration, Geometry Waveform);

    internal static class AudioPreviewService
    {
        const int Columns = 300;
        const int ReadFrames = 16384;

        static readonly SemaphoreSlim gate = new(Math.Max(2, Environment.ProcessorCount / 4));

        public static bool IsSupported(string path)
        {
            try
            {
                return FileSettings.Default.FileExtensions.GetFileType(path) == FileType.音声;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<AudioPreview?> LoadAsync(string path, CancellationToken token)
        {
            await gate.WaitAsync(token);
            try
            {
                token.ThrowIfCancellationRequested();
                return await Task.Run(() => Load(path, token), token);
            }
            finally
            {
                gate.Release();
            }
        }

        static AudioPreview? Load(string path, CancellationToken token)
        {
            using var source = AudioFileSourceFactory.Create(path, 0);
            if (source is null)
                return null;

            var duration = source.Duration;
            var totalFrames = (long)(duration.TotalSeconds * source.Hz);
            if (totalFrames <= 0)
                return null;

            var peaks = new float[Columns];
            var buffer = new float[ReadFrames * 2];
            var column = 0;
            var columnEndFrame = totalFrames / Columns;
            long frame = 0;
            int read;
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                token.ThrowIfCancellationRequested();
                for (var i = 0; i + 1 < read; i += 2)
                {
                    while (column < Columns - 1 && frame >= columnEndFrame)
                    {
                        column++;
                        columnEndFrame = totalFrames * (column + 1) / Columns;
                    }
                    var level = Math.Max(Math.Abs(buffer[i]), Math.Abs(buffer[i + 1]));
                    if (level > peaks[column])
                        peaks[column] = level;
                    frame++;
                }
            }

            var max = 0f;
            foreach (var peak in peaks)
                max = Math.Max(max, peak);
            if (max <= 0f)
                return null;

            var scale = 1f / max;
            var waveform = new StreamGeometry();
            using (var context = waveform.Open())
            {
                context.BeginFigure(new Point(0, peaks[0] * scale), true, true);
                for (var i = 1; i < Columns; i++)
                    context.LineTo(new Point(i, peaks[i] * scale), true, false);
                for (var i = Columns - 1; i >= 0; i--)
                    context.LineTo(new Point(i, -peaks[i] * scale), true, false);
            }
            waveform.Freeze();

            return new AudioPreview(duration, waveform);
        }
    }
}
