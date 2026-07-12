using System.Windows.Threading;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    internal sealed class Vst3ConnectionProxy : Vst3SharedObject
    {
        readonly object gate = new();
        readonly Queue<IntPtr> pendingMessages = new();
        IntPtr target;
        bool isDrainScheduled;

        public Vst3ConnectionProxy(IntPtr target) : base(Vst3Native.IConnectionPointUid)
        {
            this.target = target;
            BuildVtable(
                new Vst3Native.ConnectDelegate(Connect),
                new Vst3Native.ConnectDelegate(Disconnect),
                new Vst3Native.NotifyDelegate(Notify));
        }

        public void Invalidate()
        {
            lock (gate)
                target = IntPtr.Zero;
        }

        int Connect(IntPtr self, IntPtr other) => Vst3Native.ResultOk;

        int Disconnect(IntPtr self, IntPtr other) => Vst3Native.ResultOk;

        int Notify(IntPtr self, IntPtr message)
        {
            if (message == IntPtr.Zero)
                return Vst3Native.ResultFalse;
            if (Vst3HostThread.CheckAccess())
            {
                var enqueued = false;
                lock (gate)
                {
                    if (isDrainScheduled)
                    {
                        AddRef(message);
                        pendingMessages.Enqueue(message);
                        enqueued = true;
                    }
                }
                if (enqueued)
                    return Vst3Native.ResultOk;
                return Forward(message);
            }

            AddRef(message);
            var schedule = false;
            lock (gate)
            {
                pendingMessages.Enqueue(message);
                if (!isDrainScheduled)
                {
                    isDrainScheduled = true;
                    schedule = true;
                }
            }
            if (schedule)
                Vst3HostThread.Post(Drain, DispatcherPriority.Background);
            return Vst3Native.ResultOk;
        }

        void Drain()
        {
            while (true)
            {
                IntPtr message;
                lock (gate)
                {
                    if (pendingMessages.Count == 0)
                    {
                        isDrainScheduled = false;
                        return;
                    }
                    message = pendingMessages.Dequeue();
                }
                Forward(message);
                Release(message);
            }
        }

        int Forward(IntPtr message)
        {
            IntPtr current;
            lock (gate)
                current = target;
            if (current == IntPtr.Zero)
                return Vst3Native.ResultFalse;
            return Vst3Native.GetVtableMethod<Vst3Native.NotifyDelegate>(current, 5)(current, message);
        }

        static void AddRef(IntPtr message) =>
            Vst3Native.GetVtableMethod<Vst3Native.ReleaseDelegate>(message, 1)(message);

        static void Release(IntPtr message) =>
            Vst3Native.GetVtableMethod<Vst3Native.ReleaseDelegate>(message, 2)(message);
    }
}
