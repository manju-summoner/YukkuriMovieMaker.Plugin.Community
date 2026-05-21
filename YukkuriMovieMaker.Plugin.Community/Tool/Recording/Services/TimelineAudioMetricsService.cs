using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using NAudio.Wave;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services
{
    internal sealed class TimelineAudioMetricsService
    {
        public TimeSpan GetAudioDuration(string filePath)
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new WaveFileReader(stream);
            return reader.TotalTime;
        }

        public int GetLengthFrames(object timeline, string filePath, double fallbackFps = 60.0)
        {
            var fps = GetTimelineFps(timeline, fallbackFps);
            var durationSeconds = GetAudioDuration(filePath).TotalSeconds;
            var frames = (int)Math.Round(durationSeconds * fps, MidpointRounding.AwayFromZero);
            return Math.Max(1, frames);
        }

        public double GetTimelineFps(object timeline, double fallbackFps)
        {
            try
            {
                var videoInfoProperty = timeline.GetType().GetProperty("VideoInfo", BindingFlags.Public | BindingFlags.Instance);
                var videoInfo = videoInfoProperty?.GetValue(timeline);
                if (videoInfo is not null)
                {
                    var fpsFromVideoInfo = GetPropertyDouble(videoInfo, "FPS");
                    if (fpsFromVideoInfo > 0)
                        return fpsFromVideoInfo;
                }

                var fps = GetPropertyDouble(timeline, "FPS");
                if (fps > 0)
                    return fps;

                fps = GetPropertyDouble(timeline, "FrameRate");
                if (fps > 0)
                    return fps;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TimelineAudioMetricsService] GetTimelineFps failed: {ex}");
            }

            return fallbackFps;
        }

        private static double GetPropertyDouble(object instance, string propertyName)
        {
            var property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property is null)
                return 0;

            var value = property.GetValue(instance);
            return value switch
            {
                double d => d,
                float f => f,
                decimal m => (double)m,
                int i => i,
                long l => l,
                string s when double.TryParse(s, out var parsed) => parsed,
                _ => 0
            };
        }
    }
}
