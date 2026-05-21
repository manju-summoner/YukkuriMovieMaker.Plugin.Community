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

                throw new InvalidOperationException(Texts.TimelineUnavailable);
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
    }
}
