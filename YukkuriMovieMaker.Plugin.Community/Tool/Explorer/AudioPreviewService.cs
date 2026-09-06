using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using YukkuriMovieMaker.Plugin.FileSource;
using YukkuriMovieMaker.Settings;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Explorer
{
    public record AudioPreview(TimeSpan Duration, Geometry Peak, Geometry Rms)
    {
        public string DurationText => Duration.TotalHours >= 1
            ? Duration.ToString(@"h\:mm\:ss")
            : Duration.ToString(@"m\:ss\.f");
    }

    internal static class AudioPreviewService
    {
        const int Columns = 300;
        const int Height = 48;
        const int ScanFrames = 16384;
        const int SampleFrames = 8192;

        static readonly TimeSpan SampleThreshold = TimeSpan.FromMinutes(2);
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
            var rms = new float[Columns];

            if (duration > SampleThreshold)
            {
                Sample(source, duration, peaks, rms, token);
                if (MaxOf(peaks) <= 0f)
                {
                    source.Seek(TimeSpan.Zero);
                    Scan(source, totalFrames, peaks, rms, token);
                }
                else
                {
                    Smooth(peaks);
                    Smooth(rms);
                }
            }
            else
            {
                Scan(source, totalFrames, peaks, rms, token);
            }

            var max = MaxOf(peaks);
            if (max <= 0f)
                return null;

            var scale = Height * 0.5 / max;
            return new AudioPreview(duration, BuildGeometry(peaks, scale), BuildGeometry(rms, scale));
        }

        static void Scan(IAudioFileSource source, long totalFrames, float[] peaks, float[] rms, CancellationToken token)
        {
            var squareSums = new double[Columns];
            var sampleCounts = new long[Columns];
            var buffer = new float[ScanFrames * 2];
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
                    Accumulate(buffer[i], buffer[i + 1], column, peaks, squareSums, sampleCounts);
                    frame++;
                }
            }
            Resolve(squareSums, sampleCounts, rms);
        }

        static void Sample(IAudioFileSource source, TimeSpan duration, float[] peaks, float[] rms, CancellationToken token)
        {
            var squareSums = new double[Columns];
            var sampleCounts = new long[Columns];
            var buffer = new float[SampleFrames * 2];
            for (var column = 0; column < Columns; column++)
            {
                token.ThrowIfCancellationRequested();
                source.Seek(TimeSpan.FromSeconds(duration.TotalSeconds * column / Columns));

                var filled = 0;
                while (filled < buffer.Length)
                {
                    var read = source.Read(buffer, filled, buffer.Length - filled);
                    if (read <= 0)
                        break;
                    filled += read;
                }

                for (var i = 0; i + 1 < filled; i += 2)
                    Accumulate(buffer[i], buffer[i + 1], column, peaks, squareSums, sampleCounts);
            }
            Resolve(squareSums, sampleCounts, rms);
        }

        static void Accumulate(float left, float right, int column, float[] peaks, double[] squareSums, long[] sampleCounts)
        {
            var level = Math.Max(Math.Abs(left), Math.Abs(right));
            if (level > peaks[column])
                peaks[column] = level;
            squareSums[column] += (double)left * left + (double)right * right;
            sampleCounts[column] += 2;
        }

        static void Resolve(double[] squareSums, long[] sampleCounts, float[] rms)
        {
            for (var i = 0; i < Columns; i++)
                rms[i] = sampleCounts[i] > 0 ? (float)Math.Sqrt(squareSums[i] / sampleCounts[i]) : 0f;
        }

        static void Smooth(float[] values)
        {
            var source = (float[])values.Clone();
            for (var i = 0; i < Columns; i++)
            {
                var sum = source[i];
                var count = 1;
                if (i > 0)
                {
                    sum += source[i - 1];
                    count++;
                }
                if (i < Columns - 1)
                {
                    sum += source[i + 1];
                    count++;
                }
                values[i] = sum / count;
            }
        }

        static float MaxOf(float[] values)
        {
            var max = 0f;
            foreach (var value in values)
                max = Math.Max(max, value);
            return max;
        }

        static Geometry BuildGeometry(float[] values, double scale)
        {
            var center = Height * 0.5;
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(new Point(0, center - values[0] * scale), true, true);
                for (var i = 1; i < Columns; i++)
                    context.LineTo(new Point(i, center - values[i] * scale), true, false);
                for (var i = Columns - 1; i >= 0; i--)
                    context.LineTo(new Point(i, center + values[i] * scale), true, false);
            }
            geometry.Freeze();
            return geometry;
        }
    }
}
