using MeltySynth;
using NAudio.Midi;
using System.Buffers;
using System.Collections.Concurrent;
using YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Interfaces;
using YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Models;
using MidiFile = NAudio.Midi.MidiFile;

namespace YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Rendering;

internal sealed class SoundFontRenderer : IMidiRenderer
{
    private readonly int _sampleRate;
    private readonly float _masterVolume;
    private readonly List<(Synthesizer Synth, float Volume)> _layers;
    private readonly List<ParsedEvent> _events;
    private long _currentMonoPosition = -1;
    private int _eventIndex;
    private bool _disposed;

    public bool IsSeeking => false;

    private abstract record ParsedEvent(long SampleTime);
    private record ParsedNoteOn(long SampleTime, int Channel, int Note, int Velocity) : ParsedEvent(SampleTime);
    private record ParsedNoteOff(long SampleTime, int Channel, int Note) : ParsedEvent(SampleTime);
    private record ParsedPatchChange(long SampleTime, int Channel, int Patch) : ParsedEvent(SampleTime);
    private record ParsedControlChange(long SampleTime, int Channel, int Controller, int Value) : ParsedEvent(SampleTime);
    private record ParsedPitchBend(long SampleTime, int Channel, int Value) : ParsedEvent(SampleTime);
    private record ParsedChannelAfterTouch(long SampleTime, int Channel, int Pressure) : ParsedEvent(SampleTime);
    private record ParsedKeyAfterTouch(long SampleTime, int Channel, int Note, int Pressure) : ParsedEvent(SampleTime);

    private static readonly ConcurrentDictionary<string, Lazy<SoundFont>> _sfCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Lock _cacheLock = new();

    public static void ClearCache()
    {
        lock (_cacheLock)
        {
            _sfCache.Clear();
        }
    }

    private static SoundFont GetSoundFont(string path)
    {
        lock (_cacheLock)
        {
            return _sfCache.GetOrAdd(path, p => new Lazy<SoundFont>(() => new SoundFont(p))).Value;
        }
    }

    public SoundFontRenderer(string midiFilePath, IReadOnlyList<(string Path, float Volume)> layers, AudioSettings audio, PerformanceSettings performance)
    {
        _sampleRate = audio.SampleRate;
        _masterVolume = audio.MasterVolume;
        _layers = layers.Select(l => (new Synthesizer(GetSoundFont(l.Path), _sampleRate), l.Volume)).ToList();
        _events = ParseEvents(midiFilePath);
        SeekTo(0);
    }

    private List<ParsedEvent> ParseEvents(string path)
    {
        var result = new List<ParsedEvent>();
        try
        {
            var midiFile = new MidiFile(path, false);
            int ticksPerQ = midiFile.DeltaTicksPerQuarterNote;
            double secondsPerTick = 500000.0 / (ticksPerQ * 1_000_000.0);
            var allEvents = midiFile.Events.SelectMany(t => t).OrderBy(e => e.AbsoluteTime).ToList();

            long currentSample = 0;
            long lastTick = 0;

            foreach (var evt in allEvents)
            {
                long delta = evt.AbsoluteTime - lastTick;
                currentSample += (long)(delta * secondsPerTick * _sampleRate);
                lastTick = evt.AbsoluteTime;

                if (evt is TempoEvent te)
                {
                    secondsPerTick = te.MicrosecondsPerQuarterNote / (ticksPerQ * 1_000_000.0);
                }
                else if (evt is NoteOnEvent on && on.Velocity > 0)
                {
                    result.Add(new ParsedNoteOn(currentSample, on.Channel, on.NoteNumber, on.Velocity));
                }
                else if (evt is NoteEvent ne && (ne.CommandCode == MidiCommandCode.NoteOff || (ne is NoteOnEvent no && no.Velocity == 0)))
                {
                    result.Add(new ParsedNoteOff(currentSample, ne.Channel, ne.NoteNumber));
                }
                else if (evt is PatchChangeEvent pc)
                {
                    result.Add(new ParsedPatchChange(currentSample, pc.Channel, pc.Patch));
                }
                else if (evt is ControlChangeEvent cc)
                {
                    result.Add(new ParsedControlChange(currentSample, cc.Channel, (int)cc.Controller, cc.ControllerValue));
                }
                else if (evt is PitchWheelChangeEvent pw)
                {
                    result.Add(new ParsedPitchBend(currentSample, pw.Channel, pw.Pitch));
                }
                else if (evt is ChannelAfterTouchEvent cat)
                {
                    result.Add(new ParsedChannelAfterTouch(currentSample, cat.Channel, cat.AfterTouchPressure));
                }
                else if (evt.CommandCode == MidiCommandCode.KeyAfterTouch && evt is NoteEvent kat)
                {
                    result.Add(new ParsedKeyAfterTouch(currentSample, kat.Channel, kat.NoteNumber, kat.Velocity));
                }
            }
        }
        catch { }
        return result;
    }

