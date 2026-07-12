using System.Runtime.InteropServices;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    internal sealed class Vst3ParameterChanges : Vst3HostObject
    {
        readonly ParamValueQueue[] queues;

        public Vst3ParameterChanges(IReadOnlyList<KeyValuePair<uint, double>> edits) : base(Vst3Native.IParameterChangesUid)
        {
            queues = [.. edits.Select(edit => new ParamValueQueue(edit.Key, edit.Value))];
            BuildVtable(
                new GetParameterCountDelegate(GetParameterCount),
                new GetParameterDataDelegate(GetParameterData),
                new AddParameterDataDelegate(AddParameterData));
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        delegate int GetParameterCountDelegate(IntPtr self);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        delegate IntPtr GetParameterDataDelegate(IntPtr self, int index);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        delegate IntPtr AddParameterDataDelegate(IntPtr self, ref uint id, ref int index);

        int GetParameterCount(IntPtr self) => queues.Length;

        IntPtr GetParameterData(IntPtr self, int index) =>
            index >= 0 && index < queues.Length ? queues[index].Handle : IntPtr.Zero;

        IntPtr AddParameterData(IntPtr self, ref uint id, ref int index) => IntPtr.Zero;

        public override void Dispose()
        {
            foreach (var queue in queues)
                queue.Dispose();
            base.Dispose();
        }

        sealed class ParamValueQueue : Vst3HostObject
        {
            readonly uint parameterId;
            readonly double normalizedValue;

            public ParamValueQueue(uint parameterId, double normalizedValue) : base(Vst3Native.IParamValueQueueUid)
            {
                this.parameterId = parameterId;
                this.normalizedValue = normalizedValue;
                BuildVtable(
                    new GetParameterIdDelegate(GetParameterId),
                    new GetPointCountDelegate(GetPointCount),
                    new GetPointDelegate(GetPoint),
                    new AddPointDelegate(AddPoint));
            }

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            delegate uint GetParameterIdDelegate(IntPtr self);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            delegate int GetPointCountDelegate(IntPtr self);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            delegate int GetPointDelegate(IntPtr self, int index, ref int sampleOffset, ref double value);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            delegate int AddPointDelegate(IntPtr self, int sampleOffset, double value, ref int index);

            uint GetParameterId(IntPtr self) => parameterId;

            int GetPointCount(IntPtr self) => 1;

            int GetPoint(IntPtr self, int index, ref int sampleOffset, ref double value)
            {
                if (index != 0)
                    return 1;
                sampleOffset = 0;
                value = normalizedValue;
                return Vst3Native.ResultOk;
            }

            int AddPoint(IntPtr self, int sampleOffset, double value, ref int index) => 1;
        }
    }
}
