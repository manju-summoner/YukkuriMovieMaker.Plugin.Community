using System.Runtime.InteropServices;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    internal sealed class Vst3AttributeList : Vst3SharedObject
    {
        readonly object gate = new();
        readonly Dictionary<string, long> ints = [];
        readonly Dictionary<string, double> floats = [];
        readonly Dictionary<string, string> strings = [];
        readonly Dictionary<string, (IntPtr Data, uint Size)> binaries = [];

        public Vst3AttributeList() : base(Vst3Native.IAttributeListUid)
        {
            BuildVtable(
                new SetIntDelegate(SetInt),
                new GetIntDelegate(GetInt),
                new SetFloatDelegate(SetFloat),
                new GetFloatDelegate(GetFloat),
                new SetStringDelegate(SetString),
                new GetStringDelegate(GetString),
                new SetBinaryDelegate(SetBinary),
                new GetBinaryDelegate(GetBinary));
        }

        protected override void OnFinalRelease()
        {
            lock (gate)
            {
                foreach (var binary in binaries.Values)
                    Marshal.FreeHGlobal(binary.Data);
                binaries.Clear();
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        delegate int SetIntDelegate(IntPtr self, IntPtr id, long value);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        delegate int GetIntDelegate(IntPtr self, IntPtr id, out long value);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        delegate int SetFloatDelegate(IntPtr self, IntPtr id, double value);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        delegate int GetFloatDelegate(IntPtr self, IntPtr id, out double value);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        delegate int SetStringDelegate(IntPtr self, IntPtr id, IntPtr value);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        delegate int GetStringDelegate(IntPtr self, IntPtr id, IntPtr value, uint sizeInBytes);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        delegate int SetBinaryDelegate(IntPtr self, IntPtr id, IntPtr data, uint sizeInBytes);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        delegate int GetBinaryDelegate(IntPtr self, IntPtr id, out IntPtr data, out uint sizeInBytes);

        int SetInt(IntPtr self, IntPtr id, long value)
        {
            if (ReadId(id) is not string key)
                return Vst3Native.ResultFalse;
            lock (gate)
            {
                RemoveKey(key);
                ints[key] = value;
            }
            return Vst3Native.ResultOk;
        }

        int GetInt(IntPtr self, IntPtr id, out long value)
        {
            value = 0;
            if (ReadId(id) is not string key)
                return Vst3Native.ResultFalse;
            lock (gate)
                return ints.TryGetValue(key, out value) ? Vst3Native.ResultOk : Vst3Native.ResultFalse;
        }

        int SetFloat(IntPtr self, IntPtr id, double value)
        {
            if (ReadId(id) is not string key)
                return Vst3Native.ResultFalse;
            lock (gate)
            {
                RemoveKey(key);
                floats[key] = value;
            }
            return Vst3Native.ResultOk;
        }

        int GetFloat(IntPtr self, IntPtr id, out double value)
        {
            value = 0;
            if (ReadId(id) is not string key)
                return Vst3Native.ResultFalse;
            lock (gate)
                return floats.TryGetValue(key, out value) ? Vst3Native.ResultOk : Vst3Native.ResultFalse;
        }

        int SetString(IntPtr self, IntPtr id, IntPtr value)
        {
            if (ReadId(id) is not string key || value == IntPtr.Zero)
                return Vst3Native.ResultFalse;
            var text = Marshal.PtrToStringUni(value) ?? string.Empty;
            lock (gate)
            {
                RemoveKey(key);
                strings[key] = text;
            }
            return Vst3Native.ResultOk;
        }

        int GetString(IntPtr self, IntPtr id, IntPtr value, uint sizeInBytes)
        {
            if (ReadId(id) is not string key || value == IntPtr.Zero || sizeInBytes < sizeof(char))
                return Vst3Native.ResultFalse;
            string? text;
            lock (gate)
            {
                if (!strings.TryGetValue(key, out text))
                    return Vst3Native.ResultFalse;
            }
            var capacity = (int)(sizeInBytes / sizeof(char)) - 1;
            var length = Math.Min(text.Length, capacity);
            for (var i = 0; i < length; i++)
                Marshal.WriteInt16(value, i * sizeof(char), (short)text[i]);
            Marshal.WriteInt16(value, length * sizeof(char), 0);
            return Vst3Native.ResultOk;
        }

        int SetBinary(IntPtr self, IntPtr id, IntPtr data, uint sizeInBytes)
        {
            if (ReadId(id) is not string key)
                return Vst3Native.ResultFalse;
            var copy = Marshal.AllocHGlobal((int)sizeInBytes);
            if (data != IntPtr.Zero && sizeInBytes > 0)
                unsafe
                {
                    Buffer.MemoryCopy((void*)data, (void*)copy, sizeInBytes, sizeInBytes);
                }
            lock (gate)
            {
                RemoveKey(key);
                binaries[key] = (copy, sizeInBytes);
            }
            return Vst3Native.ResultOk;
        }

        int GetBinary(IntPtr self, IntPtr id, out IntPtr data, out uint sizeInBytes)
        {
            data = IntPtr.Zero;
            sizeInBytes = 0;
            if (ReadId(id) is not string key)
                return Vst3Native.ResultFalse;
            lock (gate)
            {
                if (!binaries.TryGetValue(key, out var binary))
                    return Vst3Native.ResultFalse;
                (data, sizeInBytes) = binary;
            }
            return Vst3Native.ResultOk;
        }

        void RemoveKey(string key)
        {
            ints.Remove(key);
            floats.Remove(key);
            strings.Remove(key);
            if (binaries.Remove(key, out var binary))
                Marshal.FreeHGlobal(binary.Data);
        }

        static string? ReadId(IntPtr id) =>
            id == IntPtr.Zero ? null : Marshal.PtrToStringAnsi(id);
    }
}
