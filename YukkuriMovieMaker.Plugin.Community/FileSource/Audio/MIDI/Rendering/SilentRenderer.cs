using YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Interfaces;

namespace YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Rendering;

internal sealed class SilentRenderer : IMidiRenderer
{
    public bool IsSeeking => false;

    public int Read(Span<float> buffer, long samplePosition)
    {
        buffer.Clear();
        return buffer.Length;
    }

    public float[] Render(TimeSpan duration) => [];

    public void Seek(long samplePosition) { }

    public void Dispose() { }
}
