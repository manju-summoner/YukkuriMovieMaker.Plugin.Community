using System.Collections.Frozen;
using System.IO;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Localization;
using YukkuriMovieMaker.Plugin.FileSource;

namespace YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI;

public sealed class MidiAudioSourcePlugin : IAudioFileSourcePlugin
{
    private static readonly FrozenSet<string> SupportedExtensions =
        new[] { ".mid", ".midi" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public string Name => Texts.PluginName;

    public IAudioFileSource? CreateAudioFileSource(string filePath, int audioTrackIndex)
    {
        if (!IsSupportedFile(filePath)) return null;
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
