using System;
using System.Threading.Tasks;
using YukkuriMovieMaker.Plugin.Community.Tool.Recording.Models;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services
{
    internal class RecordingStopWorkflowService
    {
        private readonly RecordingService recordingService;
        private readonly RecordingScriptItemService recordingScriptItemService;
        private readonly VoiceTimelineInsertService voiceTimelineInsertService;
        private readonly TimelineSelectionService timelineSelectionService;
        private readonly Func<string, TimeSpan> audioDurationResolver;

        public RecordingStopWorkflowService(
            RecordingService recordingService,
            RecordingScriptItemService recordingScriptItemService,
            VoiceTimelineInsertService voiceTimelineInsertService,
            TimelineSelectionService timelineSelectionService,
            Func<string, TimeSpan> audioDurationResolver)
        {
            this.recordingService = recordingService;
            this.recordingScriptItemService = recordingScriptItemService;
            this.voiceTimelineInsertService = voiceTimelineInsertService;
            this.timelineSelectionService = timelineSelectionService;
            this.audioDurationResolver = audioDurationResolver;
        }

        public async Task<RecordingStopResult> ExecuteAsync(RecordingScriptItem scriptItem, string scriptText)
        {
            var recordedFile = await recordingService.StopRecordingAsync();
            if (recordedFile is null || recordedFile.DataLength <= 0)
                return RecordingStopResult.NoData();

            recordingScriptItemService.ApplyRecorded(
                scriptItem,
                scriptText,
                recordedFile.FilePath,
                audioDurationResolver(recordedFile.FilePath),
                DateTime.Now);

            await voiceTimelineInsertService.InsertAsync(scriptItem);

            if (timelineSelectionService.TryMoveToNextSerif(scriptItem.Text, out var nextSerif) && !string.IsNullOrWhiteSpace(nextSerif))
                return RecordingStopResult.NextSerifPrepared(nextSerif, recordedFile.FilePath);

            return RecordingStopResult.Inserted(recordedFile.FilePath);
        }
    }

    internal class RecordingStopResult
    {
        public bool HasData { get; private set; }
        public string? FilePath { get; private set; }
        public string? NextSerif { get; private set; }

        private RecordingStopResult()
        {
        }

        public static RecordingStopResult NoData() => new();

        public static RecordingStopResult Inserted(string filePath) => new()
        {
            HasData = true,
            FilePath = filePath
        };

        public static RecordingStopResult NextSerifPrepared(string nextSerif, string filePath) => new()
        {
            HasData = true,
            FilePath = filePath,
            NextSerif = nextSerif
        };
    }
}
