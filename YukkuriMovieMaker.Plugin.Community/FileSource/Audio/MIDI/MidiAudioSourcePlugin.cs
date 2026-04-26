using System.Collections.Frozen;
using System.IO;
using System.Windows;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Localization;
using YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Services;
using YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Views;
using YukkuriMovieMaker.Plugin.FileSource;

namespace YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI;

public sealed class MidiAudioSourcePlugin : IAudioFileSourcePlugin
{
    private const string DefaultSoundFontFileName = "GeneralUser-GS.sf2";
    private static readonly Lock DownloadPromptLock = new();
    private static readonly FrozenSet<string> SupportedExtensions =
        new[] { ".mid", ".midi" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public string Name => Texts.PluginName;

    public MidiAudioSourcePlugin()
    {
        SoundFontDownloadService.EnsureDirectory();
    }

    public IAudioFileSource? CreateAudioFileSource(string filePath, int audioTrackIndex)
    {
        if (!IsSupportedFile(filePath)) return null;
        TryPromptDownloadOnFirstUse();
        var source = new MidiAudioSource(filePath, MidiPluginSettings.Default);
        if (source.IsReadable) 
            return source;

        if (source.LoadError is { } ex)
            Log.Default.Write($"{Texts.MidiParseError} path={filePath}", ex);
        else
            Log.Default.Write($"{Texts.MidiParseError} path={filePath}");
        source.Dispose();

        return null;
    }

    private static void TryPromptDownloadOnFirstUse()
    {
        var sf = MidiPluginSettings.Default.SoundFont;
        if (sf.HasShownDownloadPrompt) return;

        lock (DownloadPromptLock)
        {
            if (sf.HasShownDownloadPrompt) return;
            if (SoundFontDownloadService.GetInstalledSoundFonts().Count > 0) return;
            if (!sf.EnableSoundFont) return;

            Application.Current?.Dispatcher.Invoke(() =>
            {
                var result = MessageBox.Show(
                    $"{Texts.DownloadSoundFont}\n\n({DefaultSoundFontFileName})",
                    Texts.PluginName,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                sf.HasShownDownloadPrompt = true;
                MidiPluginSettings.Default.Save();

                if (result == MessageBoxResult.Yes)
                {
                    new SoundFontDownloadDialog().ShowDialog();
                }
            });
        }
    }

    private static bool IsSupportedFile(string filePath)
    {
        if (!SupportedExtensions.Contains(Path.GetExtension(filePath))) return false;
        if (!File.Exists(filePath)) return false;
        return HasValidMidiHeader(filePath);
    }

    private static bool HasValidMidiHeader(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            Span<byte> header = stackalloc byte[4];
            if (stream.Read(header) < 4) return false;
            return header.SequenceEqual("MThd"u8);
        }
        catch (Exception ex)
        {
            Log.Default.Write($"{Texts.MidiHeaderValidationError} path={filePath}", ex);
            return false;
        }
    }
}
