using System.Collections.Concurrent;
using YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Interfaces;

namespace YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Rendering;

internal sealed class GpuChunkedRenderer : IMidiRenderer
{
    private readonly IMidiRenderer _baseRenderer;
    private readonly MidiPluginSettings _settings;
    private readonly GpuAudioProcessor _gpuProcessor;
    private readonly int _chunkSizeStereo;
    private readonly int _historySamples;
    
    private readonly ConcurrentDictionary<long, float[]> _processedChunks = new();
    private readonly ConcurrentDictionary<long, float[]> _rawChunks = new();
    private readonly Lock _renderLock = new();
    private bool _disposed;

    public bool IsSeeking => false;

    public GpuChunkedRenderer(IMidiRenderer baseRenderer, MidiPluginSettings settings)
    {
        _baseRenderer = baseRenderer;
        _settings = settings;
        _gpuProcessor = new GpuAudioProcessor(settings.Effects, settings.Audio.SampleRate);
        _chunkSizeStereo = settings.Audio.SampleRate * 2;

        int hist = 0;
        if (settings.Effects.EnableEffects && settings.Effects.EnableReverb)
        {
            hist = (int)(settings.Effects.ReverbDecay * settings.Audio.SampleRate);
            if (hist % 2 != 0) hist++;
        }
        _historySamples = hist;
    }

    public int Read(Span<float> buffer, long stereoPosition)
    {
        if (_disposed) return 0;

        int samplesRead = 0;
        while (samplesRead < buffer.Length)
        {
            long currentPos = stereoPosition + samplesRead;
            long chunkIndex = currentPos / _chunkSizeStereo;
            int offsetInChunk = (int)(currentPos % _chunkSizeStereo);
            int samplesToCopy = Math.Min(buffer.Length - samplesRead, _chunkSizeStereo - offsetInChunk);

            var chunk = GetOrRenderChunk(chunkIndex);
            if (chunk.Length == 0)
            {
                buffer.Slice(samplesRead).Clear();
                break;
            }

            int available = Math.Min(samplesToCopy, chunk.Length - offsetInChunk);
            if (available <= 0) break;

            chunk.AsSpan(offsetInChunk, available).CopyTo(buffer.Slice(samplesRead, available));
            samplesRead += available;
        }

        return samplesRead;
    }

    /// <summary>
    /// オーバーラップ・セーブ方式による音声チャンクの取得およびレンダリングを行います。
    /// 前後のチャンク履歴を考慮したエフェクト処理をシームレスに適用し、
    /// 同時に再生位置から一定距離離れたチャンクを即座に破棄する戦略によりメモリ枯渇を防ぎます。
    /// </summary>
    private float[] GetOrRenderChunk(long chunkIndex)
    {
        if (_processedChunks.TryGetValue(chunkIndex, out var cached))
            return cached;

        lock (_renderLock)
        {
            if (_processedChunks.TryGetValue(chunkIndex, out cached))
                return cached;

            var rawChunk = new float[_chunkSizeStereo];
            int read = _baseRenderer.Read(rawChunk.AsSpan(), chunkIndex * _chunkSizeStereo);
            
            if (read == 0)
            {
                var empty = Array.Empty<float>();
                _processedChunks[chunkIndex] = empty;
                return empty;
            }

            if (read < _chunkSizeStereo)
            {
                Array.Resize(ref rawChunk, read);
            }

            _rawChunks[chunkIndex] = rawChunk;

            var processedChunk = new float[read];
            
            if (_gpuProcessor.IsAvailable && _settings.Performance.EnableGpuAcceleration)
            {
                if (_historySamples > 0)
                {
                    var workBuffer = new float[_historySamples + read];
                    if (_rawChunks.TryGetValue(chunkIndex - 1, out var prevRaw))
                    {
                        int copyLen = Math.Min(_historySamples, prevRaw.Length);
                        int srcOffset = prevRaw.Length - copyLen;
                        int dstOffset = _historySamples - copyLen;
                        prevRaw.AsSpan(srcOffset, copyLen).CopyTo(workBuffer.AsSpan(dstOffset, copyLen));
                    }
                    
                    rawChunk.CopyTo(workBuffer, _historySamples);

                    if (_settings.Effects.EnableEffects)
                    {
                        _gpuProcessor.TryApplyEffects(
                            workBuffer.AsSpan(),
                            _settings.Effects.EnableLimiter ? _settings.Effects.LimiterThreshold : 0f,
                            _settings.Effects.EnableCompression,
                            _settings.Effects.CompressionThreshold,
                            _settings.Effects.CompressionRatio);
                    }

                    workBuffer.AsSpan(_historySamples, read).CopyTo(processedChunk);
                }
                else
                {
                    rawChunk.CopyTo(processedChunk, 0);
                    if (_settings.Effects.EnableEffects)
                    {
                        _gpuProcessor.TryApplyEffects(
                            processedChunk.AsSpan(),
                            _settings.Effects.EnableLimiter ? _settings.Effects.LimiterThreshold : 0f,
                            _settings.Effects.EnableCompression,
                            _settings.Effects.CompressionThreshold,
                            _settings.Effects.CompressionRatio);
                    }
                }
            }
            else
            {
                rawChunk.CopyTo(processedChunk, 0);
            }

            _processedChunks[chunkIndex] = processedChunk;

            foreach (var key in _processedChunks.Keys)
            {
                if (key < chunkIndex - 2 || key > chunkIndex + 2)
                    _processedChunks.TryRemove(key, out _);
            }

            foreach (var key in _rawChunks.Keys)
            {
                if (key < chunkIndex - 2 || key > chunkIndex + 2)
                    _rawChunks.TryRemove(key, out _);
            }

            return processedChunk;
        }
    }

    public float[] Render(TimeSpan duration) => [];

    public void Seek(long samplePosition) { }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _gpuProcessor.Dispose();
        _baseRenderer.Dispose();
        _processedChunks.Clear();
        _rawChunks.Clear();
        GC.SuppressFinalize(this);
    }
}
