using MeltySynth;
using NAudio.Midi;
using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
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
    private readonly List<MidiStateSnapshot> _snapshots = [];
    private const int ChannelCount = 16;
    private const int NotesPerChannel = 128;
    private const int CCPerChannel = 128;
    private readonly int[] _activeNotes = new int[ChannelCount * NotesPerChannel];
    private readonly int[] _lastPatch = new int[ChannelCount];
    private readonly int[] _lastCC = new int[ChannelCount * CCPerChannel];
    private readonly int[] _lastPitch = new int[ChannelCount];
    private readonly int[] _lastCat = new int[ChannelCount];
    private readonly int[] _keyPressure = new int[ChannelCount * NotesPerChannel];
    private long _currentMonoPosition = -1;
    private int _eventIndex;
    private bool _disposed;

    private abstract record ParsedEvent(long SampleTime);
    private record ParsedNoteOn(long SampleTime, int Channel, int Note, int Velocity) : ParsedEvent(SampleTime);
    private record ParsedNoteOff(long SampleTime, int Channel, int Note) : ParsedEvent(SampleTime);
    private record ParsedPatchChange(long SampleTime, int Channel, int Patch) : ParsedEvent(SampleTime);
    private record ParsedControlChange(long SampleTime, int Channel, int Controller, int Value) : ParsedEvent(SampleTime);
    private record ParsedPitchBend(long SampleTime, int Channel, int Value) : ParsedEvent(SampleTime);
    private record ParsedChannelAfterTouch(long SampleTime, int Channel, int Pressure) : ParsedEvent(SampleTime);
    private record ParsedKeyAfterTouch(long SampleTime, int Channel, int Note, int Pressure) : ParsedEvent(SampleTime);

    private sealed record MidiStateSnapshot(
        long SampleTime,
        int EventIndex,
        int[] ActiveNotes,
        int[] LastPatch,
        int[] LastCC,
        int[] LastPitch,
        int[] LastCat,
        int[] KeyPressure
    );

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
        var synthSettings = new SynthesizerSettings(_sampleRate)
        {
            MaximumPolyphony = performance.MaxPolyphony,
        };
        _layers = layers.Select(l => (new Synthesizer(GetSoundFont(l.Path), synthSettings), l.Volume)).ToList();
        _events = ParseEvents(midiFilePath);
        BuildSnapshots();
        SeekTo(0);
    }

    /// <summary>
    /// シーク時の計算量をO(N)からO(1)へ削減するための状態スナップショットを事前に構築します。
    /// 一定間隔ごとに再生状態を記録することで、
    /// 任意の再生位置へ瞬時に移動可能なシークパフォーマンスを実現します。
    /// </summary>
    private void BuildSnapshots()
    {
        Array.Fill(_activeNotes, 0);
        Array.Fill(_lastPatch, -1);
        Array.Fill(_lastCC, -1);
        Array.Fill(_lastPitch, -1);
        Array.Fill(_lastCat, -1);
        Array.Fill(_keyPressure, -1);

        long nextSnapshotSample = 0;
        int eventIndex = 0;
        long maxSample = _events.Count > 0 ? _events[^1].SampleTime : 0;

        while (nextSnapshotSample <= maxSample)
        {
            while (eventIndex < _events.Count && _events[eventIndex].SampleTime <= nextSnapshotSample)
            {
                var evt = _events[eventIndex];
                if (evt is ParsedNoteOn on) _activeNotes[on.Channel * NotesPerChannel + on.Note] = on.Velocity;
                else if (evt is ParsedNoteOff off) { _activeNotes[off.Channel * NotesPerChannel + off.Note] = 0; _keyPressure[off.Channel * NotesPerChannel + off.Note] = -1; }
                else if (evt is ParsedPatchChange pc) _lastPatch[pc.Channel] = pc.Patch;
                else if (evt is ParsedControlChange cc) _lastCC[cc.Channel * CCPerChannel + cc.Controller] = cc.Value;
                else if (evt is ParsedPitchBend pb) _lastPitch[pb.Channel] = pb.Value;
                else if (evt is ParsedChannelAfterTouch cat) _lastCat[cat.Channel] = cat.Pressure;
                else if (evt is ParsedKeyAfterTouch kat) _keyPressure[kat.Channel * NotesPerChannel + kat.Note] = kat.Pressure;
                eventIndex++;
            }

            _snapshots.Add(new MidiStateSnapshot(
                nextSnapshotSample,
                eventIndex,
                _activeNotes.ToArray(),
                _lastPatch.ToArray(),
                _lastCC.ToArray(),
                _lastPitch.ToArray(),
                _lastCat.ToArray(),
                _keyPressure.ToArray()
            ));

            nextSnapshotSample += _sampleRate;
        }
    }

    private List<ParsedEvent> ParseEvents(string path)
    {
        var result = new List<ParsedEvent>();
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
            // NAudioのMidiEvent.Channelは1-based(1..16)、MeltySynthのProcessMidiMessageは0-based(0..15、percussion=9)のため、
            // 保存時点で-1して0-basedに正規化する。これを怠るとドラム(MIDI Ch.10)がメロディックチャンネルに流れて打楽器音色で鳴らなくなる。
            // NAudio: https://github.com/naudio/NAudio/blob/master/NAudio.Midi/Midi/MidiEvent.cs#L202 ("The MIDI Channel Number for this event (1-16)")
            // MeltySynth: https://github.com/sinshu/meltysynth/blob/main/MeltySynth/src/Synthesizer.cs#L18 (percussionChannel = 9), #L172 (0 <= channel && channel < channels.Length)
            else if (evt is NoteOnEvent on && on.Velocity > 0)
            {
                result.Add(new ParsedNoteOn(currentSample, on.Channel - 1, on.NoteNumber, on.Velocity));
            }
            else if (evt is NoteEvent ne && (ne.CommandCode == MidiCommandCode.NoteOff || (ne is NoteOnEvent no && no.Velocity == 0)))
            {
                result.Add(new ParsedNoteOff(currentSample, ne.Channel - 1, ne.NoteNumber));
            }
            else if (evt is PatchChangeEvent pc)
            {
                result.Add(new ParsedPatchChange(currentSample, pc.Channel - 1, pc.Patch));
            }
            else if (evt is ControlChangeEvent cc)
            {
                result.Add(new ParsedControlChange(currentSample, cc.Channel - 1, (int)cc.Controller, cc.ControllerValue));
            }
            else if (evt is PitchWheelChangeEvent pw)
            {
                result.Add(new ParsedPitchBend(currentSample, pw.Channel - 1, pw.Pitch));
            }
            else if (evt is ChannelAfterTouchEvent cat)
            {
                result.Add(new ParsedChannelAfterTouch(currentSample, cat.Channel - 1, cat.AfterTouchPressure));
            }
            else if (evt.CommandCode == MidiCommandCode.KeyAfterTouch && evt is NoteEvent kat)
            {
                result.Add(new ParsedKeyAfterTouch(currentSample, kat.Channel - 1, kat.NoteNumber, kat.Velocity));
            }
        }
        return result;
    }

    private void SeekTo(long targetSample)
    {
        foreach (var layer in _layers)
            layer.Synth.Reset();

        long snapSample = 0;
        int snapEventIndex = 0;

        int snapshotIndex = (int)(targetSample / _sampleRate);
        if (snapshotIndex >= _snapshots.Count) snapshotIndex = _snapshots.Count - 1;

        if (snapshotIndex >= 0 && _snapshots.Count > 0)
        {
            var snap = _snapshots[snapshotIndex];
            Array.Copy(snap.ActiveNotes, _activeNotes, _activeNotes.Length);
            Array.Copy(snap.LastPatch, _lastPatch, _lastPatch.Length);
            Array.Copy(snap.LastCC, _lastCC, _lastCC.Length);
            Array.Copy(snap.LastPitch, _lastPitch, _lastPitch.Length);
            Array.Copy(snap.LastCat, _lastCat, _lastCat.Length);
            Array.Copy(snap.KeyPressure, _keyPressure, _keyPressure.Length);
            snapSample = snap.SampleTime;
            snapEventIndex = snap.EventIndex;
        }
        else
        {
            Array.Fill(_activeNotes, 0);
            Array.Fill(_lastPatch, -1);
            Array.Fill(_lastCC, -1);
            Array.Fill(_lastPitch, -1);
            Array.Fill(_lastCat, -1);
            Array.Fill(_keyPressure, -1);
        }

        // スナップショット時点の状態をsynthに流す。長期サステイン中のノートはこの時点を起点に
        // envelope/LFOが動き始めるため、最大でスナップショット間隔(=1秒)分の位相ずれが残る。
        foreach (var layer in _layers)
            FlushStateToSynth(layer.Synth);

        // snapSampleからtargetSampleまで実時間でレンダリング(出力は破棄)し、
        // 中間イベントを正しい時刻に適用することでenvelope/LFO/フィルタを正しい位相まで進める。
        // これによりサステイン中ノートがアタックからやり直す問題が解消される。
        _eventIndex = snapEventIndex;
        SilentCatchUp(snapSample, targetSample);
        _currentMonoPosition = targetSample;
    }

    private void FlushStateToSynth(Synthesizer synth)
    {
        for (int ch = 0; ch < ChannelCount; ch++)
        {
            if (_lastPatch[ch] != -1) synth.ProcessMidiMessage(ch, 0xC0, _lastPatch[ch], 0);
            for (int cc = 0; cc < CCPerChannel; cc++)
            {
                int val = _lastCC[ch * CCPerChannel + cc];
                if (val != -1) synth.ProcessMidiMessage(ch, 0xB0, cc, val);
            }
            if (_lastPitch[ch] != -1) synth.ProcessMidiMessage(ch, 0xE0, _lastPitch[ch] & 0x7F, (_lastPitch[ch] >> 7) & 0x7F);
            if (_lastCat[ch] != -1) synth.ProcessMidiMessage(ch, 0xD0, _lastCat[ch], 0);

            for (int n = 0; n < NotesPerChannel; n++)
            {
                int vel = _activeNotes[ch * NotesPerChannel + n];
                if (vel > 0)
                {
                    synth.ProcessMidiMessage(ch, 0x90, n, vel);
                    int pressure = _keyPressure[ch * NotesPerChannel + n];
                    if (pressure >= 0) synth.ProcessMidiMessage(ch, 0xA0, n, pressure);
                }
            }
        }
    }

    private void SilentCatchUp(long fromSample, long toSample)
    {
        if (toSample <= fromSample || _layers.Count == 0) return;

        const int chunkSize = 1024;
        var dummyL = ArrayPool<float>.Shared.Rent(chunkSize);
        var dummyR = ArrayPool<float>.Shared.Rent(chunkSize);
        try
        {
            long pos = fromSample;
            while (pos < toSample)
            {
                long nextEventTime = _eventIndex < _events.Count ? _events[_eventIndex].SampleTime : long.MaxValue;
                int chunk = (int)Math.Min(chunkSize, Math.Min(toSample - pos, nextEventTime - pos));
                if (chunk > 0)
                {
                    var tls = dummyL.AsSpan(0, chunk);
                    var trs = dummyR.AsSpan(0, chunk);
                    foreach (var layer in _layers)
                        layer.Synth.Render(tls, trs);
                    pos += chunk;
                }
                while (_eventIndex < _events.Count && _events[_eventIndex].SampleTime <= pos)
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
        }
        finally
        {
            ArrayPool<float>.Shared.Return(dummyL);
            ArrayPool<float>.Shared.Return(dummyR);
        }
    }

    /// <summary>
    /// 指定された位置からのオーディオレンダリングを実行します。
    /// 配列プールを利用したゼロアロケーションアーキテクチャと、SIMD命令を用いた
    /// 音声のベクトル化ミックス処理により、ガベージコレクションの発生を抑えつつ高いパフォーマンスを引き出します。
    /// </summary>
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

                        unsafe
                        {
                            fixed (float* pDstL = mls)
                            fixed (float* pDstR = mrs)
                            fixed (float* pSrcL = tls)
                            fixed (float* pSrcR = trs)
                            {
                                float* dL = pDstL + samplesRendered;
                                float* dR = pDstR + samplesRendered;
                                float* sL = pSrcL;
                                float* sR = pSrcR;
                                float vol = layer.Volume;
                                int i = 0;

                                if (Avx.IsSupported && samplesToRender >= 8)
                                {
                                    int simdLength = samplesToRender - (samplesToRender % 8);
                                    var vVol = Vector256.Create(vol);
                                    for (; i < simdLength; i += 8)
                                    {
                                        Avx.Store(dL + i, Avx.Add(Avx.LoadVector256(dL + i), Avx.Multiply(Avx.LoadVector256(sL + i), vVol)));
                                        Avx.Store(dR + i, Avx.Add(Avx.LoadVector256(dR + i), Avx.Multiply(Avx.LoadVector256(sR + i), vVol)));
                                    }
                                }
                                else if (Sse.IsSupported && samplesToRender >= 4)
                                {
                                    int simdLength = samplesToRender - (samplesToRender % 4);
                                    var vVol = Vector128.Create(vol);
                                    for (; i < simdLength; i += 4)
                                    {
                                        Sse.Store(dL + i, Sse.Add(Sse.LoadVector128(dL + i), Sse.Multiply(Sse.LoadVector128(sL + i), vVol)));
                                        Sse.Store(dR + i, Sse.Add(Sse.LoadVector128(dR + i), Sse.Multiply(Sse.LoadVector128(sR + i), vVol)));
                                    }
                                }

                                for (; i < samplesToRender; i++)
                                {
                                    dL[i] += sL[i] * vol;
                                    dR[i] += sR[i] * vol;
                                }
                            }
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

            unsafe
            {
                fixed (float* pDst = buffer)
                fixed (float* pSrcL = mixLeft)
                fixed (float* pSrcR = mixRight)
                {
                    float* dst = pDst;
                    float* srcL = pSrcL;
                    float* srcR = pSrcR;
                    float masterVol = _masterVolume;
                    int i = 0;

                    if (Avx.IsSupported && numMono >= 8)
                    {
                        int simdLength = numMono - (numMono % 8);
                        var vVol = Vector256.Create(masterVol);
                        for (; i < simdLength; i += 8)
                        {
                            var vL = Avx.Multiply(Avx.LoadVector256(srcL + i), vVol);
                            var vR = Avx.Multiply(Avx.LoadVector256(srcR + i), vVol);
                            var low = Avx.UnpackLow(vL, vR);
                            var high = Avx.UnpackHigh(vL, vR);
                            Avx.Store(dst + i * 2, Avx.Permute2x128(low, high, 0x20));
                            Avx.Store(dst + i * 2 + 8, Avx.Permute2x128(low, high, 0x31));
                        }
                    }
                    else if (Sse.IsSupported && numMono >= 4)
                    {
                        int simdLength = numMono - (numMono % 4);
                        var vVol = Vector128.Create(masterVol);
                        for (; i < simdLength; i += 4)
                        {
                            var vL = Sse.Multiply(Sse.LoadVector128(srcL + i), vVol);
                            var vR = Sse.Multiply(Sse.LoadVector128(srcR + i), vVol);
                            Sse.Store(dst + i * 2, Sse.UnpackLow(vL, vR));
                            Sse.Store(dst + i * 2 + 4, Sse.UnpackHigh(vL, vR));
                        }
                    }

                    for (; i < numMono; i++)
                    {
                        dst[i * 2] = srcL[i] * masterVol;
                        dst[i * 2 + 1] = srcR[i] * masterVol;
                    }
                }
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
