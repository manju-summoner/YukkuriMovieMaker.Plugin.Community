using System.IO;
using YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.API;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus;

internal static class PiperModelScanner
{
    static readonly EnumerationOptions EnumerationOptions = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        MatchType = MatchType.Simple,
        AttributesToSkip = FileAttributes.System | FileAttributes.ReparsePoint,
    };

    public static IReadOnlyList<PiperModelInfo> Scan(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return [];

        var results = new List<PiperModelInfo>();

        foreach (var onnxPath in Directory.EnumerateFiles(directory, "*.onnx", EnumerationOptions))
        {
            var jsonPath = onnxPath + ".json";
            if (!File.Exists(jsonPath))
                continue;

            var info = TryLoad(onnxPath, jsonPath);
            if (info is not null)
                results.Add(info);
        }

        return results;
    }

    static PiperModelInfo? TryLoad(string onnxPath, string jsonPath)
    {
        try
        {
            var json = File.ReadAllText(jsonPath);
            var config = Json.Json.LoadFromText<PiperModelConfig>(json);
            if (config is null)
                return null;

            var modelName = Path.GetFileNameWithoutExtension(onnxPath);
            var speakerIdMap = config.SpeakerIdMap ?? new Dictionary<string, int>();
            var numSpeakers = config.NumSpeakers > 0 ? config.NumSpeakers : 1;
            var languageCodes = ResolveLanguageCodes(config);

            return new PiperModelInfo(onnxPath, jsonPath, modelName, numSpeakers, speakerIdMap, languageCodes);
        }
        catch
        {
            return null;
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
