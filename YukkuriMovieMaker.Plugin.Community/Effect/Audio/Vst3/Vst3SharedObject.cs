using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    internal abstract class Vst3SharedObject
    {
        const int NoInterface = unchecked((int)0x80004002);

        static readonly ConcurrentDictionary<IntPtr, Vst3SharedObject> instances = new();

        readonly byte[][] supportedUids;
        readonly List<Delegate> rootedMethods = [];
        IntPtr vtable;
        int refCount = 1;

        public IntPtr Handle { get; private set; }

        protected Vst3SharedObject(params byte[][] supportedUids)
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
            instances[Handle] = this;
        }

        internal void ReleaseReference()
        {
            Release(Handle);
        }

        protected virtual void OnFinalRelease()
        {
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
                    AddRef(self);
                    obj = Handle;
                    return Vst3Native.ResultOk;
                }
            }
            obj = IntPtr.Zero;
            return NoInterface;
        }

        uint AddRef(IntPtr self) => (uint)Interlocked.Increment(ref refCount);

        uint Release(IntPtr self)
        {
            var count = Interlocked.Decrement(ref refCount);
            if (count > 0)
                return (uint)count;
            instances.TryRemove(Handle, out _);
            OnFinalRelease();
            Marshal.FreeHGlobal(Handle);
            Marshal.FreeHGlobal(vtable);
            Handle = IntPtr.Zero;
            vtable = IntPtr.Zero;
            return 0;
        }

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
