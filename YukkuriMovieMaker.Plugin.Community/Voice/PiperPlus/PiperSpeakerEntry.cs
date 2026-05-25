namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus;

internal sealed class PiperSpeakerEntry
{
    public string ModelPath { get; init; } = string.Empty;
    public string ConfigPath { get; init; } = string.Empty;
    public string ModelName { get; init; } = string.Empty;
    public int SpeakerId { get; init; }
    public string SpeakerName { get; init; } = string.Empty;
    public bool IsMultiSpeaker { get; init; }
    public string LanguageArgument { get; init; } = string.Empty;

    public string UniqueId => $"{ModelPath}::{SpeakerId}";

    public string DisplayName => IsMultiSpeaker
        ? $"{ModelName} / {SpeakerName}"
        : ModelName;
}
