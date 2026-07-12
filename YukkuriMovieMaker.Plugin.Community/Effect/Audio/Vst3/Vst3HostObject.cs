using System.Runtime.InteropServices;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    internal abstract class Vst3HostObject : IDisposable
    {
        const int NoInterface = unchecked((int)0x80004002);

        readonly byte[][] supportedUids;
        readonly List<Delegate> rootedMethods = [];
        IntPtr vtable;
        bool isDisposed;

        public IntPtr Handle { get; private set; }

        protected Vst3HostObject(params byte[][] supportedUids)
        {
            this.supportedUids = [Vst3Native.FUnknownUid, .. supportedUids];
        }

        protected void BuildVtable(params Delegate[] interfaceMethods)
        {
            rootedMethods.Add(new QueryInterfaceDelegate(QueryInterface));
            rootedMethods.Add(new RefCountDelegate(AddRef));
            rootedMethods.Add(new RefCountDelegate(Release));
            rootedMethods.AddRange(interfaceMethods);

            vtable = Marshal.AllocHGlobal(rootedMethods.Count * IntPtr.Size);
            for (var i = 0; i < rootedMethods.Count; i++)
                Marshal.WriteIntPtr(vtable, i * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(rootedMethods[i]));

            Handle = Marshal.AllocHGlobal(IntPtr.Size);
            Marshal.WriteIntPtr(Handle, vtable);
        }

        public virtual void Dispose()
        {
            if (isDisposed)
                return;
            isDisposed = true;
            if (Handle != IntPtr.Zero)
                Marshal.FreeHGlobal(Handle);
            if (vtable != IntPtr.Zero)
                Marshal.FreeHGlobal(vtable);
            Handle = IntPtr.Zero;
            vtable = IntPtr.Zero;
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        delegate int QueryInterfaceDelegate(IntPtr self, IntPtr iid, out IntPtr obj);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        delegate uint RefCountDelegate(IntPtr self);

        int QueryInterface(IntPtr self, IntPtr iid, out IntPtr obj)
        {
            foreach (var uid in supportedUids)
            {
                if (UidEquals(iid, uid))
                {
                    obj = self;
                    return Vst3Native.ResultOk;
                }
            }
            obj = IntPtr.Zero;
            return NoInterface;
        }

        uint AddRef(IntPtr self) => 1;

        uint Release(IntPtr self) => 1;

        static bool UidEquals(IntPtr iid, byte[] uid)
        {
            for (var i = 0; i < uid.Length; i++)
            {
                if (Marshal.ReadByte(iid, i) != uid[i])
                    return false;
            }
            return true;
        }
    }
}
