using System.Numerics;
using YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Interfaces;
using YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Models;

namespace YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Rendering;

internal sealed class AudioEffectProcessor(EffectsSettings effects, int sampleRate) : IAudioEffectProcessor
{
    private const float ReverbDecayGain = 0.4f;

    private readonly EffectsSettings _effects = effects;
    private readonly int _sampleRate = sampleRate;

    public bool ApplyEffects(Span<float> buffer, float limiterThreshold, bool enableCompression, float compressionThreshold, float compressionRatio)
    {
        if (buffer.IsEmpty)
            return false;

        if (enableCompression)
            Compress(buffer, compressionThreshold, compressionRatio);

        if (limiterThreshold > 0f)
            Limit(buffer, limiterThreshold);

        if (_effects.EnableReverb)
            Reverb(buffer, (int)(_effects.ReverbDecay * _sampleRate) * 2);

        return true;
    }

    private static void Compress(Span<float> buffer, float threshold, float ratio)
    {
        var index = 0;

        if (Vector.IsHardwareAccelerated && buffer.Length >= Vector<float>.Count)
        {
            var thresholds = new Vector<float>(threshold);
            var inverseRatio = new Vector<float>(1f / ratio);

            for (; index <= buffer.Length - Vector<float>.Count; index += Vector<float>.Count)
            {
                var samples = new Vector<float>(buffer[index..]);
                var magnitudes = Vector.Abs(samples);
                var compressed = thresholds + (magnitudes - thresholds) * inverseRatio;
                var scaled = samples / magnitudes * compressed;
                Vector.ConditionalSelect(Vector.GreaterThan(magnitudes, thresholds), scaled, samples).CopyTo(buffer[index..]);
            }
        }

        for (; index < buffer.Length; index++)
        {
            var sample = buffer[index];
            var magnitude = MathF.Abs(sample);
            if (magnitude <= threshold)
                continue;

            buffer[index] = sample / magnitude * (threshold + (magnitude - threshold) / ratio);
        }
    }

    private static void Limit(Span<float> buffer, float threshold)
    {
        var index = 0;

        if (Vector.IsHardwareAccelerated && buffer.Length >= Vector<float>.Count)
        {
            var lower = new Vector<float>(-threshold);
            var upper = new Vector<float>(threshold);

            for (; index <= buffer.Length - Vector<float>.Count; index += Vector<float>.Count)
                Vector.Min(Vector.Max(new Vector<float>(buffer[index..]), lower), upper).CopyTo(buffer[index..]);
        }

        for (; index < buffer.Length; index++)
            buffer[index] = Math.Clamp(buffer[index], -threshold, threshold);
    }

    private static void Reverb(Span<float> buffer, int delaySamples)
    {
        if (delaySamples < 0)
            return;

        for (var index = buffer.Length - 1; index >= delaySamples; index--)
            buffer[index] += buffer[index - delaySamples] * ReverbDecayGain;
    }

    public void Dispose()
    {
    }
}
