namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.API;

internal record PiperModelInfo(
    string ModelPath,
    string ConfigPath,
    string ModelName,
    int NumSpeakers,
    IReadOnlyDictionary<string, int> SpeakerIdMap
)
{
    public bool IsMultiSpeaker => NumSpeakers > 1;
}
