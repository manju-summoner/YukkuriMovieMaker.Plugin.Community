using System.Numerics;
using System.Runtime.InteropServices;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    internal sealed unsafe class Vst3Plugin : IDisposable
    {
        readonly int maxBlockFrames;
        Vst3Module module = null!;
        IntPtr component;
        IntPtr processor;
        Vst3Native.SetProcessingDelegate setProcessing = null!;
        Vst3Native.ProcessDelegate process = null!;
        int[] inputChannelCounts = [];
        int[] outputChannelCounts = [];
        IntPtr memory;
        Vst3Native.ProcessData processData;
        bool isDisposed;
        readonly object pendingEditsGate = new();
        readonly Dictionary<uint, double> pendingEdits = [];

        public Vst3Plugin(string path, int sampleRate, int maxBlockFrames, byte[]? componentState = null)
        {
            this.maxBlockFrames = maxBlockFrames;
            Vst3HostThread.Invoke(() => Initialize(path, sampleRate, componentState));
        }

        void Initialize(string path, int sampleRate, byte[]? componentState)
        {
            module = Vst3Module.Acquire(path);
            try
            {
                component = module.CreateAudioComponent();

                var queryInterface = Vst3Native.GetVtableMethod<Vst3Native.QueryInterfaceDelegate>(component, 0);
                if (queryInterface(component, Vst3Native.IAudioProcessorUid, out processor) != Vst3Native.ResultOk
                    || processor == IntPtr.Zero)
                    throw new InvalidOperationException($"IAudioProcessor is not supported: {path}");

                var initialize = Vst3Native.GetVtableMethod<Vst3Native.InitializeDelegate>(component, 3);
                if (initialize(component, Vst3HostContext.Instance) != Vst3Native.ResultOk)
                    throw new InvalidOperationException($"IComponent::initialize failed: {path}");

                if (componentState is not null)
                {
                    var setState = Vst3Native.GetVtableMethod<Vst3Native.StreamDelegate>(component, 12);
                    using var stream = new Vst3BStream(componentState);
                    setState(component, stream.Handle);
                }

                var canProcessSampleSize = Vst3Native.GetVtableMethod<Vst3Native.CanProcessSampleSizeDelegate>(processor, 5);
                if (canProcessSampleSize(processor, Vst3Native.SymbolicSampleSize32) != Vst3Native.ResultOk)
                    throw new InvalidOperationException($"32bit processing is not supported: {path}");

                var getBusCount = Vst3Native.GetVtableMethod<Vst3Native.GetBusCountDelegate>(component, 7);
                var inputBusCount = getBusCount(component, Vst3Native.MediaTypeAudio, Vst3Native.BusDirectionInput);
                var outputBusCount = getBusCount(component, Vst3Native.MediaTypeAudio, Vst3Native.BusDirectionOutput);
                if (inputBusCount < 1 || outputBusCount < 1)
                    throw new InvalidOperationException($"Audio input and output busses are required: {path}");

                var setBusArrangements = Vst3Native.GetVtableMethod<Vst3Native.SetBusArrangementsDelegate>(processor, 3);
                var inputArrangements = new ulong[inputBusCount];
                var outputArrangements = new ulong[outputBusCount];
                Array.Fill(inputArrangements, Vst3Native.SpeakerArrangementStereo);
                Array.Fill(outputArrangements, Vst3Native.SpeakerArrangementStereo);
                setBusArrangements(processor, inputArrangements, inputBusCount, outputArrangements, outputBusCount);

                var getBusArrangement = Vst3Native.GetVtableMethod<Vst3Native.GetBusArrangementDelegate>(processor, 4);
                inputChannelCounts = GetChannelCounts(getBusArrangement, Vst3Native.BusDirectionInput, inputBusCount);
                outputChannelCounts = GetChannelCounts(getBusArrangement, Vst3Native.BusDirectionOutput, outputBusCount);
                if (inputChannelCounts[0] < 1 || outputChannelCounts[0] < 1)
                    throw new InvalidOperationException($"Main busses have no channels: {path}");

                var setup = new Vst3Native.ProcessSetup
                {
                    ProcessMode = Vst3Native.ProcessModeRealtime,
                    SymbolicSampleSize = Vst3Native.SymbolicSampleSize32,
                    MaxSamplesPerBlock = maxBlockFrames,
                    SampleRate = sampleRate,
                };
                var setupProcessing = Vst3Native.GetVtableMethod<Vst3Native.SetupProcessingDelegate>(processor, 7);
                if (setupProcessing(processor, ref setup) != Vst3Native.ResultOk)
                    throw new InvalidOperationException($"IAudioProcessor::setupProcessing failed: {path}");

                memory = AllocateBuffers(out var inputs, out var outputs);
                processData = new Vst3Native.ProcessData
                {
                    ProcessMode = Vst3Native.ProcessModeRealtime,
                    SymbolicSampleSize = Vst3Native.SymbolicSampleSize32,
                    NumInputs = inputBusCount,
                    NumOutputs = outputBusCount,
                    Inputs = inputs,
                    Outputs = outputs,
                };

                var activateBus = Vst3Native.GetVtableMethod<Vst3Native.ActivateBusDelegate>(component, 10);
                activateBus(component, Vst3Native.MediaTypeAudio, Vst3Native.BusDirectionInput, 0, 1);
                activateBus(component, Vst3Native.MediaTypeAudio, Vst3Native.BusDirectionOutput, 0, 1);

                var setActive = Vst3Native.GetVtableMethod<Vst3Native.SetActiveDelegate>(component, 11);
                if (setActive(component, 1) != Vst3Native.ResultOk)
                    throw new InvalidOperationException($"IComponent::setActive failed: {path}");

                setProcessing = Vst3Native.GetVtableMethod<Vst3Native.SetProcessingDelegate>(processor, 8);
                process = Vst3Native.GetVtableMethod<Vst3Native.ProcessDelegate>(processor, 9);
                setProcessing(processor, 1);
            }
            catch
            {
                ReleaseNativeResources();
                throw;
            }
        }

        public void QueueParameterChange(uint parameterId, double normalizedValue)
        {
            lock (pendingEditsGate)
                pendingEdits[parameterId] = normalizedValue;
        }

        public void Process(float[] buffer, int offset, int frames)
        {
            if (isDisposed)
                return;

            using var changes = DrainPendingEdits();
            processData.InputParameterChanges = changes?.Handle ?? IntPtr.Zero;
            try
            {
                while (frames > 0)
                {
                    var blockFrames = Math.Min(frames, maxBlockFrames);
                    ProcessBlock(buffer, offset, blockFrames);
                    processData.InputParameterChanges = IntPtr.Zero;
                    offset += blockFrames * 2;
                    frames -= blockFrames;
                }
            }
            finally
            {
                processData.InputParameterChanges = IntPtr.Zero;
            }
        }

        Vst3ParameterChanges? DrainPendingEdits()
        {
            KeyValuePair<uint, double>[] edits;
            lock (pendingEditsGate)
            {
                if (pendingEdits.Count == 0)
                    return null;
                edits = [.. pendingEdits];
                pendingEdits.Clear();
            }
            return new Vst3ParameterChanges(edits);
        }

        public void Reset()
        {
            if (isDisposed)
                return;
            setProcessing(processor, 0);
            setProcessing(processor, 1);
        }

        public void Dispose()
        {
            if (isDisposed)
                return;
            isDisposed = true;
            try
            {
                Vst3HostThread.Invoke(() =>
                {
                    setProcessing(processor, 0);
                    Vst3Native.GetVtableMethod<Vst3Native.SetActiveDelegate>(component, 11)(component, 0);
                    Vst3Native.GetVtableMethod<Vst3Native.TerminateDelegate>(component, 4)(component);
                    ReleaseNativeResources();
                });
            }
            catch (TimeoutException)
            {
            }
        }

        void ProcessBlock(float[] buffer, int offset, int frames)
        {
            var inputs = (Vst3Native.AudioBusBuffers*)processData.Inputs;
            var mainInput = (float**)inputs[0].ChannelBuffers;
            fixed (float* source = &buffer[offset])
            {
                if (inputChannelCounts[0] == 1)
                {
                    for (var i = 0; i < frames; i++)
                        mainInput[0][i] = (source[i * 2] + source[i * 2 + 1]) * 0.5f;
                }
                else
                {
                    for (var i = 0; i < frames; i++)
                    {
                        mainInput[0][i] = source[i * 2];
                        mainInput[1][i] = source[i * 2 + 1];
                    }
                }
            }

            processData.NumSamples = frames;
            if (process(processor, ref processData) != Vst3Native.ResultOk)
                return;

            var outputs = (Vst3Native.AudioBusBuffers*)processData.Outputs;
            var mainOutput = (float**)outputs[0].ChannelBuffers;
            fixed (float* destination = &buffer[offset])
            {
                if (outputChannelCounts[0] == 1)
                {
                    for (var i = 0; i < frames; i++)
                    {
                        destination[i * 2] = mainOutput[0][i];
                        destination[i * 2 + 1] = mainOutput[0][i];
                    }
                }
                else
                {
                    for (var i = 0; i < frames; i++)
                    {
                        destination[i * 2] = mainOutput[0][i];
                        destination[i * 2 + 1] = mainOutput[1][i];
                    }
                }
            }
        }

        IntPtr AllocateBuffers(out IntPtr inputs, out IntPtr outputs)
        {
            var busStructSize = sizeof(Vst3Native.AudioBusBuffers);
            var totalChannels = inputChannelCounts.Sum() + outputChannelCounts.Sum();
            var totalBusses = inputChannelCounts.Length + outputChannelCounts.Length;
            var size = totalBusses * busStructSize + totalChannels * (IntPtr.Size + maxBlockFrames * sizeof(float));
            var block = Marshal.AllocHGlobal(size);
            NativeMemory.Clear((void*)block, (nuint)size);

            inputs = block;
            outputs = block + inputChannelCounts.Length * busStructSize;
            var pointerTable = outputs + outputChannelCounts.Length * busStructSize;
            var sampleBuffers = pointerTable + totalChannels * IntPtr.Size;

            var busses = (Vst3Native.AudioBusBuffers*)block;
            var channels = (float**)pointerTable;
            var samples = (float*)sampleBuffers;
            foreach (var channelCount in inputChannelCounts.Concat(outputChannelCounts))
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
            return block;
        }

        int[] GetChannelCounts(Vst3Native.GetBusArrangementDelegate getBusArrangement, int direction, int busCount)
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

        void ReleaseNativeResources()
        {
            if (memory != IntPtr.Zero)
                Marshal.FreeHGlobal(memory);
            if (processor != IntPtr.Zero)
                Vst3Native.GetVtableMethod<Vst3Native.ReleaseDelegate>(processor, 2)(processor);
            if (component != IntPtr.Zero)
                Vst3Native.GetVtableMethod<Vst3Native.ReleaseDelegate>(component, 2)(component);
            module.Release();
        }
    }
}
