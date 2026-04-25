using ComputeSharp;
using YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.GPU;
using YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Interfaces;
using YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Models;

namespace YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Rendering;

internal sealed class GpuAudioProcessor : IGpuAudioProcessor
{
    private readonly EffectsSettings _effects;
    private readonly int _sampleRate;
    private GraphicsDevice? _device;
    private ReadWriteBuffer<float>? _gpuBuffer;
    private ReadOnlyBuffer<float>? _reverbInputBuffer;
    private bool _disposed;

    public bool IsAvailable => _device is not null;

    public GpuAudioProcessor(EffectsSettings effects, int sampleRate)
    {
        _effects = effects;
        _sampleRate = sampleRate;
        _device = TryGetDevice();
    }

    private static GraphicsDevice? TryGetDevice()
    {
        try { return GraphicsDevice.GetDefault(); }
        catch { return null; }
    }

    private void EnsureGpuBuffers(int length)
    {
        if (_device is null) return;
        if (_gpuBuffer == null || _gpuBuffer.Length < length)
        {
            _gpuBuffer?.Dispose();
            _reverbInputBuffer?.Dispose();
            _gpuBuffer = _device.AllocateReadWriteBuffer<float>(length);
            _reverbInputBuffer = _device.AllocateReadOnlyBuffer<float>(length);
        }
    }

    public bool TryApplyEffects(Span<float> buffer, float limiterThreshold, bool enableCompression, float compressionThreshold, float compressionRatio)
    {
        if (_device is null || buffer.IsEmpty) return false;
        try
        {
            EnsureGpuBuffers(buffer.Length);
            var gpuBuffer = _gpuBuffer!;

            gpuBuffer.CopyFrom(buffer);

            if (enableCompression)
                _device.For(buffer.Length, new CompressionShader(gpuBuffer, compressionThreshold, compressionRatio));

            if (limiterThreshold > 0f)
                _device.For(buffer.Length, new LimiterShader(gpuBuffer, limiterThreshold));

            if (_effects.EnableReverb)
            {
                var delaySamples = (int)(_effects.ReverbDecay * _sampleRate);
                var reverbBuffer = _reverbInputBuffer!;
                reverbBuffer.CopyFrom(gpuBuffer);
                _device.For(buffer.Length, new ReverbShader(reverbBuffer, gpuBuffer, delaySamples, 0.4f, buffer.Length));
            }

            gpuBuffer.CopyTo(buffer);
            return true;
        }
        catch { return false; }
    }

    public bool TryNormalize(Span<float> buffer, float targetLevel)
    {
        if (_device is null || buffer.IsEmpty) return false;
        try
        {
            var peak = 0f;
            foreach (var s in buffer) peak = MathF.Max(peak, MathF.Abs(s));
            if (peak < 1e-6f) return true;

            var scale = targetLevel / peak;
            EnsureGpuBuffers(buffer.Length);
            var gpuBuffer = _gpuBuffer!;
            gpuBuffer.CopyFrom(buffer);
            _device.For(buffer.Length, new NormalizeSamplesShader(gpuBuffer, scale));
            gpuBuffer.CopyTo(buffer);
            return true;
        }
        catch { return false; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _gpuBuffer?.Dispose();
        _reverbInputBuffer?.Dispose();
        _device = null;
        GC.SuppressFinalize(this);
    }
}
