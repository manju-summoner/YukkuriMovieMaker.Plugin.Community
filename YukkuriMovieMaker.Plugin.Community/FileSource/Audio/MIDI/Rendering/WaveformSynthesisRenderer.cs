using NAudio.Midi;
using YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Interfaces;
using YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Models;

namespace YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Rendering;

internal sealed class WaveformSynthesisRenderer : IMidiRenderer
{
    private readonly int _sampleRate;
    private readonly int _maxPolyphony;
    private readonly float _gain;
    private readonly List<ParsedNote> _noteEvents;
    private bool _disposed;

    private sealed record ParsedNote(float Frequency, long StartSample, long EndSample, float Velocity);

    public WaveformSynthesisRenderer(string midiFilePath, AudioSettings audio, PerformanceSettings performance)
    {
        _sampleRate = audio.SampleRate;
        _maxPolyphony = performance.MaxPolyphony;
        _gain = audio.MasterVolume * 0.1f;
        _noteEvents = ParseNoteEvents(midiFilePath);
    }

    private List<ParsedNote> ParseNoteEvents(string path)
    {
        var result = new List<ParsedNote>();
        var midiFile = new MidiFile(path, false);
        int ticksPerQ = midiFile.DeltaTicksPerQuarterNote;
        double secondsPerTick = 500000.0 / (ticksPerQ * 1_000_000.0);
        var openNotes = new Dictionary<(int, int), (long StartSample, float Velocity)>();
        var allEvents = midiFile.Events.SelectMany(t => t).OrderBy(e => e.AbsoluteTime).ToList();
        // 累積はdoubleで保持し、ParsedNoteへ書き出す瞬間だけlongに量子化する。
        // tickごとに(long)キャストすると小数部が切り捨てられて誤差が累積し、
        // 長尺・高密度MIDIでタイミングずれを引き起こすため。
        double currentSampleAcc = 0.0;
        long currentSample = 0;
        long lastTick = 0;

        foreach (var evt in allEvents)
        {
            long delta = evt.AbsoluteTime - lastTick;
            currentSampleAcc += delta * secondsPerTick * _sampleRate;
            lastTick = evt.AbsoluteTime;
            currentSample = (long)currentSampleAcc;
            if (evt is TempoEvent te)
                secondsPerTick = te.MicrosecondsPerQuarterNote / (ticksPerQ * 1_000_000.0);

            if (evt is NoteOnEvent on && on.Velocity > 0)
            {
                var key = (on.Channel, on.NoteNumber);
                if (openNotes.TryGetValue(key, out var oldNote))
                {
                    result.Add(new ParsedNote(Freq(key.Item2), oldNote.StartSample, currentSample, oldNote.Velocity));
                }
                else if (openNotes.Count >= _maxPolyphony)
                {
                    var oldest = openNotes.OrderBy(kv => kv.Value.StartSample).First();
                    openNotes.Remove(oldest.Key);
                    result.Add(new ParsedNote(Freq(oldest.Key.Item2), oldest.Value.StartSample, currentSample, oldest.Value.Velocity));
                }
                openNotes[key] = (currentSample, on.Velocity / 127f);
            }
            else if (evt is NoteEvent ne && (ne.CommandCode == MidiCommandCode.NoteOff || (ne is NoteOnEvent no && no.Velocity == 0)))
            {
                var key = (ne.Channel, ne.NoteNumber);
                if (openNotes.Remove(key, out var op))
                {
                    result.Add(new ParsedNote(Freq(ne.NoteNumber), op.StartSample, currentSample, op.Velocity));
                }
            }
        }
        foreach (var kv in openNotes)
            result.Add(new ParsedNote(Freq(kv.Key.Item2), kv.Value.StartSample, currentSample, kv.Value.Velocity));
        return result;
    }

    private static float Freq(int n) => 440f * MathF.Pow(2f, (n - 69) / 12f);

    public float[] Render(TimeSpan duration)
    {
        var totalMono = (long)(duration.TotalSeconds * _sampleRate);
        if (totalMono <= 0) return [];
        var output = new float[totalMono * 2];
        if (AccumulateNotes(output.AsSpan(), 0, totalMono, _noteEvents))
        {
            ApplyClamp(output.AsSpan());
        }
        return output;
    }

    public int Read(Span<float> buffer, long stereoPosition)
    {
        long monoPos = stereoPosition / 2;
        buffer.Clear();
        long endMono = monoPos + buffer.Length / 2;
        if (AccumulateNotes(buffer, monoPos, endMono, _noteEvents))
        {
            ApplyClamp(buffer);
        }
        return buffer.Length;
    }

    private bool AccumulateNotes(Span<float> buffer, long monoFrom, long monoTo, List<ParsedNote> notes)
    {
        float minDur = _sampleRate * 0.002f;
        bool accumulated = false;

        foreach (var note in notes)
        {
            if (note.StartSample >= monoTo || note.EndSample <= monoFrom) continue;

            long dur = note.EndSample - note.StartSample;
            if (dur < minDur) continue;

            accumulated = true;
            float amp = note.Velocity * _gain;
            float atk = _sampleRate * 0.005f;
            float rel = _sampleRate * 0.010f;

            if (atk + rel > dur)
            {
                float scale = dur / (atk + rel);
                atk *= scale;
                rel *= scale;
            }

            long from = Math.Max(note.StartSample, monoFrom);
            long to = Math.Min(note.EndSample, monoTo);

            for (long s = from; s < to; s++)
            {
                double t = (double)(s - note.StartSample) / _sampleRate;
                float sample = (float)(Math.Sin(2.0 * Math.PI * note.Frequency * t)) * amp;
                long p = s - note.StartSample;

                if (p < atk)
                {
                    float env = 0.5f - 0.5f * MathF.Cos(MathF.PI * (float)p / atk);
                    sample *= env;
                }
                else if (p >= dur - rel)
                {
                    float env = 0.5f - 0.5f * MathF.Cos(MathF.PI * MathF.Max(0f, (float)(dur - 1 - p)) / rel);
                    sample *= env;
                }

                int idx = (int)((s - monoFrom) * 2);
                if ((uint)(idx + 1) < (uint)buffer.Length)
                {
                    buffer[idx] += sample;
                    buffer[idx + 1] += sample;
                }
            }
        }
        return accumulated;
    }

    private static void ApplyClamp(Span<float> buffer)
    {
        for (int i = 0; i < buffer.Length; i++)
            buffer[i] = MathF.Tanh(buffer[i]) * 0.99f;
    }

    public void Seek(long stereoPosition) { }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
