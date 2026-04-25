namespace YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Interfaces;

internal interface IGpuAudioProcessor : IDisposable
{
    bool IsAvailable { get; }
    bool TryApplyEffects(Span<float> buffer, float limiterThreshold, bool enableCompression, float compressionThreshold, float compressionRatio);
    bool TryNormalize(Span<float> buffer, float targetLevel);
}
