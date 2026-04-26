using System.IO;
using System.IO.Compression;
using System.Reflection;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Services;

internal sealed class SoundFontDownloadService
{
    private const string ReleaseBaseUrl = "https://github.com/routersys/YMM4-SoundFonts/releases/latest/download/";
    private static readonly string SoundFontDir = Path.Combine(
        AppDirectories.UserDirectory, "resources", "soundFonts");

    public static string SoundFontDirectory => SoundFontDir;

    public static void EnsureDirectory() => Directory.CreateDirectory(SoundFontDir);

    public async Task<bool> DownloadAsync(string zipFileName, ProgressMessage progress, CancellationToken cancellationToken = default)
    {
        EnsureDirectory();
        var url = ReleaseBaseUrl + zipFileName;
        var zipPath = Path.Combine(SoundFontDir, zipFileName);

        await Downloader.DownloadAsync(url, zipPath, progress, cancellationToken);

        ExtractZip(zipPath);
        File.Delete(zipPath);
        return true;
    }

    private static void ExtractZip(string zipPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue;
            var destPath = Path.Combine(SoundFontDir, entry.Name);
            if (!File.Exists(destPath))
                entry.ExtractToFile(destPath, overwrite: false);
        }
    }

    public static IReadOnlyList<string> GetInstalledSoundFonts() =>
        Directory.Exists(SoundFontDir)
            ? Directory.GetFiles(SoundFontDir, "*.sf2", SearchOption.TopDirectoryOnly)
            : [];
}
