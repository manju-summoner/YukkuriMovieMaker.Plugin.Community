using System.Diagnostics.CodeAnalysis;
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

    /// <summary>
    /// onnx パスと config パスから <see cref="PiperModelInfo"/> を組み立てる。
    /// config の読み込み・パースに成功した場合のみ true を返す。
    /// </summary>
    public static bool TryLoad(string onnxPath, string configPath, [NotNullWhen(true)] out PiperModelInfo? modelInfo)
    {
        modelInfo = null;
        try
        {
            var json = File.ReadAllText(configPath);
            var config = Json.Json.LoadFromText<PiperModelConfig>(json);
            if (config is null)
                return false;

            var speakerIdMap = config.SpeakerIdMap ?? new Dictionary<string, int>();
            var numSpeakers = config.NumSpeakers > 0 ? config.NumSpeakers : 1;
            var languageCodes = ResolveLanguageCodes(config);

            modelInfo = new PiperModelInfo(onnxPath, configPath, numSpeakers, speakerIdMap, languageCodes);
            return true;
        }
        catch
        {
            return false;
        }
    }

    static IReadOnlyList<string> ResolveLanguageCodes(PiperModelConfig config)
    {
        if (config.LanguageIdMap is { Count: > 0 })
            return config.LanguageIdMap
                .OrderBy(kv => kv.Value)
                .Select(kv => kv.Key)
                .ToList();

        if (config.Language?.Code is { } code && !string.IsNullOrEmpty(code))
            return [code];

        return [];
    }
}
