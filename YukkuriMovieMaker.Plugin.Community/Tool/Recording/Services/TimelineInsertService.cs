using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using YukkuriMovieMaker.Plugin.Community.Tool.Recording.Models;
using YukkuriMovieMaker.Project;
using YukkuriMovieMaker.Project.Items;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services
{
    public class TimelineInsertService
    {
        private readonly TimelineAudioMetricsService timelineAudioMetricsService = new();

        public Task InsertAsync(RecordedFileInfo recordedFile)
        {
            if (recordedFile is null)
                throw new ArgumentNullException(nameof(recordedFile));

            if (!File.Exists(recordedFile.FilePath))
                throw new FileNotFoundException(Texts.RecordedWavNotFound, recordedFile.FilePath);

            var dispatcher = Application.Current?.Dispatcher
                ?? throw new InvalidOperationException(Texts.UiDispatcherUnavailable);

            return dispatcher.InvokeAsync(() =>
            {
                var timeline = ToolViewModel.TimelineInstance;
                if (timeline is not null)
                {
                    InsertWithTimeline(timeline, recordedFile.FilePath);
                    return;
                }

                var mainViewModel = Application.Current?.MainWindow?.DataContext
                    ?? throw new InvalidOperationException(Texts.MainViewModelUnavailable);

                var fallbackTimeline = GetActiveTimeline(mainViewModel)
                    ?? throw new InvalidOperationException(Texts.TimelineUnavailable);

                var currentFrame = TimelineMemberReader.GetCurrentFrame(fallbackTimeline);
                if (currentFrame == int.MinValue)
                    throw new InvalidOperationException(Texts.CurrentFrameUnavailable);
                var length = timelineAudioMetricsService.GetLengthFrames(fallbackTimeline, recordedFile.FilePath);
                var audioItem = CreateAudioItem(recordedFile.FilePath, currentFrame, length);
                TryAddItem(fallbackTimeline, audioItem, currentFrame, length);
            }).Task;
        }

        private void InsertWithTimeline(Timeline timeline, string filePath)
        {
            var frame = timeline.CurrentFrame;
            var length = timelineAudioMetricsService.GetLengthFrames(timeline, filePath);
            var audioItem = new AudioItem(filePath)
            {
                Frame = frame,
                Layer = 0,
                Length = length
            };

            var added = timeline.TryAddItems(new IItem[] { audioItem }, audioItem.Frame, audioItem.Layer);
            if (!added)
                throw new InvalidOperationException(Texts.TimelineAddFailedMessage);
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

        private static object CreateAudioItem(string filePath, int frame, int length)
        {
            var audioItemType = Type.GetType("YukkuriMovieMaker.Project.Items.AudioItem, YukkuriMovieMaker")
                ?? throw new InvalidOperationException(Texts.AudioItemUnavailable);

            object audioItem;

            var stringCtor = audioItemType.GetConstructor(new[] { typeof(string) });
            if (stringCtor is not null)
            {
                audioItem = stringCtor.Invoke(new object[] { filePath });
            }
            else
            {
                audioItem = Activator.CreateInstance(audioItemType)
                    ?? throw new InvalidOperationException(Texts.AudioItemCannotSetup);
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
                ?? throw new InvalidOperationException(Texts.IItemUnavailable);

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
                    throw new InvalidOperationException(Texts.TimelineAddFailedMessage);
                return;
            }

            var addItems = timelineType.GetMethod("AddItems", BindingFlags.Instance | BindingFlags.Public);
            if (addItems is null)
                throw new InvalidOperationException(Texts.TimelineAddMethodUnavailable);

            addItems.Invoke(timeline, new object[] { itemArray });
        }
    }
}
