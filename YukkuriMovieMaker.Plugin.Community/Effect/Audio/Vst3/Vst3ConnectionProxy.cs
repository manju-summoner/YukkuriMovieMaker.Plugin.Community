namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    internal sealed class Vst3ConnectionProxy : Vst3SharedObject
    {
        readonly object gate = new();
        IntPtr target;

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
                return Forward(message);
            Vst3Native.GetVtableMethod<Vst3Native.ReleaseDelegate>(message, 1)(message);
            Vst3HostThread.Post(() =>
            {
                Forward(message);
                Vst3Native.GetVtableMethod<Vst3Native.ReleaseDelegate>(message, 2)(message);
            });
            return Vst3Native.ResultOk;
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
    }
}
