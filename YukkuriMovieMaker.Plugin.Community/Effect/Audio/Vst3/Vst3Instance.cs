using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    internal sealed unsafe class Vst3Instance : IDisposable
    {
        public const int BlockFrames = 512;

        readonly object processGate = new();
        readonly object pendingEditsGate = new();
        readonly Dictionary<uint, double> pendingEdits = [];
        readonly object outputForwardGate = new();
        readonly Dictionary<uint, double> pendingOutputParameters = [];
        bool isOutputForwardScheduled;
        int editCompletedPending;
        int rebuildPending;

        Vst3Module module = null!;
        IntPtr component;
        IntPtr processor;
        IntPtr controller;
        bool isSingleComponent;
        ComponentHandler handler = null!;
        PlugFrame frame = null!;
        Vst3ConnectionProxy? componentSideProxy;
        Vst3ConnectionProxy? controllerSideProxy;
        IntPtr componentPoint;
        IntPtr controllerPoint;
        Vst3ProcessBuffers? buffers;
        Vst3OutputParameterChanges? outputChanges;
        IntPtr contextMemory;
        Vst3Native.ProcessData processData;
        Vst3Native.SetProcessingDelegate setProcessing = null!;
        Vst3Native.ProcessDelegate process = null!;
        int[] inputChannelCounts = [];
        int[] outputChannelCounts = [];
        double sampleRate;
        IntPtr view;
        bool isViewAttached;
        volatile bool isDisposed;
        bool isInProcessCall;
        bool isComponentInitialized;
        bool isComponentActivated;

        public int LatencySamples { get; private set; }

        public event Action<int, int>? ViewResizeRequested;
        public event Action<uint, double>? ParameterPerformed;
        public event Action? EditCompleted;

        public Vst3Instance(string path, int sampleRate, byte[]? componentState, byte[]? controllerState)
        {
            Vst3HostThread.Invoke(() =>
            {
                try
                {
                    Initialize(path, sampleRate, componentState, controllerState);
                }
                catch
                {
                    DisposeCore();
                    throw;
                }
            });
        }

        void Initialize(string path, int initialSampleRate, byte[]? componentState, byte[]? controllerState)
        {
            sampleRate = initialSampleRate;
            handler = new ComponentHandler(OnParameterEdited, OnEditCompleted, OnRestartComponent);
            frame = new PlugFrame(OnViewResizeRequested);
            module = Vst3Module.Acquire(path);

            component = module.CreateAudioComponent();

            var queryInterface = Vst3Native.GetVtableMethod<Vst3Native.QueryInterfaceDelegate>(component, 0);
            if (queryInterface(component, Vst3Native.IAudioProcessorUid, out processor) != Vst3Native.ResultOk
                || processor == IntPtr.Zero)
                throw new InvalidOperationException($"IAudioProcessor is not supported: {path}");

            var initialize = Vst3Native.GetVtableMethod<Vst3Native.InitializeDelegate>(component, 3);
            if (initialize(component, Vst3HostContext.Instance) != Vst3Native.ResultOk)
                throw new InvalidOperationException($"IComponent::initialize failed: {path}");
            isComponentInitialized = true;

            controller = CreateController(path, out isSingleComponent);
            ConnectComponents();

            var setComponentHandler = Vst3Native.GetVtableMethod<Vst3Native.SetComponentHandlerDelegate>(controller, 16);
            setComponentHandler(controller, handler.Handle);

            RestoreStatesCore(componentState, controllerState);

            var canProcessSampleSize = Vst3Native.GetVtableMethod<Vst3Native.CanProcessSampleSizeDelegate>(processor, 5);
            if (canProcessSampleSize(processor, Vst3Native.SymbolicSampleSize32) != Vst3Native.ResultOk)
                throw new InvalidOperationException($"32bit processing is not supported: {path}");

            var getBusCount = Vst3Native.GetVtableMethod<Vst3Native.GetBusCountDelegate>(component, 7);
            var inputBusCount = getBusCount(component, Vst3Native.MediaTypeAudio, Vst3Native.BusDirectionInput);
            var outputBusCount = getBusCount(component, Vst3Native.MediaTypeAudio, Vst3Native.BusDirectionOutput);
            if (inputBusCount < 1 || outputBusCount < 1)
                throw new InvalidOperationException($"Audio input and output busses are required: {path}");

            buffers = new Vst3ProcessBuffers(processor, inputBusCount, outputBusCount, BlockFrames);
            inputChannelCounts = buffers.InputChannelCounts;
            outputChannelCounts = buffers.OutputChannelCounts;
            if (inputChannelCounts[0] < 1 || outputChannelCounts[0] < 1)
                throw new InvalidOperationException($"Main busses have no channels: {path}");

            var setup = new Vst3Native.ProcessSetup
            {
                ProcessMode = Vst3Native.ProcessModeRealtime,
                SymbolicSampleSize = Vst3Native.SymbolicSampleSize32,
                MaxSamplesPerBlock = BlockFrames,
                SampleRate = sampleRate,
            };
            var setupProcessing = Vst3Native.GetVtableMethod<Vst3Native.SetupProcessingDelegate>(processor, 7);
            if (setupProcessing(processor, ref setup) != Vst3Native.ResultOk)
                throw new InvalidOperationException($"IAudioProcessor::setupProcessing failed: {path}");

            var activateBus = Vst3Native.GetVtableMethod<Vst3Native.ActivateBusDelegate>(component, 10);
            activateBus(component, Vst3Native.MediaTypeAudio, Vst3Native.BusDirectionInput, 0, 1);
            activateBus(component, Vst3Native.MediaTypeAudio, Vst3Native.BusDirectionOutput, 0, 1);

            var setActive = Vst3Native.GetVtableMethod<Vst3Native.SetActiveDelegate>(component, 11);
            if (setActive(component, 1) != Vst3Native.ResultOk)
                throw new InvalidOperationException($"IComponent::setActive failed: {path}");
            isComponentActivated = true;

            RefreshLatency();

            contextMemory = Marshal.AllocHGlobal(sizeof(Vst3Native.ProcessContext));
            NativeMemory.Clear((void*)contextMemory, (nuint)sizeof(Vst3Native.ProcessContext));
            outputChanges = new Vst3OutputParameterChanges();
            processData = new Vst3Native.ProcessData
            {
                ProcessMode = Vst3Native.ProcessModeRealtime,
                SymbolicSampleSize = Vst3Native.SymbolicSampleSize32,
                NumInputs = inputBusCount,
                NumOutputs = outputBusCount,
                Inputs = buffers.Inputs,
                Outputs = buffers.Outputs,
                OutputParameterChanges = outputChanges.Handle,
                ProcessContext = contextMemory,
            };

            setProcessing = Vst3Native.GetVtableMethod<Vst3Native.SetProcessingDelegate>(processor, 8);
            process = Vst3Native.GetVtableMethod<Vst3Native.ProcessDelegate>(processor, 9);
            setProcessing(processor, 1);
        }

        public void EnsureSampleRate(int hz)
        {
            if (isDisposed || sampleRate == hz)
                return;
            Vst3HostThread.Invoke(() =>
            {
                lock (processGate)
                {
                    if (isDisposed || sampleRate == hz)
                        return;
                    setProcessing(processor, 0);
                    Vst3Native.GetVtableMethod<Vst3Native.SetActiveDelegate>(component, 11)(component, 0);
                    isComponentActivated = false;
                    var setup = new Vst3Native.ProcessSetup
                    {
                        ProcessMode = Vst3Native.ProcessModeRealtime,
                        SymbolicSampleSize = Vst3Native.SymbolicSampleSize32,
                        MaxSamplesPerBlock = BlockFrames,
                        SampleRate = hz,
                    };
                    var setupProcessing = Vst3Native.GetVtableMethod<Vst3Native.SetupProcessingDelegate>(processor, 7);
                    var succeeded = setupProcessing(processor, ref setup) == Vst3Native.ResultOk;
                    if (!succeeded)
                    {
                        var fallback = setup with { SampleRate = sampleRate };
                        setupProcessing(processor, ref fallback);
                    }
                    Vst3Native.GetVtableMethod<Vst3Native.SetActiveDelegate>(component, 11)(component, 1);
                    isComponentActivated = true;
                    setProcessing(processor, 1);
                    if (succeeded)
                        sampleRate = hz;
                    RefreshLatency();
                    if (!succeeded)
                        throw new InvalidOperationException($"IAudioProcessor::setupProcessing failed: {hz}Hz");
                }
            });
        }

        public void RefreshLatency()
        {
            if (isDisposed)
                return;
            lock (processGate)
            {
                if (isDisposed)
                    return;
                var getLatencySamples = Vst3Native.GetVtableMethod<Vst3Native.GetLatencySamplesDelegate>(processor, 6);
                LatencySamples = (int)Math.Min(getLatencySamples(processor), (uint)(sampleRate * 4));
            }
        }

        public void QueueParameterChange(uint parameterId, double normalizedValue)
        {
            lock (pendingEditsGate)
                pendingEdits[parameterId] = normalizedValue;
            if (Vst3HostThread.CheckAccess())
                FlushPendingEdits();
        }

        void FlushPendingEdits()
        {
            if (!Monitor.TryEnter(processGate))
                return;
            try
            {
                FlushPendingEditsCore();
            }
            finally
            {
                Monitor.Exit(processGate);
            }
        }

        void FlushPendingEditsCore()
        {
            if (isDisposed || isInProcessCall)
                return;
            using var changes = DrainPendingEdits();
            if (changes is null)
                return;
            var data = processData;
            data.NumSamples = 0;
            data.InputParameterChanges = changes.Handle;
            isInProcessCall = true;
            try
            {
                process(processor, ref data);
            }
            finally
            {
                isInProcessCall = false;
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

        public void Process(float[] buffer, int offset, int frames, long framePosition, in Vst3Transport transport)
        {
            if (isDisposed)
                return;

            List<KeyValuePair<uint, double>>? performed = null;
            lock (processGate)
            {
                if (isDisposed)
                    return;
                using var changes = DrainPendingEdits();
                processData.InputParameterChanges = changes?.Handle ?? IntPtr.Zero;
                isInProcessCall = true;
                try
                {
                    while (frames > 0)
                    {
                        var blockFrames = Math.Min(frames, BlockFrames);
                        WriteProcessContext(framePosition, transport);
                        ProcessBlock(buffer, offset, blockFrames);
                        outputChanges!.Drain((id, value) => (performed ??= []).Add(new(id, value)));
                        processData.InputParameterChanges = IntPtr.Zero;
                        offset += blockFrames * 2;
                        frames -= blockFrames;
                        framePosition += blockFrames;
                    }
                }
                finally
                {
                    isInProcessCall = false;
                    processData.InputParameterChanges = IntPtr.Zero;
                }
            }

            if (performed is not null && isViewAttached)
            {
                var schedule = false;
                lock (outputForwardGate)
                {
                    foreach (var (id, value) in performed)
                        pendingOutputParameters[id] = value;
                    if (!isOutputForwardScheduled)
                    {
                        isOutputForwardScheduled = true;
                        schedule = true;
                    }
                }
                if (schedule)
                    Vst3HostThread.Post(ForwardOutputParameters, DispatcherPriority.Background);
            }
        }

        void ForwardOutputParameters()
        {
            KeyValuePair<uint, double>[] values;
            lock (outputForwardGate)
            {
                values = [.. pendingOutputParameters];
                pendingOutputParameters.Clear();
                isOutputForwardScheduled = false;
            }
            if (isDisposed || controller == IntPtr.Zero || values.Length == 0)
                return;
            var setParamNormalized = Vst3Native.GetVtableMethod<Vst3Native.SetParamNormalizedDelegate>(controller, 15);
            foreach (var (id, value) in values)
                setParamNormalized(controller, id, value);
        }

        void WriteProcessContext(long framePosition, in Vst3Transport transport)
        {
            var state = Vst3Native.ProcessContextPlaying | Vst3Native.ProcessContextContTimeValid;
            var context = new Vst3Native.ProcessContext
            {
                SampleRate = sampleRate,
                ProjectTimeSamples = framePosition,
                ContinousTimeSamples = framePosition,
            };
            if (transport.IsTempoValid)
            {
                var quarterNotes = framePosition / sampleRate * transport.Tempo / 60.0;
                var barLength = transport.TimeSignatureNumerator * 4.0 / transport.TimeSignatureDenominator;
                state |= Vst3Native.ProcessContextTempoValid
                    | Vst3Native.ProcessContextTimeSigValid
                    | Vst3Native.ProcessContextProjectTimeMusicValid
                    | Vst3Native.ProcessContextBarPositionValid;
                context.Tempo = transport.Tempo;
                context.TimeSigNumerator = transport.TimeSignatureNumerator;
                context.TimeSigDenominator = transport.TimeSignatureDenominator;
                context.ProjectTimeMusic = quarterNotes;
                context.BarPositionMusic = Math.Floor(quarterNotes / barLength) * barLength;
            }
            context.State = state;
            *(Vst3Native.ProcessContext*)contextMemory = context;
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

        public void Reset()
        {
            if (isDisposed)
                return;
            lock (processGate)
            {
                if (isDisposed)
                    return;
                setProcessing(processor, 0);
                setProcessing(processor, 1);
            }
        }

        public bool HasView => view != IntPtr.Zero;

        public bool TryCreateView()
        {
            if (isDisposed)
                return false;
            if (view != IntPtr.Zero)
                return true;
            var createView = Vst3Native.GetVtableMethod<Vst3Native.CreateViewDelegate>(controller, 17);
            view = createView(controller, Vst3Native.EditorViewType);
            if (view == IntPtr.Zero)
                return false;
            var isPlatformTypeSupported = Vst3Native.GetVtableMethod<Vst3Native.IsPlatformTypeSupportedDelegate>(view, 3);
            if (isPlatformTypeSupported(view, Vst3Native.PlatformTypeHwnd) != Vst3Native.ResultOk)
            {
                Vst3Native.GetVtableMethod<Vst3Native.ReleaseDelegate>(view, 2)(view);
                view = IntPtr.Zero;
                return false;
            }
            return true;
        }

        public bool CanResizeView =>
            HasView && Vst3Native.GetVtableMethod<Vst3Native.CanResizeDelegate>(view, 13)(view) == Vst3Native.ResultOk;

        public (int Width, int Height) GetViewSize()
        {
            var rect = new Vst3Native.ViewRect();
            var getSize = Vst3Native.GetVtableMethod<Vst3Native.GetSizeDelegate>(view, 9);
            if (getSize(view, ref rect) != Vst3Native.ResultOk)
                return (400, 300);
            return (Math.Max(1, rect.Right - rect.Left), Math.Max(1, rect.Bottom - rect.Top));
        }

        public (int Width, int Height) CheckSizeConstraint(int width, int height)
        {
            if (!HasView)
                return (width, height);
            var rect = new Vst3Native.ViewRect { Left = 0, Top = 0, Right = width, Bottom = height };
            var checkSizeConstraint = Vst3Native.GetVtableMethod<Vst3Native.CheckSizeConstraintDelegate>(view, 14);
            if (checkSizeConstraint(view, ref rect) != Vst3Native.ResultOk)
                return (width, height);
            return (Math.Max(1, rect.Right - rect.Left), Math.Max(1, rect.Bottom - rect.Top));
        }

        public void AttachView(IntPtr hwnd)
        {
            var setFrame = Vst3Native.GetVtableMethod<Vst3Native.SetFrameDelegate>(view, 12);
            setFrame(view, frame.Handle);
            var attached = Vst3Native.GetVtableMethod<Vst3Native.AttachedDelegate>(view, 4);
            if (attached(view, hwnd, Vst3Native.PlatformTypeHwnd) != Vst3Native.ResultOk)
                throw new InvalidOperationException("IPlugView::attached failed.");
            isViewAttached = true;
        }

        public void ResizeView(int width, int height)
        {
            if (!isViewAttached)
                return;
            var rect = new Vst3Native.ViewRect { Left = 0, Top = 0, Right = width, Bottom = height };
            var onSize = Vst3Native.GetVtableMethod<Vst3Native.OnSizeDelegate>(view, 10);
            onSize(view, ref rect);
        }

        public void DetachView()
        {
            if (!isViewAttached)
                return;
            isViewAttached = false;
            Vst3Native.GetVtableMethod<Vst3Native.RemovedDelegate>(view, 5)(view);
        }

        public void ReleaseView()
        {
            DetachView();
            if (view == IntPtr.Zero)
                return;
            Vst3Native.GetVtableMethod<Vst3Native.ReleaseDelegate>(view, 2)(view);
            view = IntPtr.Zero;
        }

        public (byte[]? ComponentState, byte[]? ControllerState) CaptureStates()
        {
            lock (processGate)
            {
                FlushPendingEditsCore();
                var componentGetState = Vst3Native.GetVtableMethod<Vst3Native.StreamDelegate>(component, 13);
                using var componentStream = new Vst3BStream();
                var componentState = componentGetState(component, componentStream.Handle) == Vst3Native.ResultOk
                    ? componentStream.ToArray()
                    : null;

                var controllerGetState = Vst3Native.GetVtableMethod<Vst3Native.StreamDelegate>(controller, 7);
                using var controllerStream = new Vst3BStream();
                var controllerState = controllerGetState(controller, controllerStream.Handle) == Vst3Native.ResultOk
                    ? controllerStream.ToArray()
                    : null;

                return (componentState, controllerState);
            }
        }

        public void RestoreStates(byte[]? componentState, byte[]? controllerState)
        {
            if (isDisposed)
                return;
            Vst3HostThread.Invoke(() =>
            {
                lock (processGate)
                {
                    if (isDisposed)
                        return;
                    RestoreStatesCore(componentState, controllerState);
                    RefreshLatency();
                }
            });
        }

        void RestoreStatesCore(byte[]? componentState, byte[]? controllerState)
        {
            if (componentState is not null)
            {
                var setState = Vst3Native.GetVtableMethod<Vst3Native.StreamDelegate>(component, 12);
                using var stream = new Vst3BStream(componentState);
                setState(component, stream.Handle);
            }

            var getState = Vst3Native.GetVtableMethod<Vst3Native.StreamDelegate>(component, 13);
            using var currentStream = new Vst3BStream();
            if (getState(component, currentStream.Handle) == Vst3Native.ResultOk)
            {
                var setComponentState = Vst3Native.GetVtableMethod<Vst3Native.StreamDelegate>(controller, 5);
                using var syncStream = new Vst3BStream(currentStream.ToArray());
                setComponentState(controller, syncStream.Handle);
            }

            if (controllerState is not null)
            {
                var setState = Vst3Native.GetVtableMethod<Vst3Native.StreamDelegate>(controller, 6);
                using var stream = new Vst3BStream(controllerState);
                setState(controller, stream.Handle);
            }
        }

        public void Dispose()
        {
            if (isDisposed)
                return;
            isDisposed = true;
            try
            {
                Vst3HostThread.Invoke(DisposeCore);
            }
            catch (TimeoutException)
            {
            }
        }

        void DisposeCore()
        {
            isDisposed = true;
            ReleaseView();

            if (processor != IntPtr.Zero && setProcessing is not null)
                setProcessing(processor, 0);
            if (component != IntPtr.Zero && isComponentActivated)
            {
                Vst3Native.GetVtableMethod<Vst3Native.SetActiveDelegate>(component, 11)(component, 0);
                isComponentActivated = false;
            }

            DisconnectComponents();

            if (controller != IntPtr.Zero)
            {
                if (!isSingleComponent)
                    Vst3Native.GetVtableMethod<Vst3Native.TerminateDelegate>(controller, 4)(controller);
                Vst3Native.GetVtableMethod<Vst3Native.ReleaseDelegate>(controller, 2)(controller);
                controller = IntPtr.Zero;
            }
            if (component != IntPtr.Zero)
            {
                if (isComponentInitialized)
                    Vst3Native.GetVtableMethod<Vst3Native.TerminateDelegate>(component, 4)(component);
                Vst3Native.GetVtableMethod<Vst3Native.ReleaseDelegate>(component, 2)(component);
                component = IntPtr.Zero;
            }
            if (processor != IntPtr.Zero)
            {
                Vst3Native.GetVtableMethod<Vst3Native.ReleaseDelegate>(processor, 2)(processor);
                processor = IntPtr.Zero;
            }

            buffers?.Dispose();
            buffers = null;
            outputChanges?.Dispose();
            outputChanges = null;
            if (contextMemory != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(contextMemory);
                contextMemory = IntPtr.Zero;
            }
            handler?.Dispose();
            frame?.Dispose();
            module?.Release();
            module = null!;
        }

        IntPtr CreateController(string path, out bool isSingleComponent)
        {
            var classId = new byte[16];
            var classIdBuffer = Marshal.AllocHGlobal(16);
            try
            {
                var getControllerClassId = Vst3Native.GetVtableMethod<Vst3Native.GetControllerClassIdDelegate>(component, 5);
                if (getControllerClassId(component, classIdBuffer) == Vst3Native.ResultOk)
                {
                    Marshal.Copy(classIdBuffer, classId, 0, 16);
                    var instance = module.CreateInstance(classId, Vst3Native.IEditControllerUid);
                    if (instance != IntPtr.Zero)
                    {
                        var initialize = Vst3Native.GetVtableMethod<Vst3Native.InitializeDelegate>(instance, 3);
                        if (initialize(instance, Vst3HostContext.Instance) != Vst3Native.ResultOk)
                        {
                            Vst3Native.GetVtableMethod<Vst3Native.ReleaseDelegate>(instance, 2)(instance);
                            throw new InvalidOperationException($"IEditController::initialize failed: {path}");
                        }
                        isSingleComponent = false;
                        return instance;
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(classIdBuffer);
            }

            var queryInterface = Vst3Native.GetVtableMethod<Vst3Native.QueryInterfaceDelegate>(component, 0);
            if (queryInterface(component, Vst3Native.IEditControllerUid, out var sharedController) == Vst3Native.ResultOk
                && sharedController != IntPtr.Zero)
            {
                isSingleComponent = true;
                return sharedController;
            }
            throw new InvalidOperationException($"IEditController is not available: {path}");
        }

        void ConnectComponents()
        {
            if (isSingleComponent)
                return;
            var queryComponent = Vst3Native.GetVtableMethod<Vst3Native.QueryInterfaceDelegate>(component, 0);
            var queryController = Vst3Native.GetVtableMethod<Vst3Native.QueryInterfaceDelegate>(controller, 0);
            queryComponent(component, Vst3Native.IConnectionPointUid, out componentPoint);
            queryController(controller, Vst3Native.IConnectionPointUid, out controllerPoint);
            if (componentPoint == IntPtr.Zero || controllerPoint == IntPtr.Zero)
                return;
            componentSideProxy = new Vst3ConnectionProxy(controllerPoint);
            controllerSideProxy = new Vst3ConnectionProxy(componentPoint);
            Vst3Native.GetVtableMethod<Vst3Native.ConnectDelegate>(componentPoint, 3)(componentPoint, componentSideProxy.Handle);
            Vst3Native.GetVtableMethod<Vst3Native.ConnectDelegate>(controllerPoint, 3)(controllerPoint, controllerSideProxy.Handle);
        }

        void DisconnectComponents()
        {
            if (componentPoint != IntPtr.Zero && componentSideProxy is not null)
                Vst3Native.GetVtableMethod<Vst3Native.ConnectDelegate>(componentPoint, 4)(componentPoint, componentSideProxy.Handle);
            if (controllerPoint != IntPtr.Zero && controllerSideProxy is not null)
                Vst3Native.GetVtableMethod<Vst3Native.ConnectDelegate>(controllerPoint, 4)(controllerPoint, controllerSideProxy.Handle);
            componentSideProxy?.Invalidate();
            controllerSideProxy?.Invalidate();
            componentSideProxy?.ReleaseReference();
            controllerSideProxy?.ReleaseReference();
            componentSideProxy = null;
            controllerSideProxy = null;
            if (componentPoint != IntPtr.Zero)
            {
                Vst3Native.GetVtableMethod<Vst3Native.ReleaseDelegate>(componentPoint, 2)(componentPoint);
                componentPoint = IntPtr.Zero;
            }
            if (controllerPoint != IntPtr.Zero)
            {
                Vst3Native.GetVtableMethod<Vst3Native.ReleaseDelegate>(controllerPoint, 2)(controllerPoint);
                controllerPoint = IntPtr.Zero;
            }
        }

        void OnParameterEdited(uint parameterId, double normalizedValue)
        {
            QueueParameterChange(parameterId, normalizedValue);
            ParameterPerformed?.Invoke(parameterId, normalizedValue);
        }

        void OnRestartComponent(int flags)
        {
            if ((flags & (Vst3Native.RestartReloadComponent | Vst3Native.RestartIoChanged)) != 0)
            {
                if (Interlocked.Exchange(ref rebuildPending, 1) == 0)
                    Vst3HostThread.Post(() =>
                    {
                        Volatile.Write(ref rebuildPending, 0);
                        RebuildProcessingSetup();
                    }, DispatcherPriority.Background);
            }
            else if ((flags & Vst3Native.RestartLatencyChanged) != 0)
            {
                Vst3HostThread.Post(RefreshLatency, DispatcherPriority.Background);
            }
            OnEditCompleted();
        }

        void RebuildProcessingSetup()
        {
            if (isDisposed)
                return;
            lock (processGate)
            {
                if (isDisposed)
                    return;
                var getBusCount = Vst3Native.GetVtableMethod<Vst3Native.GetBusCountDelegate>(component, 7);
                var inputBusCount = getBusCount(component, Vst3Native.MediaTypeAudio, Vst3Native.BusDirectionInput);
                var outputBusCount = getBusCount(component, Vst3Native.MediaTypeAudio, Vst3Native.BusDirectionOutput);
                if (inputBusCount < 1 || outputBusCount < 1)
                    return;

                setProcessing(processor, 0);
                Vst3Native.GetVtableMethod<Vst3Native.SetActiveDelegate>(component, 11)(component, 0);
                isComponentActivated = false;

                var newBuffers = new Vst3ProcessBuffers(processor, inputBusCount, outputBusCount, BlockFrames);
                if (newBuffers.InputChannelCounts[0] >= 1 && newBuffers.OutputChannelCounts[0] >= 1)
                {
                    buffers!.Dispose();
                    buffers = newBuffers;
                    inputChannelCounts = newBuffers.InputChannelCounts;
                    outputChannelCounts = newBuffers.OutputChannelCounts;
                    processData.NumInputs = inputBusCount;
                    processData.NumOutputs = outputBusCount;
                    processData.Inputs = newBuffers.Inputs;
                    processData.Outputs = newBuffers.Outputs;
                }
                else
                {
                    newBuffers.Dispose();
                }

                var setup = new Vst3Native.ProcessSetup
                {
                    ProcessMode = Vst3Native.ProcessModeRealtime,
                    SymbolicSampleSize = Vst3Native.SymbolicSampleSize32,
                    MaxSamplesPerBlock = BlockFrames,
                    SampleRate = sampleRate,
                };
                var setupProcessing = Vst3Native.GetVtableMethod<Vst3Native.SetupProcessingDelegate>(processor, 7);
                setupProcessing(processor, ref setup);

                var activateBus = Vst3Native.GetVtableMethod<Vst3Native.ActivateBusDelegate>(component, 10);
                activateBus(component, Vst3Native.MediaTypeAudio, Vst3Native.BusDirectionInput, 0, 1);
                activateBus(component, Vst3Native.MediaTypeAudio, Vst3Native.BusDirectionOutput, 0, 1);

                Vst3Native.GetVtableMethod<Vst3Native.SetActiveDelegate>(component, 11)(component, 1);
                isComponentActivated = true;
                setProcessing(processor, 1);
                RefreshLatency();
            }
        }

        void OnEditCompleted()
        {
            if (Interlocked.Exchange(ref editCompletedPending, 1) == 1)
                return;
            Vst3HostThread.Post(() =>
            {
                Volatile.Write(ref editCompletedPending, 0);
                if (isDisposed)
                    return;
                EditCompleted?.Invoke();
            }, DispatcherPriority.Background);
        }

        void OnViewResizeRequested(int width, int height)
        {
            ViewResizeRequested?.Invoke(width, height);
        }

        sealed class ComponentHandler : Vst3HostObject
        {
            readonly Action<uint, double> performEditCallback;
            readonly Action editCompletedCallback;
            readonly Action<int> restartCallback;

            public ComponentHandler(Action<uint, double> performEditCallback, Action editCompletedCallback, Action<int> restartCallback) : base(Vst3Native.IComponentHandlerUid)
            {
                this.performEditCallback = performEditCallback;
                this.editCompletedCallback = editCompletedCallback;
                this.restartCallback = restartCallback;
                BuildVtable(
                    new EditDelegate(BeginEdit),
                    new PerformEditDelegate(PerformEdit),
                    new EditDelegate(EndEdit),
                    new RestartComponentDelegate(RestartComponent));
            }

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            delegate int EditDelegate(IntPtr self, uint id);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            delegate int PerformEditDelegate(IntPtr self, uint id, double valueNormalized);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            delegate int RestartComponentDelegate(IntPtr self, int flags);

            int BeginEdit(IntPtr self, uint id) => Vst3Native.ResultOk;

            int PerformEdit(IntPtr self, uint id, double valueNormalized)
            {
                performEditCallback(id, valueNormalized);
                return Vst3Native.ResultOk;
            }

            int EndEdit(IntPtr self, uint id)
            {
                editCompletedCallback();
                return Vst3Native.ResultOk;
            }

            int RestartComponent(IntPtr self, int flags)
            {
                restartCallback(flags);
                return Vst3Native.ResultOk;
            }
        }

        sealed class PlugFrame : Vst3HostObject
        {
            readonly Action<int, int> resizeCallback;

            public PlugFrame(Action<int, int> resizeCallback) : base(Vst3Native.IPlugFrameUid)
            {
                this.resizeCallback = resizeCallback;
                BuildVtable(new ResizeViewDelegate(ResizeView));
            }

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            delegate int ResizeViewDelegate(IntPtr self, IntPtr view, ref Vst3Native.ViewRect newSize);

            int ResizeView(IntPtr self, IntPtr view, ref Vst3Native.ViewRect newSize)
            {
                resizeCallback(Math.Max(1, newSize.Right - newSize.Left), Math.Max(1, newSize.Bottom - newSize.Top));
                return Vst3Native.ResultOk;
            }
        }
    }
}
