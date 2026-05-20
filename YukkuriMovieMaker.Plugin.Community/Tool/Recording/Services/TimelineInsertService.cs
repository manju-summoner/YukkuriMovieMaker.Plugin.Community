using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using NAudio.Wave;
using YukkuriMovieMaker.Plugin.Community.Tool.Recording.Models;
using YukkuriMovieMaker.Project;
using YukkuriMovieMaker.Project.Items;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services
{
    public class TimelineInsertService
    {
        public Task InsertAsync(RecordedFileInfo recordedFile)
        {
            if (recordedFile is null)
                throw new ArgumentNullException(nameof(recordedFile));

            if (!File.Exists(recordedFile.FilePath))
                throw new FileNotFoundException("録音済み wav ファイルが見つかりません。", recordedFile.FilePath);

            var dispatcher = Application.Current?.Dispatcher
                ?? throw new InvalidOperationException("UI Dispatcher を取得できません。");

            return dispatcher.InvokeAsync(() =>
            {
                var timeline = ToolViewModel.TimelineInstance;
                if (timeline is not null)
                {
                    InsertWithTimeline(timeline, recordedFile.FilePath);
                    return;
                }

                var mainViewModel = Application.Current?.MainWindow?.DataContext
                    ?? throw new InvalidOperationException("MainViewModel を取得できません。");

                var fallbackTimeline = GetActiveTimeline(mainViewModel)
                    ?? throw new InvalidOperationException("タイムラインを取得できません。");

                var currentFrame = GetCurrentFrame(fallbackTimeline);
                var length = GetLengthFrames(fallbackTimeline, recordedFile.FilePath);
                var audioItem = CreateAudioItem(recordedFile.FilePath, currentFrame, length);
                TryAddItem(fallbackTimeline, audioItem, currentFrame, length);
            }).Task;
        }

        private static void InsertWithTimeline(Timeline timeline, string filePath)
        {
            var frame = timeline.CurrentFrame;
            var length = GetLengthFrames(timeline, filePath);
            var audioItem = new AudioItem(filePath)
            {
                Frame = frame,
                Layer = 0,
                Length = length
            };

            var added = timeline.TryAddItems(new IItem[] { audioItem }, audioItem.Frame, audioItem.Layer);
            if (!added)
                throw new InvalidOperationException("タイムラインへの追加に失敗しました。");
        }

        private static int GetLengthFrames(object timeline, string filePath)
        {
            var fps = GetTimelineFps(timeline, fallbackFps: 60.0);
            var durationSeconds = GetAudioDurationSeconds(filePath);
            var frames = (int)Math.Round(durationSeconds * fps, MidpointRounding.AwayFromZero);
            return Math.Max(1, frames);
        }

        private static double GetAudioDurationSeconds(string filePath)
        {
            using var reader = new WaveFileReader(filePath);
            return reader.TotalTime.TotalSeconds;
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

        private static object? GetActiveTimeline(object mainViewModel)
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

        private static int GetCurrentFrame(object timeline)
        {
            return (int)(timeline.GetType()
                .GetProperty("CurrentFrame", BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(timeline)
                ?? throw new InvalidOperationException("現在フレームを取得できません。"));
        }

        private static object CreateAudioItem(string filePath, int frame, int length)
        {
            var audioItemType = Type.GetType("YukkuriMovieMaker.Project.Items.AudioItem, YukkuriMovieMaker")
                ?? throw new InvalidOperationException("AudioItem を取得できません。");

            object audioItem;

            var stringCtor = audioItemType.GetConstructor(new[] { typeof(string) });
            if (stringCtor is not null)
            {
                audioItem = stringCtor.Invoke(new object[] { filePath });
            }
            else
            {
                audioItem = Activator.CreateInstance(audioItemType)
                    ?? throw new InvalidOperationException("AudioItem を設定できません。");
                audioItemType.GetProperty("FilePath")?.SetValue(audioItem, filePath);
            }

            audioItemType.GetProperty("Frame")?.SetValue(audioItem, frame);
            audioItemType.GetProperty("Layer")?.SetValue(audioItem, 0);
            audioItemType.GetProperty("Length")?.SetValue(audioItem, length);
            return audioItem;
        }

        private static void TryAddItem(object timeline, object audioItem, int frame, int length)
        {
            var timelineType = timeline.GetType();
            var itemInterfaceType = timelineType.Assembly.GetType("YukkuriMovieMaker.Project.Items.IItem")
                ?? throw new InvalidOperationException("IItem を取得できません。");

            var itemArray = Array.CreateInstance(itemInterfaceType, 1);
            itemArray.SetValue(audioItem, 0);

            var tryAddItems = timelineType.GetMethod(
                "TryAddItems",
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: new[] { itemArray.GetType(), typeof(int), typeof(int) },
                modifiers: null);

            if (tryAddItems is not null)
            {
                var added = (bool)tryAddItems.Invoke(timeline, new object[] { itemArray, frame, 0 })!;
                if (!added)
                    throw new InvalidOperationException("タイムラインへの追加に失敗しました。");
                return;
            }

            var addItems = timelineType.GetMethod("AddItems", BindingFlags.Instance | BindingFlags.Public);
            if (addItems is null)
                throw new InvalidOperationException("タイムライン追加メソッドを取得できません。");

            addItems.Invoke(timeline, new object[] { itemArray });
        }
    }
}



