namespace YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Interfaces;

internal interface IMidiRenderer : IDisposable
{
    float[] Render(TimeSpan duration);
    int Read(Span<float> buffer, long samplePosition);
    void Seek(long samplePosition);
}
