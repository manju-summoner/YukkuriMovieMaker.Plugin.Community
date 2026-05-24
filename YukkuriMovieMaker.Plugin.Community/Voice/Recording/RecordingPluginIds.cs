namespace YukkuriMovieMaker.Plugin.Community.Voice.Recording
{
    internal static class RecordingPluginIds
    {
        public const string AuthorName = "CommunityRecording";
        public const string ContentId = "CommunityRecording";
        public const string ApiName = "CommunityRecording";

        // Canonical speaker id for new data.
        public const string SpeakerId = "MicRecording";

        // Backward-compatible ids used by older implementations.
        public const string LegacySpeakerIdTypo = "CommunitMicRecording";
        public const string LegacySpeakerId = "CommunityMicRecording";
    }
}
