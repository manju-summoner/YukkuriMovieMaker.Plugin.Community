using System;
using System.IO;
using NAudio.Wave;
using YukkuriMovieMaker.Plugin.Community.Tool.Recording.Models;
using YukkuriMovieMaker.Plugin.Community.Voice.Recording;
using YukkuriMovieMaker.Project;
using YukkuriMovieMaker.Project.Items;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services
{
    internal class VoiceTimelineDirectInsertService
    {
        public void Insert(Timeline timeline, RecordingScriptItem item, int? selectedFrame, int? selectedLayer)
        {
            var frame = selectedFrame ?? timeline.CurrentFrame;
            var layer = selectedLayer ?? 0;
            var length = GetLengthFrames(timeline, item.AudioFilePath);
            var voiceItem = CreateVoiceItem(item, frame, length, layer);

            var added = timeline.TryAddItems(new IItem[] { voiceItem }, voiceItem.Frame, voiceItem.Layer);
            if (!added)
                throw new InvalidOperationException("タイムラインへの追加に失敗しました。");
        }

        private static VoiceItem CreateVoiceItem(RecordingScriptItem item, int frame, int length, int layer)
        {
            var parameter = new RecordedVoiceParameter
            {
                Text = item.Text,
                AudioFilePath = item.AudioFilePath,
                Duration = item.Duration,
                CreatedAt = item.CreatedAt
            };

            var character = new Character
            {
                Voice = RecordedVoiceSpeaker.Description,
                VoiceParameter = parameter.Clone()
            };

            return new VoiceItem(character)
            {
                Serif = item.Text,
                VoiceParameter = parameter,
                Frame = frame,
                Layer = layer,
                Length = length
            };
        }

        private static TimeSpan GetAudioDuration(string filePath)
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new WaveFileReader(stream);
            return reader.TotalTime;
        }

        private static int GetLengthFrames(object timeline, string filePath)
        {
            var fps = GetTimelineFps(timeline, fallbackFps: 60.0);
            var durationSeconds = GetAudioDuration(filePath).TotalSeconds;
            var frames = (int)Math.Round(durationSeconds * fps, MidpointRounding.AwayFromZero);
            return Math.Max(1, frames);
        }

        private static double GetTimelineFps(object timeline, double fallbackFps)
        {
            try
            {
                var videoInfoProperty = timeline.GetType().GetProperty("VideoInfo", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
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
            catch
            {
            }

            return fallbackFps;
        }

        private static double GetPropertyDouble(object instance, string propertyName)
        {
            var property = instance.GetType().GetProperty(propertyName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
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
