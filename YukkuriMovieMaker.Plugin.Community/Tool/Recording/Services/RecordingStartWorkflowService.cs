using System.Linq;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services
{
    internal class RecordingStartWorkflowService
    {
        private readonly RecordingService recordingService;

        public RecordingStartWorkflowService(RecordingService recordingService)
        {
            this.recordingService = recordingService;
        }

        public RecordingStartResult Execute(string scriptText, string? deviceName)
        {
            if (string.IsNullOrWhiteSpace(deviceName))
                return RecordingStartResult.Failed(Texts.NoRecordingDeviceFound);

            if (string.IsNullOrWhiteSpace(scriptText))
                return RecordingStartResult.Failed(Texts.SelectSerifInTimeline);

            recordingService.StartRecording(deviceName);
            return RecordingStartResult.Succeeded();
        }
    }

    internal class RecordingStartResult
    {
        public bool IsSuccess { get; private set; }
        public string? ErrorMessage { get; private set; }

        private RecordingStartResult()
        {
        }

        public static RecordingStartResult Succeeded() => new()
        {
            IsSuccess = true
        };

        public static RecordingStartResult Failed(string message) => new()
        {
            IsSuccess = false,
            ErrorMessage = message
        };
    }
}
