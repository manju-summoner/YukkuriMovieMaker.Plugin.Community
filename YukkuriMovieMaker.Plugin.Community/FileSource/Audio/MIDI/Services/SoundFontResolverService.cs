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

    public IReadOnlyList<string> GetActiveSoundFontPaths()
    {
        if (!_settings.EnableSoundFont) return [];

        var result = new List<string>();
        var installedFiles = GetInstalledFiles();

        foreach (var layer in _settings.Layers.Where(l => l.IsEnabled && !string.IsNullOrEmpty(l.FileName)))
        {
            var match = installedFiles.FirstOrDefault(f =>
                Path.GetFileName(f).Equals(layer.FileName, StringComparison.OrdinalIgnoreCase));
            if (match is not null && !result.Contains(match))
                result.Add(match);
        }

        if (result.Count == 0 && installedFiles.Count > 0)
            result.Add(installedFiles.OrderByDescending(f => new FileInfo(f).Length).First());

        return result;
    }

    public bool HasAnySoundFont() =>
        _settings.EnableSoundFont && GetInstalledFiles().Count > 0;

    private IReadOnlyList<string> GetInstalledFiles() =>
        Directory.Exists(_soundFontDirectory)
            ? Directory.GetFiles(_soundFontDirectory, "*.sf2", SearchOption.TopDirectoryOnly)
            : [];
}
