using System.Runtime.InteropServices;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    internal sealed class Vst3OutputParameterChanges : Vst3HostObject
    {
        const int Capacity = 128;

        readonly ParamValueQueue?[] queues = new ParamValueQueue?[Capacity];
        int usedCount;

        public Vst3OutputParameterChanges() : base(Vst3Native.IParameterChangesUid)
        {
            BuildVtable(
                new GetParameterCountDelegate(GetParameterCount),
                new GetParameterDataDelegate(GetParameterData),
                new AddParameterDataDelegate(AddParameterData));
        }

        public void Drain(Action<uint, double> collector)
        {
            for (var i = 0; i < usedCount; i++)
            {
                var queue = queues[i]!;
                if (queue.HasValue)
                    collector(queue.ParameterId, queue.Value);
                queue.HasValue = false;
            }
            usedCount = 0;
        }

        public override void Dispose()
        {
            foreach (var queue in queues)
                queue?.Dispose();
            base.Dispose();
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        delegate int GetParameterCountDelegate(IntPtr self);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        delegate IntPtr GetParameterDataDelegate(IntPtr self, int index);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        delegate IntPtr AddParameterDataDelegate(IntPtr self, ref uint id, ref int index);

        int GetParameterCount(IntPtr self) => usedCount;

        IntPtr GetParameterData(IntPtr self, int index) =>
            index >= 0 && index < usedCount ? queues[index]!.Handle : IntPtr.Zero;

        IntPtr AddParameterData(IntPtr self, ref uint id, ref int index)
        {
            for (var i = 0; i < usedCount; i++)
            {
                if (queues[i]!.ParameterId == id)
                {
                    index = i;
                    return queues[i]!.Handle;
                }
            }
            if (usedCount >= Capacity)
                return IntPtr.Zero;
            var queue = queues[usedCount] ??= new ParamValueQueue();
            queue.ParameterId = id;
            queue.HasValue = false;
            index = usedCount++;
            return queue.Handle;
        }

        sealed class ParamValueQueue : Vst3HostObject
        {
            public uint ParameterId;
            public double Value;
            public bool HasValue;

            public ParamValueQueue() : base(Vst3Native.IParamValueQueueUid)
            {
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

            uint GetParameterId(IntPtr self) => ParameterId;

            int GetPointCount(IntPtr self) => HasValue ? 1 : 0;

            int GetPoint(IntPtr self, int index, ref int sampleOffset, ref double value)
            {
                if (index != 0 || !HasValue)
                    return Vst3Native.ResultFalse;
                sampleOffset = 0;
                value = Value;
                return Vst3Native.ResultOk;
            }

            int AddPoint(IntPtr self, int sampleOffset, double value, ref int index)
            {
                Value = value;
                HasValue = true;
                index = 0;
                return Vst3Native.ResultOk;
            }
        }
    }
}
