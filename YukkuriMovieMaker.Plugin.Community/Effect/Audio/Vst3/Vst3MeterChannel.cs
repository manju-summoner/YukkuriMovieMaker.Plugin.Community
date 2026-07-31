using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    internal readonly record struct Vst3MeterValue(
        long SourceId,
        int Generation,
        uint ParamId,
        double NormalizedValue,
        long SamplePosition,
        int SampleRate,
        bool IsReset);

    /// <summary>
    /// 音声処理インスタンスが生成したoutput parameterをエディターへ渡す。
    /// 音声側は固定長リングへ書くだけで、UI側が低優先度で履歴を読み取る。
    /// </summary>
    internal sealed class Vst3MeterChannel
    {
        readonly object sync = new();
        readonly List<Vst3MeterPublisher> publishers = [];
        volatile Vst3MeterPublisher[] publisherSnapshot = [];
        long nextSourceId;

        public Vst3MeterPublisher CreatePublisher()
        {
            lock (sync)
            {
                var publisher = new Vst3MeterPublisher(this, ++nextSourceId);
                publishers.Add(publisher);
                publisherSnapshot = [.. publishers];
                return publisher;
            }
        }

        public Vst3MeterSubscription Subscribe() => new(this);

        internal Vst3MeterPublisher[] GetPublishers() => publisherSnapshot;

        internal void Remove(Vst3MeterPublisher publisher)
        {
            lock (sync)
            {
                publishers.Remove(publisher);
                publisherSnapshot = [.. publishers];
            }
        }
    }

    internal sealed class Vst3MeterPublisher(Vst3MeterChannel owner, long sourceId) : IDisposable
    {
        const int Capacity = 4096;

        readonly Slot[] slots = new Slot[Capacity];
        long writeSequence;
        long publishedSequence;
        int generation;
        volatile bool isDisposed;

        public bool IsDisposed => isDisposed;

        public void Publish(uint paramId, double normalizedValue, long samplePosition, int sampleRate)
        {
            Write(new Vst3MeterValue(
                sourceId,
                generation,
                paramId,
                normalizedValue,
                samplePosition,
                sampleRate,
                false));
        }

        public void Reset(long samplePosition, int sampleRate)
        {
            generation++;
            Write(new Vst3MeterValue(
                sourceId,
                generation,
                0,
                0,
                samplePosition,
                sampleRate,
                true));
        }

        internal void Read(ref long cursor, List<Vst3MeterValue> destination)
        {
            var latest = Volatile.Read(ref publishedSequence);
            if (latest <= 0 || latest <= cursor)
                return;
            var first = Math.Max(cursor + 1, latest - Capacity + 1);
            for (var sequence = first; sequence <= latest; sequence++)
            {
                ref var slot = ref slots[(int)((sequence - 1) % Capacity)];
                if (Volatile.Read(ref slot.Sequence) != sequence)
                    continue;
                var value = slot.Value;
                if (Volatile.Read(ref slot.Sequence) == sequence)
                    destination.Add(value);
            }
            cursor = latest;
        }

        void Write(in Vst3MeterValue value)
        {
            if (isDisposed)
                return;
            var sequence = ++writeSequence;
            ref var slot = ref slots[(int)((sequence - 1) % Capacity)];
            Volatile.Write(ref slot.Sequence, -sequence);
            slot.Value = value;
            Volatile.Write(ref slot.Sequence, sequence);
            Volatile.Write(ref publishedSequence, sequence);
        }

        public void Dispose()
        {
            if (isDisposed)
                return;
            isDisposed = true;
            owner.Remove(this);
        }

        struct Slot
        {
            public Vst3MeterValue Value;
            public long Sequence;
        }
    }

    internal sealed class Vst3MeterSubscription(Vst3MeterChannel owner) : IDisposable
    {
        const int MaxHistoryCount = 8192;

        readonly Dictionary<Vst3MeterPublisher, long> cursors = [];
        readonly Dictionary<long, int> generations = [];
        readonly List<Vst3MeterValue> history = [];
        readonly List<Vst3MeterValue> received = [];
        readonly Dictionary<uint, (long PositionTicks, double Value)> selectedValues = [];
        bool isDisposed;

        public int CollectValues(TimeSpan targetPosition, Dictionary<uint, double> destination)
        {
            if (isDisposed)
                return 0;

            received.Clear();
            foreach (var publisher in owner.GetPublishers())
            {
                cursors.TryGetValue(publisher, out var cursor);
                publisher.Read(ref cursor, received);
                cursors[publisher] = cursor;
            }

            foreach (var value in received)
            {
                if (value.IsReset)
                {
                    if (!generations.TryGetValue(value.SourceId, out var currentGeneration)
                        || value.Generation >= currentGeneration)
                    {
                        generations[value.SourceId] = value.Generation;
                        history.RemoveAll(x => x.SourceId == value.SourceId && x.Generation < value.Generation);
                    }
                    continue;
                }
                if (generations.TryGetValue(value.SourceId, out var generation))
                {
                    if (value.Generation < generation)
                        continue;
                    if (value.Generation > generation)
                        history.RemoveAll(x => x.SourceId == value.SourceId && x.Generation < value.Generation);
                }
                generations[value.SourceId] = value.Generation;
                history.Add(value);
            }
            if (history.Count > MaxHistoryCount)
                history.RemoveRange(0, history.Count - MaxHistoryCount);

            selectedValues.Clear();
            var targetTicks = targetPosition.Ticks;
            foreach (var value in history)
            {
                if (value.SampleRate <= 0)
                    continue;
                var positionTicks = TimeSpan.FromSeconds((double)value.SamplePosition / value.SampleRate).Ticks;
                if (positionTicks > targetTicks)
                    continue;
                if (!selectedValues.TryGetValue(value.ParamId, out var selected)
                    || positionTicks >= selected.PositionTicks)
                {
                    selectedValues[value.ParamId] = (positionTicks, value.NormalizedValue);
                }
            }
            foreach (var (paramId, value) in selectedValues)
                destination[paramId] = value.Value;
            return selectedValues.Count;
        }

        public void Dispose()
        {
            isDisposed = true;
            cursors.Clear();
            generations.Clear();
            history.Clear();
            received.Clear();
            selectedValues.Clear();
        }
    }

    internal sealed class Vst3EditorMeterForwarder : IDisposable
    {
        readonly Vst3MeterSubscription[] subscriptions;
        readonly Func<TimeSpan> getTimelinePosition;
        readonly Dictionary<uint, double> values = [];

        public Vst3EditorMeterForwarder(IEnumerable<Vst3AudioEffect> effects, Func<TimeSpan> getTimelinePosition)
        {
            subscriptions = [.. effects.Select(x => x.MeterChannel.Subscribe())];
            this.getTimelinePosition = getTimelinePosition;
        }

        public int Apply(Vst3Plugin plugin)
        {
            values.Clear();
            var targetPosition = getTimelinePosition();
            foreach (var subscription in subscriptions)
                subscription.CollectValues(targetPosition, values);
            foreach (var (paramId, normalizedValue) in values)
                plugin.SetControllerParameter(paramId, normalizedValue);
            return values.Count;
        }

        public void Dispose()
        {
            foreach (var subscription in subscriptions)
                subscription.Dispose();
            values.Clear();
        }
    }
}
