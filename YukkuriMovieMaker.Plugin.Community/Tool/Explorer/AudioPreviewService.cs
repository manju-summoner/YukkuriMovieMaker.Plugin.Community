using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using YukkuriMovieMaker.Plugin.FileSource;
using YukkuriMovieMaker.Settings;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Explorer
{
    public record AudioPreview(TimeSpan Duration, TimeSpan Start, TimeSpan Length, Geometry Waveform)
    {
        public string DurationText => Duration.TotalHours >= 1
            ? Duration.ToString(@"h\:mm\:ss")
            : Duration.ToString(@"m\:ss\.f");
    }

    internal static class AudioPreviewService
    {
        const int Columns = 300;
        const int Height = 48;
        const int ReadFrames = 16384;

        static readonly SemaphoreSlim gate = new(Math.Clamp(Environment.ProcessorCount / 4, 2, 4));

        public static TimeSpan[] SupportedWindowLengths { get; } =
        [
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(15),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(60),
            TimeSpan.FromSeconds(120),
        ];

        public static TimeSpan DefaultWindowLength { get; } = TimeSpan.FromSeconds(30);

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

        public static TimeSpan GetWindowStart(TimeSpan position, TimeSpan windowLength)
        {
            if (position <= TimeSpan.Zero || windowLength <= TimeSpan.Zero)
                return TimeSpan.Zero;
            return TimeSpan.FromTicks(position.Ticks / windowLength.Ticks * windowLength.Ticks);
        }

        public static async Task<AudioPreview?> LoadAsync(string path, TimeSpan start, TimeSpan windowLength, CancellationToken token)
        {
            await gate.WaitAsync(token);
            try
            {
                token.ThrowIfCancellationRequested();
                return await Task.Run(() => Load(path, start, windowLength, token), token);
            }
            finally
            {
                gate.Release();
            }
        }

        static AudioPreview? Load(string path, TimeSpan start, TimeSpan windowLength, CancellationToken token)
        {
            using var source = AudioFileSourceFactory.Create(path, 0);
            if (source is null)
                return null;

            var duration = source.Duration;
            if (duration <= TimeSpan.Zero)
                return null;

            if (start < TimeSpan.Zero || start >= duration)
                start = TimeSpan.Zero;

            var length = duration - start;
            if (length > windowLength)
                length = windowLength;

            var totalFrames = (long)(length.TotalSeconds * source.Hz);
            if (totalFrames <= 0)
                return null;

            if (start > TimeSpan.Zero)
                source.Seek(start);

            var minimums = new float[Columns];
            var maximums = new float[Columns];
            var isFilled = new bool[Columns];
            var buffer = new float[ReadFrames * 2];
            var column = 0;
            var columnEndFrame = totalFrames / Columns;
            long frame = 0;
            int read;

            while (frame < totalFrames && (read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                token.ThrowIfCancellationRequested();
                for (var i = 0; i + 1 < read && frame < totalFrames; i += 2)
                {
                    while (column < Columns - 1 && frame >= columnEndFrame)
                    {
                        column++;
                        columnEndFrame = totalFrames * (column + 1) / Columns;
                    }
                    isFilled[column] = true;
                    Accumulate(buffer[i], column, minimums, maximums);
                    Accumulate(buffer[i + 1], column, minimums, maximums);
                    frame++;
                }
            }

            if (frame <= 0)
                return null;

            for (var i = 1; i < Columns; i++)
            {
                if (isFilled[i])
                    continue;
                minimums[i] = minimums[i - 1];
                maximums[i] = maximums[i - 1];
            }

            var max = 0f;
            for (var i = 0; i < Columns; i++)
                max = Math.Max(max, Math.Max(Math.Abs(minimums[i]), Math.Abs(maximums[i])));

            var scale = max > 0f ? Height * 0.5 / max : 0.0;
            return new AudioPreview(duration, start, length, BuildGeometry(minimums, maximums, scale));
        }

        static void Accumulate(float value, int column, float[] minimums, float[] maximums)
        {
            if (!float.IsNormal(value))
                return;
            if (value < minimums[column])
                minimums[column] = value;
            if (maximums[column] < value)
                maximums[column] = value;
        }

        static Geometry BuildGeometry(float[] minimums, float[] maximums, double scale)
        {
            var center = Height * 0.5;
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(new Point(0, center - maximums[0] * scale), true, true);
                for (var i = 1; i < Columns; i++)
                    context.LineTo(new Point(i, center - maximums[i] * scale), true, false);
                for (var i = Columns - 1; i >= 0; i--)
                    context.LineTo(new Point(i, center - minimums[i] * scale), true, false);
            }
            geometry.Freeze();
            return geometry;
        }
    }
}
