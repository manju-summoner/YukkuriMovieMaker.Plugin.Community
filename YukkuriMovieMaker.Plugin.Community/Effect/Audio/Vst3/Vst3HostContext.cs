using System.Runtime.InteropServices;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    internal static class Vst3HostContext
    {
        const int NoInterface = unchecked((int)0x80004002);
        const string HostName = "YukkuriMovieMaker";

        static readonly byte[] FUnknownUid = Vst3Native.FUnknownUid;
        static readonly byte[] IHostApplicationUid = Vst3Native.IHostApplicationUid;

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        delegate int QueryInterfaceDelegate(IntPtr self, IntPtr iid, out IntPtr obj);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        delegate uint RefCountDelegate(IntPtr self);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        delegate int GetNameDelegate(IntPtr self, IntPtr name);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        delegate int CreateInstanceDelegate(IntPtr self, IntPtr cid, IntPtr iid, out IntPtr obj);

        static readonly QueryInterfaceDelegate queryInterface = QueryInterface;
        static readonly RefCountDelegate addRef = AddRef;
        static readonly RefCountDelegate release = AddRef;
        static readonly GetNameDelegate getName = GetName;
        static readonly CreateInstanceDelegate createInstance = CreateInstance;

        public static IntPtr Instance { get; } = CreateNativeInstance();

        static IntPtr CreateNativeInstance()
        {
            var vtable = Marshal.AllocHGlobal(5 * IntPtr.Size);
            Marshal.WriteIntPtr(vtable, 0 * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(queryInterface));
            Marshal.WriteIntPtr(vtable, 1 * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(addRef));
            Marshal.WriteIntPtr(vtable, 2 * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(release));
            Marshal.WriteIntPtr(vtable, 3 * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(getName));
            Marshal.WriteIntPtr(vtable, 4 * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(createInstance));

            var instance = Marshal.AllocHGlobal(IntPtr.Size);
            Marshal.WriteIntPtr(instance, vtable);
            return instance;
        }

        static int QueryInterface(IntPtr self, IntPtr iid, out IntPtr obj)
        {
            if (UidEquals(iid, FUnknownUid) || UidEquals(iid, IHostApplicationUid))
            {
                obj = self;
                return Vst3Native.ResultOk;
            }
            obj = IntPtr.Zero;
            return NoInterface;
        }

        static uint AddRef(IntPtr self) => 1;

        static int GetName(IntPtr self, IntPtr name)
        {
            var characters = HostName.AsSpan();
            for (var i = 0; i < characters.Length; i++)
                Marshal.WriteInt16(name, i * sizeof(char), (short)characters[i]);
            Marshal.WriteInt16(name, characters.Length * sizeof(char), 0);
            return Vst3Native.ResultOk;
        }

        static int CreateInstance(IntPtr self, IntPtr cid, IntPtr iid, out IntPtr obj)
        {
            if (UidEquals(cid, Vst3Native.IMessageUid) && UidEquals(iid, Vst3Native.IMessageUid))
            {
                obj = new Vst3Message().Handle;
                return Vst3Native.ResultOk;
            }
            if (UidEquals(cid, Vst3Native.IAttributeListUid) && UidEquals(iid, Vst3Native.IAttributeListUid))
            {
                obj = new Vst3AttributeList().Handle;
                return Vst3Native.ResultOk;
            }
            obj = IntPtr.Zero;
            return NoInterface;
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
