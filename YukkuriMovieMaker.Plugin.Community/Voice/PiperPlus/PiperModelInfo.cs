using System.IO;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.API;

internal record PiperModelInfo(
    string ModelPath,
    string ConfigPath,
    int NumSpeakers,
    IReadOnlyDictionary<string, int> SpeakerIdMap,
    IReadOnlyList<string> LanguageCodes
)
{
    public string ModelName => Path.GetFileNameWithoutExtension(ModelPath);

    public bool IsMultiSpeaker => NumSpeakers > 1;

    public string LanguageArgument =>
        LanguageCodes.Count > 0
            ? string.Join("-", LanguageCodes)
            : string.Empty;
}
