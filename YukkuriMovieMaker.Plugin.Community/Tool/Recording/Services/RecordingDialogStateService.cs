namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services
{
    internal static class RecordingDialogStateService
    {
        public static bool CanStartRecording(RecordingDialogState state, string scriptText)
            => state != RecordingDialogState.Recording && !string.IsNullOrWhiteSpace(scriptText);

        public static bool CanStopRecording(RecordingDialogState state)
            => state == RecordingDialogState.Recording;

        public static bool CanAddToTimeline(RecordingDialogState state)
            => state == RecordingDialogState.Recorded;

        public static bool CanRegenerate(RecordingDialogState state, string scriptText)
            => state != RecordingDialogState.Recording && !string.IsNullOrWhiteSpace(scriptText);

        public static bool CanPlay(RecordingDialogState state, bool isPlaying, bool hasAudioFile)
            => state != RecordingDialogState.Recording && !isPlaying && hasAudioFile;

        public static bool CanChangeOutputDirectory(RecordingDialogState state)
            => state != RecordingDialogState.Recording;

        public static RecordingDialogState ToIdle() => RecordingDialogState.Idle;
        public static RecordingDialogState ToRecording() => RecordingDialogState.Recording;
        public static RecordingDialogState ToRecorded() => RecordingDialogState.Recorded;
    }
}
