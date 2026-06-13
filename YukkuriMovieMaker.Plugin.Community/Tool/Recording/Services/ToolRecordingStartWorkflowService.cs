using System;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services
{
    internal class ToolRecordingStartWorkflowService(RecordingService recordingService)
    {
        private readonly RecordingService recordingService = recordingService;

        public ToolRecordingStartResult Execute(string? selectedDeviceId)
        {
            if (string.IsNullOrWhiteSpace(selectedDeviceId))
                return ToolRecordingStartResult.Failed(Texts.ReselectRecordingDevice);

            var selection = recordingService.StartRecording(selectedDeviceId);
            return ToolRecordingStartResult.Succeeded(selection);
        }
    }

    internal class ToolRecordingStartResult
    {
        public bool IsSuccess { get; private set; }
        public string? ErrorMessage { get; private set; }
        public RecordingStartDeviceSelection Selection { get; private set; }

        private ToolRecordingStartResult()
        {
        }

        public static ToolRecordingStartResult Succeeded(RecordingStartDeviceSelection selection) => new()
        {
            IsSuccess = true,
            Selection = selection
        };

        public static ToolRecordingStartResult Failed(string message) => new()
        {
            IsSuccess = false,
            ErrorMessage = message
        };
    }
}
