using System.IO;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Interfaces;
using YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Models;

namespace YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Services;

internal sealed class SoundFontResolverService : ISoundFontProvider
{
    // 同名ファイルがあればユーザーフォルダ側を優先する（バンドル版を上書き可能にするため、ユーザーディレクトリを先に並べる）
    public static IReadOnlyList<string> SoundFontDirectories { get; } =
    [
        Path.Combine(AppDirectories.UserDirectory, "soundFonts"),
        Path.Combine(AppDirectories.ResourceDirectory, "SoundFonts"),
    ];

    public static IReadOnlyList<string> GetInstalledFiles()
    {
        var result = new List<string>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in SoundFontDirectories)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.GetFiles(dir, "*.sf2", SearchOption.TopDirectoryOnly))
            {
                if (seenNames.Add(Path.GetFileName(file)))
                    result.Add(file);
            }
        }
        return result;
    }

    private readonly SoundFontSettings _settings;

    public SoundFontResolverService(SoundFontSettings settings)
    {
        _settings = settings;
    }

    public IReadOnlyList<(string Path, float Volume)> GetActiveSoundFontPaths()
    {
        if (!_settings.EnableSoundFont) return [];

        var result = new List<(string Path, float Volume)>();
        var installedFiles = GetInstalledFiles();

        foreach (var layer in _settings.Layers.Where(l => l.IsEnabled && !string.IsNullOrEmpty(l.FileName)))
        {
            var match = installedFiles.FirstOrDefault(f =>
                Path.GetFileName(f).Equals(layer.FileName, StringComparison.OrdinalIgnoreCase));
            if (match is not null && !result.Any(r => r.Path == match))
                result.Add((match, layer.Volume));
        }

        if (result.Count == 0 && installedFiles.Count > 0)
            result.Add((installedFiles.OrderByDescending(f => new FileInfo(f).Length).First(), 1.0f));

        return result;
    }

    public bool HasAnySoundFont() =>
        _settings.EnableSoundFont && GetInstalledFiles().Count > 0;
}
