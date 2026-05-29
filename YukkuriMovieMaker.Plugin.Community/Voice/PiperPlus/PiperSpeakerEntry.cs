using System.IO;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus;

internal sealed class PiperSpeakerEntry
{
    public string ModelPath { get; init; } = string.Empty;
    public string ConfigPath { get; init; } = string.Empty;
    public int SpeakerId { get; init; }
    public string SpeakerName { get; init; } = string.Empty;
    public bool IsMultiSpeaker { get; init; }

    public string UniqueId =>
        $"{Uri.EscapeDataString(Path.GetRelativePath(PiperPlusPaths.ModelDirectory, ModelPath))}::{SpeakerId}";

    public string DisplayName
    {
        get
        {
            var modelName = Path.GetFileNameWithoutExtension(ModelPath);
            return IsMultiSpeaker ? $"{modelName} / {SpeakerName}" : modelName;
        }
    }
}
