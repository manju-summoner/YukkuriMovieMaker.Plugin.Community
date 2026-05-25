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
            var canonicalJsonPath = onnxPath + ".json";

            if (!File.Exists(canonicalJsonPath))
            {
                var resolved = TryAutoRenameJson(onnxPath);
                if (resolved is null)
                    continue;
            }

            var info = TryLoad(onnxPath, canonicalJsonPath);
            if (info is not null)
                results.Add(info);
        }

        return results;
    }

    static string? TryAutoRenameJson(string onnxPath)
    {
        var folder = Path.GetDirectoryName(onnxPath);
        if (folder is null)
            return null;

        var folderOnnxFiles = Directory
            .GetFiles(folder, "*.onnx", SearchOption.TopDirectoryOnly);
        var folderJsonFiles = Directory
            .GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly)
            .Where(j => !j.EndsWith(".onnx.json", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (folderOnnxFiles.Length != 1 || folderJsonFiles.Count != 1)
            return null;

        var singleOnnx = folderOnnxFiles[0];
        var singleJson = folderJsonFiles[0];

        if (!string.Equals(singleOnnx, onnxPath, StringComparison.OrdinalIgnoreCase))
            return null;

        var targetJsonPath = singleOnnx + ".json";

        try
        {
            File.Move(singleJson, targetJsonPath);
            return targetJsonPath;
        }
        catch
        {
            return null;
        }
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
