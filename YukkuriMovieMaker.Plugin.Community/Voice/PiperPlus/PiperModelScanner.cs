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
            var jsonPath = ResolveJsonPath(onnxPath);
            if (jsonPath is null)
                continue;

            if (PiperModelInfo.TryLoad(onnxPath, jsonPath, out var info))
                results.Add(info);
        }

        return results;
    }

    static string? ResolveJsonPath(string onnxPath)
    {
        // 1. model.onnx.json
        var canonicalJsonPath = onnxPath + ".json";
        if (File.Exists(canonicalJsonPath))
            return canonicalJsonPath;

        // 2. model.json
        var modelJsonPath = Path.ChangeExtension(onnxPath, ".json");
        if (File.Exists(modelJsonPath))
            return modelJsonPath;

        // 3. config.json
        var folder = Path.GetDirectoryName(onnxPath);
        if (folder is null)
            return null;

        var configJsonPath = Path.Combine(folder, "config.json");
        if (File.Exists(configJsonPath))
            return configJsonPath;

        return null;
    }
}
