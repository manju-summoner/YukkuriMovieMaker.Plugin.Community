namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.API;

internal record PiperModelInfo(
    string ModelPath,
    string ConfigPath,
    string ModelName,
    int NumSpeakers,
    IReadOnlyDictionary<string, int> SpeakerIdMap,
    IReadOnlyList<string> LanguageCodes
)
{
    public bool IsMultiSpeaker => NumSpeakers > 1;

    public bool IsMultilingual => LanguageCodes.Count > 1;

    public string LanguageArgument =>
        LanguageCodes.Count > 0
            ? string.Join("-", LanguageCodes)
            : string.Empty;
}
