using System.Threading.Tasks;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services
{
    internal class ToolRecordingStopWorkflowService
    {
        private readonly RecordingService recordingService;
        private readonly TimelineInsertService timelineInsertService;

        public ToolRecordingStopWorkflowService(RecordingService recordingService, TimelineInsertService timelineInsertService)
        {
            this.recordingService = recordingService;
            this.timelineInsertService = timelineInsertService;
        }

        public async Task<ToolRecordingStopResult> ExecuteAsync()
        {
            var recordedFile = await recordingService.StopRecordingAsync();
            if (recordedFile is null)
                return ToolRecordingStopResult.NoData();

            if (recordedFile.DataLength <= 0)
                return ToolRecordingStopResult.ZeroData(recordedFile.FilePath);

            await timelineInsertService.InsertAsync(recordedFile);
            return ToolRecordingStopResult.Inserted(recordedFile.FilePath);
        }
    }

    internal class ToolRecordingStopResult
    {
        public bool HasData { get; private set; }
        public bool HasZeroLengthData { get; private set; }
        public string? FilePath { get; private set; }

        private ToolRecordingStopResult()
        {
        }

        public static ToolRecordingStopResult NoData() => new();

        public static ToolRecordingStopResult ZeroData(string filePath) => new()
        {
            HasData = true,
            HasZeroLengthData = true,
            FilePath = filePath
        };

        public static ToolRecordingStopResult Inserted(string filePath) => new()
        {
            HasData = true,
            FilePath = filePath
        };
    }
}
