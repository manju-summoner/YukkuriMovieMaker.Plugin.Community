using System.IO;
using YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Interfaces;
using YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Models;

namespace YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Services;

internal sealed class SoundFontResolverService : ISoundFontProvider
{
    private readonly SoundFontSettings _settings;
    private readonly string _soundFontDirectory;

    public SoundFontResolverService(SoundFontSettings settings)
    {
        _settings = settings;
        _soundFontDirectory = SoundFontDownloadService.SoundFontDirectory;
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

    private IReadOnlyList<string> GetInstalledFiles() =>
        Directory.Exists(_soundFontDirectory)
            ? Directory.GetFiles(_soundFontDirectory, "*.sf2", SearchOption.TopDirectoryOnly)
            : [];
}
