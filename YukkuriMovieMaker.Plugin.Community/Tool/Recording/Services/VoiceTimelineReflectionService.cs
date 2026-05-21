using System;
using System.IO;
using System.Reflection;
using NAudio.Wave;
using YukkuriMovieMaker.Plugin.Community.Tool.Recording.Models;
using YukkuriMovieMaker.Plugin.Community.Voice.Recording;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services
{
    internal class VoiceTimelineReflectionService
    {
        public object? GetActiveTimeline(object mainViewModel)
        {
            var mainViewModelType = mainViewModel.GetType();

            var activeTimelineViewModel = mainViewModelType
                .GetProperty("ActiveTimelineViewModel", BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(mainViewModel);

            if (activeTimelineViewModel is not null)
            {
                var timelineField = activeTimelineViewModel.GetType()
                    .GetField("timeline", BindingFlags.Instance | BindingFlags.NonPublic);

                if (timelineField?.GetValue(activeTimelineViewModel) is { } timeline)
                    return timeline;
            }

            var modelField = mainViewModelType.GetField("model", BindingFlags.Instance | BindingFlags.NonPublic);
            var model = modelField?.GetValue(mainViewModel);
            return model?.GetType()
                .GetProperty("Timeline", BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(model);
        }

        public int GetCurrentFrame(object timeline)
        {
            return (int)(timeline.GetType()
                .GetProperty("CurrentFrame", BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(timeline)
                ?? throw new InvalidOperationException("現在フレームを取得できません。"));
        }

        public int GetLengthFrames(object timeline, string filePath)
        {
            var fps = GetTimelineFps(timeline, fallbackFps: 60.0);
            var durationSeconds = GetAudioDuration(filePath).TotalSeconds;
            var frames = (int)Math.Round(durationSeconds * fps, MidpointRounding.AwayFromZero);
            return Math.Max(1, frames);
        }

        public object CreateVoiceItemViaReflection(RecordingScriptItem item, int frame, int length, int layer)
        {
            var voiceItemType = Type.GetType("YukkuriMovieMaker.Project.Items.VoiceItem, YukkuriMovieMaker")
                ?? throw new InvalidOperationException("VoiceItem を取得できません。");

            var parameter = new RecordedVoiceParameter
            {
                Text = item.Text,
                AudioFilePath = item.AudioFilePath,
                Duration = item.Duration,
                CreatedAt = item.CreatedAt
            };

            var characterType = Type.GetType("YukkuriMovieMaker.Project.Character, YukkuriMovieMaker")
                ?? throw new InvalidOperationException("Character を取得できません。");
            var character = Activator.CreateInstance(characterType)
                ?? throw new InvalidOperationException("Character を作成できません。");

            var voiceDescriptionType = Type.GetType("YukkuriMovieMaker.Plugin.Voice.VoiceDescription, YukkuriMovieMaker.Plugin")
                ?? throw new InvalidOperationException("VoiceDescription を取得できません。");
            var speaker = RecordedVoiceSpeaker.Instance;
            var voiceDescription = Activator.CreateInstance(voiceDescriptionType, speaker)
                ?? throw new InvalidOperationException("VoiceDescription を作成できません。");
            var apiProp = voiceDescriptionType.GetProperty("API");
            if (apiProp?.CanWrite == true)
                apiProp.SetValue(voiceDescription, RecordedVoiceSpeaker.ApiName);
            var argProp = voiceDescriptionType.GetProperty("Arg");
            if (argProp?.CanWrite == true)
                argProp.SetValue(voiceDescription, RecordedVoiceSpeaker.SpeakerId);

            characterType.GetProperty("Voice")?.SetValue(character, voiceDescription);
            characterType.GetProperty("VoiceParameter")?.SetValue(character, parameter.Clone());

            var voiceItem = Activator.CreateInstance(voiceItemType, character)
                ?? throw new InvalidOperationException("VoiceItem を作成できません。");

            voiceItemType.GetProperty("Serif")?.SetValue(voiceItem, item.Text);
            voiceItemType.GetProperty("VoiceParameter")?.SetValue(voiceItem, parameter);
            voiceItemType.GetProperty("Frame")?.SetValue(voiceItem, frame);
            voiceItemType.GetProperty("Layer")?.SetValue(voiceItem, layer);
            voiceItemType.GetProperty("Length")?.SetValue(voiceItem, length);
            var voiceLengthProp = voiceItemType.GetProperty("VoiceLength");
            if (voiceLengthProp?.CanWrite == true)
                voiceLengthProp.SetValue(voiceItem, item.Duration ?? GetAudioDuration(item.AudioFilePath));

            return voiceItem;
        }

        public void TryAddItem(object timeline, object voiceItem, int frame, int layer)
        {
            var timelineType = timeline.GetType();
            var itemInterfaceType = timelineType.Assembly.GetType("YukkuriMovieMaker.Project.Items.IItem")
                ?? throw new InvalidOperationException("IItem を取得できません。");

            var itemArray = Array.CreateInstance(itemInterfaceType, 1);
            itemArray.SetValue(voiceItem, 0);

            var tryAddItems = timelineType.GetMethod(
                "TryAddItems",
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: new[] { itemArray.GetType(), typeof(int), typeof(int) },
                modifiers: null);

            if (tryAddItems is not null)
            {
                var added = (bool)tryAddItems.Invoke(timeline, new object[] { itemArray, frame, layer })!;
                if (!added)
                    throw new InvalidOperationException("タイムラインへの追加に失敗しました。");
                return;
            }

            var addItems = timelineType.GetMethod("AddItems", BindingFlags.Instance | BindingFlags.Public);
            if (addItems is null)
                throw new InvalidOperationException("タイムライン追加メソッドを取得できません。");

            addItems.Invoke(timeline, new object[] { itemArray });
        }

        private static TimeSpan GetAudioDuration(string filePath)
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new WaveFileReader(stream);
            return reader.TotalTime;
        }

        private static double GetTimelineFps(object timeline, double fallbackFps)
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
            catch
            {
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
