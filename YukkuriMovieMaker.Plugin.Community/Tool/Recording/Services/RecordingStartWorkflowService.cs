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

        public RecordingStartResult Execute(string scriptText)
        {
            var deviceName = recordingService.GetAvailableDeviceNames().FirstOrDefault();
            if (string.IsNullOrWhiteSpace(deviceName))
                return RecordingStartResult.Failed("録音デバイスが見つかりません。");

            if (string.IsNullOrWhiteSpace(scriptText))
                return RecordingStartResult.Failed("セリフが選択されていません。タイムラインのセリフを選択してください。");

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
