using System;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services
{
    internal class ToolRecordingStartWorkflowService
    {
        private readonly RecordingService recordingService;

        public ToolRecordingStartWorkflowService(RecordingService recordingService)
        {
            this.recordingService = recordingService;
        }

        public ToolRecordingStartResult Execute(string? selectedDeviceId)
        {
            if (string.IsNullOrWhiteSpace(selectedDeviceId))
                return ToolRecordingStartResult.Failed(Texts.ReselectRecordingDevice);

            recordingService.StartRecording(selectedDeviceId);
            return ToolRecordingStartResult.Succeeded();
        }
    }

    internal class ToolRecordingStartResult
    {
        public bool IsSuccess { get; private set; }
        public string? ErrorMessage { get; private set; }

        private ToolRecordingStartResult()
        {
        }

        public static ToolRecordingStartResult Succeeded() => new()
        {
            IsSuccess = true
        };

        public static ToolRecordingStartResult Failed(string message) => new()
        {
            IsSuccess = false,
            ErrorMessage = message
        };
    }
}
