using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    /// <summary>
    /// エディター専用インスタンスのパラメーター変更を、各音声処理インスタンスへ配信する。
    /// 購読者ごとに最新値を保持するため、複数ストリームが同じ変更を奪い合わない。
    /// </summary>
    internal sealed class Vst3ParameterChannel
    {
        readonly object sync = new();
        readonly Dictionary<uint, double> latestValues = [];
        readonly List<Vst3ParameterSubscription> subscriptions = [];

        public Vst3ParameterSubscription Subscribe()
        {
            lock (sync)
            {
                var subscription = new Vst3ParameterSubscription(this);
                CopyLatestTo(subscription);
                subscriptions.Add(subscription);
                return subscription;
            }
        }

        public void Publish(uint paramId, double normalizedValue)
        {
            lock (sync)
            {
                latestValues[paramId] = normalizedValue;
                foreach (var subscription in subscriptions)
                    subscription.Enqueue(paramId, normalizedValue);
            }
        }

        internal void ReplayLatest(Vst3ParameterSubscription subscription)
        {
            lock (sync)
                CopyLatestTo(subscription);
        }

        internal void Unsubscribe(Vst3ParameterSubscription subscription)
        {
            lock (sync)
                subscriptions.Remove(subscription);
        }

        /// <summary>
        /// 配信済み・保留中のパラメーター値をすべて破棄する。
        /// Undo/Redoで状態を巻き戻したとき、巻き戻し前のGUI編集値が
        /// ReplayLatest経由で復元状態を上書きしないようにするために使う
        /// </summary>
        public void Clear()
        {
            lock (sync)
            {
                latestValues.Clear();
                foreach (var subscription in subscriptions)
                    subscription.ClearPending();
            }
        }

        void CopyLatestTo(Vst3ParameterSubscription subscription)
        {
            foreach (var (paramId, normalizedValue) in latestValues)
                subscription.Enqueue(paramId, normalizedValue);
        }
    }

    internal sealed class Vst3ParameterSubscription(Vst3ParameterChannel owner) : IDisposable
    {
        readonly ConcurrentDictionary<uint, double> pendingValues = new();
        volatile bool isDisposed;

        internal void Enqueue(uint paramId, double normalizedValue)
        {
            if (!isDisposed)
                pendingValues[paramId] = normalizedValue;
        }

        internal void ClearPending() => pendingValues.Clear();

        public void ReplayLatest() => owner.ReplayLatest(this);

        public int ApplyTo(Vst3Plugin plugin)
        {
            var count = 0;
            foreach (var (paramId, _) in pendingValues)
            {
                if (!pendingValues.TryRemove(paramId, out var normalizedValue))
                    continue;
                plugin.SetParameter(paramId, normalizedValue);
                count++;
            }
            return count;
        }

        public void Dispose()
        {
            if (isDisposed)
                return;
            isDisposed = true;
            owner.Unsubscribe(this);
            pendingValues.Clear();
        }
    }

    internal sealed class Vst3EditorParameterForwarder
    {
        readonly Vst3ParameterChannel[] channels;

        public Vst3EditorParameterForwarder(IEnumerable<Vst3AudioEffect> effects)
        {
            channels = [.. effects.Select(x => x.ParameterChannel).Distinct()];
        }

        public int PumpAndForward(Vst3Plugin plugin)
        {
            plugin.Pump();
            return plugin.DrainEditorParameterChanges((paramId, normalizedValue) =>
            {
                foreach (var channel in channels)
                    channel.Publish(paramId, normalizedValue);
            });
        }
    }
}
