namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.API;

internal record PiperModelInfo(
    string ModelPath,
    string ConfigPath,
    string ModelName,
    int NumSpeakers,
    IReadOnlyDictionary<string, int> SpeakerIdMap,
    IReadOnlyDictionary<string, int> LanguageIdMap
)
{
    public bool IsMultiSpeaker => NumSpeakers > 1;
    public bool IsMultilingual => LanguageIdMap.Count > 1;

    public string LanguageArgument =>
        IsMultilingual
            ? string.Join("-", LanguageIdMap.OrderBy(kv => kv.Value).Select(kv => kv.Key))
            : string.Empty;
}
