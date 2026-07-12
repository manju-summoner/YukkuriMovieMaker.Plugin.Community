using System.Numerics;
using System.Runtime.InteropServices;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    internal sealed unsafe class Vst3ProcessBuffers : IDisposable
    {
        IntPtr memory;

        public IntPtr Inputs { get; }
        public IntPtr Outputs { get; }
        public int[] InputChannelCounts { get; }
        public int[] OutputChannelCounts { get; }

        public Vst3ProcessBuffers(IntPtr processor, int inputBusCount, int outputBusCount, int maxBlockFrames)
        {
            var setBusArrangements = Vst3Native.GetVtableMethod<Vst3Native.SetBusArrangementsDelegate>(processor, 3);
            var inputArrangements = new ulong[inputBusCount];
            var outputArrangements = new ulong[outputBusCount];
            Array.Fill(inputArrangements, Vst3Native.SpeakerArrangementStereo);
            Array.Fill(outputArrangements, Vst3Native.SpeakerArrangementStereo);
            setBusArrangements(processor, inputArrangements, inputBusCount, outputArrangements, outputBusCount);

            var getBusArrangement = Vst3Native.GetVtableMethod<Vst3Native.GetBusArrangementDelegate>(processor, 4);
            InputChannelCounts = GetChannelCounts(processor, getBusArrangement, Vst3Native.BusDirectionInput, inputBusCount);
            OutputChannelCounts = GetChannelCounts(processor, getBusArrangement, Vst3Native.BusDirectionOutput, outputBusCount);

            var busStructSize = sizeof(Vst3Native.AudioBusBuffers);
            var totalChannels = InputChannelCounts.Sum() + OutputChannelCounts.Sum();
            var totalBusses = InputChannelCounts.Length + OutputChannelCounts.Length;
            var size = totalBusses * busStructSize + totalChannels * (IntPtr.Size + maxBlockFrames * sizeof(float));
            memory = Marshal.AllocHGlobal(size);
            NativeMemory.Clear((void*)memory, (nuint)size);

            Inputs = memory;
            Outputs = memory + InputChannelCounts.Length * busStructSize;
            var pointerTable = Outputs + OutputChannelCounts.Length * busStructSize;
            var sampleBuffers = pointerTable + totalChannels * IntPtr.Size;

            var busses = (Vst3Native.AudioBusBuffers*)memory;
            var channels = (float**)pointerTable;
            var samples = (float*)sampleBuffers;
            foreach (var channelCount in InputChannelCounts.Concat(OutputChannelCounts))
            {
                busses->NumChannels = channelCount;
                busses->SilenceFlags = 0;
                busses->ChannelBuffers = (IntPtr)channels;
                busses++;
                for (var i = 0; i < channelCount; i++)
                {
                    *channels++ = samples;
                    samples += maxBlockFrames;
                }
            }
        }

        public void Dispose()
        {
            if (memory == IntPtr.Zero)
                return;
            Marshal.FreeHGlobal(memory);
            memory = IntPtr.Zero;
        }

        static int[] GetChannelCounts(IntPtr processor, Vst3Native.GetBusArrangementDelegate getBusArrangement, int direction, int busCount)
        {
            var counts = new int[busCount];
            for (var i = 0; i < busCount; i++)
            {
                ulong arrangement = 0;
                if (getBusArrangement(processor, direction, i, ref arrangement) == Vst3Native.ResultOk)
                    counts[i] = BitOperations.PopCount(arrangement);
            }
            return counts;
        }
    }
}
