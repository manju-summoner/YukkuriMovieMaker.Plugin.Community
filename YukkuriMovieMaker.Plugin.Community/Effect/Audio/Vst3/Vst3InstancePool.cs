namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    internal static class Vst3InstancePool
    {
        const int MaxIdleEntries = 4;
        const int DefaultSampleRate = 48000;

        static readonly object gate = new();
        static readonly Dictionary<Vst3Effect, Vst3InstanceEntry> entries = [];
        static readonly List<Vst3InstanceEntry> idleOrder = [];

        public static Vst3InstanceLease AcquireProcessing(Vst3Effect effect, int sampleRate)
        {
            Vst3InstanceLease lease = null!;
            Vst3HostThread.Invoke(() => lease = AcquireProcessingCore(effect, sampleRate));
            return lease;
        }

        public static Vst3InstanceLease AcquireEditor(Vst3Effect effect)
        {
            Vst3InstanceLease lease = null!;
            Vst3HostThread.Invoke(() => lease = AcquireEditorCore(effect));
            return lease;
        }

        static Vst3InstanceLease AcquireProcessingCore(Vst3Effect effect, int sampleRate)
        {
            var entry = GetEntry(effect);
            lock (entry.Gate)
            {
                entry.EnsureInstance(sampleRate);
                entry.ApplyEffectStates();
                if (entry.HasProcessingLease)
                {
                    var transient = new Vst3Instance(
                        entry.Path, sampleRate,
                        Vst3StateCodec.Decode(effect.PluginState),
                        Vst3StateCodec.Decode(effect.ControllerState));
                    transient.SetProcessingLeased(true);
                    entry.AddTransient(transient);
                    return new Vst3InstanceLease(entry, transient, isProcessing: true, isTransient: true);
                }
                entry.HasProcessingLease = true;
                entry.Instance!.SetProcessingLeased(true);
                entry.Instance.EnsureSampleRate(sampleRate);
                entry.Instance.RefreshLatency();
                entry.LeaseCount++;
                return new Vst3InstanceLease(entry, entry.Instance, isProcessing: true, isTransient: false);
            }
        }

        static Vst3InstanceLease AcquireEditorCore(Vst3Effect effect)
        {
            var entry = GetEntry(effect);
            lock (entry.Gate)
            {
                entry.EnsureInstance(DefaultSampleRate);
                entry.ApplyEffectStates();
                entry.LeaseCount++;
                return new Vst3InstanceLease(entry, entry.Instance!, isProcessing: false, isTransient: false);
            }
        }

        static Vst3InstanceEntry GetEntry(Vst3Effect effect)
        {
            lock (gate)
            {
                if (entries.TryGetValue(effect, out var entry) && entry.Path != effect.FilePath)
                {
                    entries.Remove(effect);
                    idleOrder.Remove(entry);
                    entry.MarkRemoved();
                    entry = null;
                }
                if (entry is null)
                {
                    entry = new Vst3InstanceEntry(effect);
                    entries.Add(effect, entry);
                }
                idleOrder.Remove(entry);
                return entry;
            }
        }

        internal static void Release(Vst3InstanceEntry entry, Vst3Instance instance, bool isProcessing, bool isTransient)
        {
            try
            {
                Vst3HostThread.Invoke(() => ReleaseCore(entry, instance, isProcessing, isTransient));
            }
            catch (TimeoutException)
            {
            }
        }

        static void ReleaseCore(Vst3InstanceEntry entry, Vst3Instance instance, bool isProcessing, bool isTransient)
        {
            if (isTransient)
            {
                lock (entry.Gate)
                    entry.RemoveTransient(instance);
                instance.SetProcessingLeased(false);
                instance.Dispose();
                return;
            }

            bool isIdle;
            lock (entry.Gate)
            {
                if (isProcessing)
                {
                    entry.HasProcessingLease = false;
                    instance.SetProcessingLeased(false);
                }
                entry.LeaseCount--;
                isIdle = entry.LeaseCount == 0;
                if (isIdle && entry.IsRemoved)
                {
                    entry.DisposeInstance();
                    return;
                }
            }
            if (!isIdle)
                return;

            Vst3InstanceEntry? evicted = null;
            lock (gate)
            {
                if (entry.IsRemoved)
                    return;
                idleOrder.Remove(entry);
                idleOrder.Add(entry);
                if (idleOrder.Count > MaxIdleEntries)
                {
                    evicted = idleOrder[0];
                    idleOrder.RemoveAt(0);
                    entries.Remove(evicted.Effect);
                    evicted.MarkRemoved();
                }
            }
            if (evicted is not null)
            {
                lock (evicted.Gate)
                {
                    if (evicted.LeaseCount == 0)
                        evicted.DisposeInstance();
                }
            }
        }
    }

    internal sealed class Vst3InstanceEntry(Vst3Effect effect)
    {
        readonly List<Vst3Instance> transients = [];

        public object Gate { get; } = new();
        public Vst3Effect Effect { get; } = effect;
        public string Path { get; } = effect.FilePath;
        public Vst3Instance? Instance { get; private set; }
        public int LeaseCount { get; set; }
        public bool HasProcessingLease { get; set; }
        public bool IsRemoved { get; private set; }
        string appliedComponentState = effect.PluginState;
        string appliedControllerState = effect.ControllerState;

        public void EnsureInstance(int sampleRate)
        {
            if (Instance is not null)
                return;
            Instance = new Vst3Instance(
                Path, sampleRate,
                Vst3StateCodec.Decode(Effect.PluginState),
                Vst3StateCodec.Decode(Effect.ControllerState));
            appliedComponentState = Effect.PluginState;
            appliedControllerState = Effect.ControllerState;
            Instance.ParameterPerformed += OnParameterPerformed;
        }

        public void ApplyEffectStates()
        {
            if (Instance is null)
                return;
            var componentState = Effect.PluginState;
            var controllerState = Effect.ControllerState;
            if (appliedComponentState == componentState && appliedControllerState == controllerState)
                return;
            Instance.RestoreStates(Vst3StateCodec.Decode(componentState), Vst3StateCodec.Decode(controllerState));
            appliedComponentState = componentState;
            appliedControllerState = controllerState;
        }

        public (string ComponentState, string ControllerState) CaptureStates()
        {
            var (componentState, controllerState) = Instance!.CaptureStates();
            appliedComponentState = Vst3StateCodec.Encode(componentState);
            appliedControllerState = Vst3StateCodec.Encode(controllerState);
            return (appliedComponentState, appliedControllerState);
        }

        public void AddTransient(Vst3Instance instance)
        {
            transients.Add(instance);
        }

        public void RemoveTransient(Vst3Instance instance)
        {
            transients.Remove(instance);
        }

        public void MarkRemoved()
        {
            IsRemoved = true;
        }

        public void DisposeInstance()
        {
            if (Instance is null)
                return;
            Instance.ParameterPerformed -= OnParameterPerformed;
            Instance.Dispose();
            Instance = null;
        }

        void OnParameterPerformed(uint parameterId, double normalizedValue)
        {
            lock (Gate)
            {
                foreach (var transient in transients)
                    transient.QueueParameterChange(parameterId, normalizedValue);
            }
        }
    }

    internal sealed class Vst3InstanceLease : IDisposable
    {
        readonly Vst3InstanceEntry entry;
        readonly bool isProcessing;
        readonly bool isTransient;
        bool isDisposed;

        internal Vst3InstanceLease(Vst3InstanceEntry entry, Vst3Instance instance, bool isProcessing, bool isTransient)
        {
            this.entry = entry;
            this.isProcessing = isProcessing;
            this.isTransient = isTransient;
            Instance = instance;
        }

        public Vst3Instance Instance { get; }

        public (string ComponentState, string ControllerState) CaptureStates()
        {
            lock (entry.Gate)
                return entry.CaptureStates();
        }

        public void Dispose()
        {
            if (isDisposed)
                return;
            isDisposed = true;
            Vst3InstancePool.Release(entry, Instance, isProcessing, isTransient);
        }
    }

    internal static class Vst3StateCodec
    {
        public static byte[]? Decode(string state)
        {
            if (string.IsNullOrEmpty(state))
                return null;
            try
            {
                return Convert.FromBase64String(state);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        public static string Encode(byte[]? state) =>
            state is null ? string.Empty : Convert.ToBase64String(state);
    }
}
