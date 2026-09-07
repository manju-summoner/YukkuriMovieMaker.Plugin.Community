namespace YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Interfaces;

internal interface IAudioEffectProcessor : IDisposable
{
    bool ApplyEffects(Span<float> buffer, float limiterThreshold, bool enableCompression, float compressionThreshold, float compressionRatio);
}
