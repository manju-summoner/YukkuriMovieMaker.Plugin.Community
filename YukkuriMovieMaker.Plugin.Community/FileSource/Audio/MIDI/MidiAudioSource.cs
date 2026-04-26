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
    public bool IsReadable { get; }
    internal Exception? LoadError { get; }

    private readonly int _sampleRate;
    private readonly IMidiRenderer? _renderer;
    private long _position;
    private bool _disposed;
    private readonly Lock _syncRoot = new();

    internal MidiAudioSource(string filePath, MidiPluginSettings settings)
    {
        _sampleRate = settings.Audio.SampleRate;

        try
        {
            var mf = new MidiFile(filePath);
            Duration = mf.Length + TimeSpan.FromSeconds(2.0);
        }
        catch (Exception ex)
        {
            Duration = TimeSpan.Zero;
            LoadError = ex;
            IsReadable = false;
            return;
        }

        try
        {
            _renderer = CreateRenderer(filePath, settings);
            IsReadable = true;
        }
        catch (Exception ex)
        {
            LoadError = ex;
            IsReadable = false;
        }
    }

    private static IMidiRenderer CreateRenderer(string filePath, MidiPluginSettings settings)
    {
        var sfProvider = new SoundFontResolverService(settings.SoundFont);
        IMidiRenderer baseRenderer = settings.Performance.RenderingMode switch
        {
            RenderingMode.SoundFont when sfProvider.HasAnySoundFont() => new SoundFontRenderer(filePath, sfProvider.GetActiveSoundFontPaths(), settings.Audio, settings.Performance),
            RenderingMode.SoundFont when settings.SoundFont.FallbackToSynthesis => new WaveformSynthesisRenderer(filePath, settings.Audio, settings.Performance),
            RenderingMode.SoundFont => throw new InvalidOperationException("No SoundFont available and fallback to synthesis is disabled."),
            _ => new WaveformSynthesisRenderer(filePath, settings.Audio, settings.Performance)
        };

        if (settings.Performance.EnableGpuAcceleration)
            return new GpuChunkedRenderer(baseRenderer, settings);

        return baseRenderer;
    }

    public int Read(float[] destBuffer, int offset, int count)
    {
        if (_disposed || _renderer is null) return 0;

        lock (_syncRoot)
        {
            _renderer.Read(destBuffer.AsSpan(offset, count), _position);
            _position += count;
            return count;
        }
    }

    public void Seek(TimeSpan time)
    {
        var newPos = (long)(time.TotalSeconds * _sampleRate) * 2;
        lock (_syncRoot)
        {
            _position = newPos;
            _renderer?.Seek(newPos);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _renderer?.Dispose();
        GC.SuppressFinalize(this);
    }
}
