namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Models
{
    public sealed class RecordingDeviceInfo
    {
        public string Id { get; init; } = string.Empty;
        public string FriendlyName { get; init; } = string.Empty;
        public bool IsDefault { get; init; }
        public string? ResolvedDeviceName { get; init; }
    }
}
