using MeltySynth;
using YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Interfaces;
using YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Models;
using YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Rendering;
using YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Services;
using YukkuriMovieMaker.Plugin.FileSource;

namespace YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI;

public sealed class MidiAudioSource : IAudioFileSource
{
    public static void ClearCache() => SoundFontRenderer.ClearCache();

    public TimeSpan Duration { get; }
    public int Hz => _sampleRate;

    private readonly int _sampleRate;
    private readonly IMidiRenderer _renderer;
    private long _position;
    private bool _disposed;

    internal MidiAudioSource(string filePath, MidiPluginSettings settings)
    {
        _sampleRate = settings.Audio.SampleRate;
        try
        {
            var mf = new MidiFile(filePath);
            Duration = mf.Length + TimeSpan.FromSeconds(2.0);
        }
        catch
        {
            Duration = TimeSpan.Zero;
        }
        _renderer = CreateRenderer(filePath, settings);
    }

    private static IMidiRenderer CreateRenderer(string filePath, MidiPluginSettings settings)
    {
        var sfProvider = new SoundFontResolverService(settings.SoundFont);
        IMidiRenderer baseRenderer = settings.Performance.RenderingMode switch
        {
            RenderingMode.SoundFont when sfProvider.HasAnySoundFont() => new SoundFontRenderer(filePath, sfProvider.GetActiveSoundFontPaths(), settings.Audio, settings.Performance),
            RenderingMode.SoundFont when settings.SoundFont.FallbackToSynthesis => new WaveformSynthesisRenderer(filePath, settings.Audio, settings.Performance),
            RenderingMode.SoundFont => new SilentRenderer(),
            _ => new WaveformSynthesisRenderer(filePath, settings.Audio, settings.Performance)
        };

        if (settings.Performance.EnableGpuAcceleration)
            return new GpuChunkedRenderer(baseRenderer, settings);

        return baseRenderer;
    }

    public int Read(float[] destBuffer, int offset, int count)
    {
        if (_disposed) return 0;

        var pos = Interlocked.Read(ref _position);
        _renderer.Read(destBuffer.AsSpan(offset, count), pos);
        Interlocked.Add(ref _position, count);
        return count;
    }

    public void Seek(TimeSpan time)
    {
        var newPos = (long)(time.TotalSeconds * _sampleRate) * 2;
        Interlocked.Exchange(ref _position, newPos);
        _renderer.Seek(newPos);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _renderer.Dispose();
        GC.SuppressFinalize(this);
    }
}