    private void SeekTo(long targetSample)
    {
        foreach (var layer in _layers)
        {
            layer.Synth.Reset();
        }

        var activeNotes = new Dictionary<(int Channel, int Note), int>();
        var lastPatch = new Dictionary<int, int>();
        var lastCC = new Dictionary<(int Channel, int Controller), int>();
        var lastPitch = new Dictionary<int, int>();
        var lastCat = new Dictionary<int, int>();

        foreach (var evt in _events)
        {
            if (evt.SampleTime > targetSample) break;

            if (evt is ParsedNoteOn on) activeNotes[(on.Channel, on.Note)] = on.Velocity;
            else if (evt is ParsedNoteOff off) activeNotes.Remove((off.Channel, off.Note));
            else if (evt is ParsedPatchChange pc) lastPatch[pc.Channel] = pc.Patch;
            else if (evt is ParsedControlChange cc) lastCC[(cc.Channel, cc.Controller)] = cc.Value;
            else if (evt is ParsedPitchBend pb) lastPitch[pb.Channel] = pb.Value;
            else if (evt is ParsedChannelAfterTouch cat) lastCat[cat.Channel] = cat.Pressure;
        }

        foreach (var layer in _layers)
        {
            foreach (var kv in lastPatch) layer.Synth.ProcessMidiMessage(kv.Key, 0xC0, kv.Value, 0);
            foreach (var kv in lastCC) layer.Synth.ProcessMidiMessage(kv.Key.Channel, 0xB0, kv.Key.Controller, kv.Value);
            foreach (var kv in lastPitch) layer.Synth.ProcessMidiMessage(kv.Key, 0xE0, kv.Value & 0x7F, (kv.Value >> 7) & 0x7F);
            foreach (var kv in lastCat) layer.Synth.ProcessMidiMessage(kv.Key, 0xD0, kv.Value, 0);
            foreach (var kv in activeNotes) layer.Synth.ProcessMidiMessage(kv.Key.Channel, 0x90, kv.Key.Note, kv.Value);
        }

        _eventIndex = _events.FindIndex(e => e.SampleTime > targetSample);
        if (_eventIndex == -1) _eventIndex = _events.Count;
        _currentMonoPosition = targetSample;
    }

    public int Read(Span<float> buffer, long stereoPosition)
    {
        long targetMono = stereoPosition / 2;
        if (_currentMonoPosition != targetMono)
        {
            SeekTo(targetMono);
        }

        int numMono = buffer.Length / 2;
        int samplesRendered = 0;

        var left = ArrayPool<float>.Shared.Rent(numMono);
        var right = ArrayPool<float>.Shared.Rent(numMono);
        var mixLeft = ArrayPool<float>.Shared.Rent(numMono);
        var mixRight = ArrayPool<float>.Shared.Rent(numMono);

        try
        {
            var ls = left.AsSpan(0, numMono);
            var rs = right.AsSpan(0, numMono);
            var mls = mixLeft.AsSpan(0, numMono);
            var mrs = mixRight.AsSpan(0, numMono);

            mls.Clear();
            mrs.Clear();

            while (samplesRendered < numMono)
            {
                long nextEventTime = _eventIndex < _events.Count ? _events[_eventIndex].SampleTime : long.MaxValue;
                int samplesToRender = (int)Math.Min(numMono - samplesRendered, nextEventTime - _currentMonoPosition);

                if (samplesToRender > 0)
                {
                    var tls = ls.Slice(0, samplesToRender);
                    var trs = rs.Slice(0, samplesToRender);

                    foreach (var layer in _layers)
                    {
                        tls.Clear();
                        trs.Clear();
                        layer.Synth.Render(tls, trs);

                        for (int i = 0; i < samplesToRender; i++)
                        {
                            mls[samplesRendered + i] += tls[i] * layer.Volume;
                            mrs[samplesRendered + i] += trs[i] * layer.Volume;
                        }
                    }

                    samplesRendered += samplesToRender;
                    _currentMonoPosition += samplesToRender;
                }

                while (_eventIndex < _events.Count && _events[_eventIndex].SampleTime <= _currentMonoPosition)
                {
                    var evt = _events[_eventIndex];
                    foreach (var layer in _layers)
                    {
                        if (evt is ParsedNoteOn on) layer.Synth.ProcessMidiMessage(on.Channel, 0x90, on.Note, on.Velocity);
                        else if (evt is ParsedNoteOff off) layer.Synth.ProcessMidiMessage(off.Channel, 0x80, off.Note, 0);
                        else if (evt is ParsedPatchChange pc) layer.Synth.ProcessMidiMessage(pc.Channel, 0xC0, pc.Patch, 0);
                        else if (evt is ParsedControlChange cc) layer.Synth.ProcessMidiMessage(cc.Channel, 0xB0, cc.Controller, cc.Value);
                        else if (evt is ParsedPitchBend pb) layer.Synth.ProcessMidiMessage(pb.Channel, 0xE0, pb.Value & 0x7F, (pb.Value >> 7) & 0x7F);
                        else if (evt is ParsedChannelAfterTouch cat) layer.Synth.ProcessMidiMessage(cat.Channel, 0xD0, cat.Pressure, 0);
                        else if (evt is ParsedKeyAfterTouch kat) layer.Synth.ProcessMidiMessage(kat.Channel, 0xA0, kat.Note, kat.Pressure);
                    }
                    _eventIndex++;
                }
            }

            for (int i = 0; i < numMono; i++)
            {
                buffer[i * 2] = mls[i] * _masterVolume;
                buffer[i * 2 + 1] = mrs[i] * _masterVolume;
            }
        }
        finally
        {
            ArrayPool<float>.Shared.Return(left);
            ArrayPool<float>.Shared.Return(right);
            ArrayPool<float>.Shared.Return(mixLeft);
            ArrayPool<float>.Shared.Return(mixRight);
        }

        return buffer.Length;
    }

    public float[] Render(TimeSpan duration)
    {
        var totalMono = (long)(duration.TotalSeconds * _sampleRate);
        if (totalMono <= 0) return [];
        var output = new float[totalMono * 2];
        SeekTo(0);
        Read(output.AsSpan(), 0);
        return output;
    }

    public void Seek(long samplePosition) { }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
