using System.Runtime.InteropServices;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    internal sealed class Vst3EditorSession : IDisposable
    {
        readonly Vst3Module module;
        readonly IntPtr component;
        readonly IntPtr processor;
        readonly IntPtr controller;
        readonly bool isSingleComponent;
        const int FlushBlockFrames = 32;

        readonly ComponentHandler handler;
        readonly PlugFrame frame;
        readonly int inputBusCount;
        readonly int outputBusCount;
        Vst3ProcessBuffers? buffers;
        IntPtr view;
        bool isViewAttached;
        bool isDisposed;

        public event Action<int, int>? ViewResizeRequested;
        public event Action<uint, double>? ParameterPerformed;
        public event Action? EditCompleted;

        public Vst3EditorSession(string path, byte[]? componentState, byte[]? controllerState)
        {
            handler = new ComponentHandler(OnParameterEdited, OnEditCompleted);
            frame = new PlugFrame(OnViewResizeRequested);
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

                controller = CreateController(path, out isSingleComponent);
                ConnectComponents();
                RestoreStates(componentState, controllerState);

                var setComponentHandler = Vst3Native.GetVtableMethod<Vst3Native.SetComponentHandlerDelegate>(controller, 16);
                setComponentHandler(controller, handler.Handle);

                var getBusCount = Vst3Native.GetVtableMethod<Vst3Native.GetBusCountDelegate>(component, 7);
                inputBusCount = getBusCount(component, Vst3Native.MediaTypeAudio, Vst3Native.BusDirectionInput);
                outputBusCount = getBusCount(component, Vst3Native.MediaTypeAudio, Vst3Native.BusDirectionOutput);
                buffers = new Vst3ProcessBuffers(processor, inputBusCount, outputBusCount, FlushBlockFrames);

                var setup = new Vst3Native.ProcessSetup
                {
                    ProcessMode = Vst3Native.ProcessModeRealtime,
                    SymbolicSampleSize = Vst3Native.SymbolicSampleSize32,
                    MaxSamplesPerBlock = FlushBlockFrames,
                    SampleRate = 48000,
                };
                var setupProcessing = Vst3Native.GetVtableMethod<Vst3Native.SetupProcessingDelegate>(processor, 7);
                setupProcessing(processor, ref setup);

                var activateBus = Vst3Native.GetVtableMethod<Vst3Native.ActivateBusDelegate>(component, 10);
                if (inputBusCount > 0)
                    activateBus(component, Vst3Native.MediaTypeAudio, Vst3Native.BusDirectionInput, 0, 1);
                if (outputBusCount > 0)
                    activateBus(component, Vst3Native.MediaTypeAudio, Vst3Native.BusDirectionOutput, 0, 1);

                Vst3Native.GetVtableMethod<Vst3Native.SetActiveDelegate>(component, 11)(component, 1);
                Vst3Native.GetVtableMethod<Vst3Native.SetProcessingDelegate>(processor, 8)(processor, 1);
            }
            catch
            {
                Dispose();
                throw;
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

        public void Dispose()
        {
            if (isDisposed)
                return;
            isDisposed = true;

            ReleaseView();

            if (component != IntPtr.Zero && processor != IntPtr.Zero && controller != IntPtr.Zero)
            {
                Vst3Native.GetVtableMethod<Vst3Native.SetProcessingDelegate>(processor, 8)(processor, 0);
                Vst3Native.GetVtableMethod<Vst3Native.SetActiveDelegate>(component, 11)(component, 0);
            }
            DisconnectComponents();
            if (controller != IntPtr.Zero)
            {
                if (!isSingleComponent)
                    Vst3Native.GetVtableMethod<Vst3Native.TerminateDelegate>(controller, 4)(controller);
                Vst3Native.GetVtableMethod<Vst3Native.ReleaseDelegate>(controller, 2)(controller);
            }
            if (component != IntPtr.Zero)
            {
                Vst3Native.GetVtableMethod<Vst3Native.TerminateDelegate>(component, 4)(component);
                Vst3Native.GetVtableMethod<Vst3Native.ReleaseDelegate>(component, 2)(component);
            }
            if (processor != IntPtr.Zero)
                Vst3Native.GetVtableMethod<Vst3Native.ReleaseDelegate>(processor, 2)(processor);

            buffers?.Dispose();
            buffers = null;
            handler.Dispose();
            frame.Dispose();
            module.Release();
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
            if (queryInterface(component, Vst3Native.IEditControllerUid, out var controller) == Vst3Native.ResultOk
                && controller != IntPtr.Zero)
            {
                isSingleComponent = true;
                return controller;
            }
            throw new InvalidOperationException($"IEditController is not available: {path}");
        }

        void ConnectComponents()
        {
            if (isSingleComponent)
                return;
            var (componentPoint, controllerPoint) = GetConnectionPoints();
            if (componentPoint == IntPtr.Zero || controllerPoint == IntPtr.Zero)
                return;
            Vst3Native.GetVtableMethod<Vst3Native.ConnectDelegate>(componentPoint, 3)(componentPoint, controllerPoint);
            Vst3Native.GetVtableMethod<Vst3Native.ConnectDelegate>(controllerPoint, 3)(controllerPoint, componentPoint);
            ReleaseConnectionPoints(componentPoint, controllerPoint);
        }

        void DisconnectComponents()
        {
            if (isSingleComponent || component == IntPtr.Zero || controller == IntPtr.Zero)
                return;
            var (componentPoint, controllerPoint) = GetConnectionPoints();
            if (componentPoint != IntPtr.Zero && controllerPoint != IntPtr.Zero)
            {
                Vst3Native.GetVtableMethod<Vst3Native.ConnectDelegate>(componentPoint, 4)(componentPoint, controllerPoint);
                Vst3Native.GetVtableMethod<Vst3Native.ConnectDelegate>(controllerPoint, 4)(controllerPoint, componentPoint);
            }
            ReleaseConnectionPoints(componentPoint, controllerPoint);
        }

        (IntPtr ComponentPoint, IntPtr ControllerPoint) GetConnectionPoints()
        {
            var queryComponent = Vst3Native.GetVtableMethod<Vst3Native.QueryInterfaceDelegate>(component, 0);
            var queryController = Vst3Native.GetVtableMethod<Vst3Native.QueryInterfaceDelegate>(controller, 0);
            queryComponent(component, Vst3Native.IConnectionPointUid, out var componentPoint);
            queryController(controller, Vst3Native.IConnectionPointUid, out var controllerPoint);
            return (componentPoint, controllerPoint);
        }

        static void ReleaseConnectionPoints(IntPtr componentPoint, IntPtr controllerPoint)
        {
            if (componentPoint != IntPtr.Zero)
                Vst3Native.GetVtableMethod<Vst3Native.ReleaseDelegate>(componentPoint, 2)(componentPoint);
            if (controllerPoint != IntPtr.Zero)
                Vst3Native.GetVtableMethod<Vst3Native.ReleaseDelegate>(controllerPoint, 2)(controllerPoint);
        }

        void RestoreStates(byte[]? componentState, byte[]? controllerState)
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

        void OnParameterEdited(uint parameterId, double normalizedValue)
        {
            FlushParameterEdit(parameterId, normalizedValue);
            ParameterPerformed?.Invoke(parameterId, normalizedValue);
        }

        void OnEditCompleted()
        {
            EditCompleted?.Invoke();
        }

        void FlushParameterEdit(uint parameterId, double normalizedValue)
        {
            if (isDisposed || buffers is null)
                return;
            using var changes = new Vst3ParameterChanges([new(parameterId, normalizedValue)]);
            var data = new Vst3Native.ProcessData
            {
                ProcessMode = Vst3Native.ProcessModeRealtime,
                SymbolicSampleSize = Vst3Native.SymbolicSampleSize32,
                NumSamples = 0,
                NumInputs = inputBusCount,
                NumOutputs = outputBusCount,
                Inputs = buffers.Inputs,
                Outputs = buffers.Outputs,
                InputParameterChanges = changes.Handle,
            };
            var process = Vst3Native.GetVtableMethod<Vst3Native.ProcessDelegate>(processor, 9);
            process(processor, ref data);
        }

        void OnViewResizeRequested(int width, int height)
        {
            ViewResizeRequested?.Invoke(width, height);
        }

        sealed class ComponentHandler : Vst3HostObject
        {
            readonly Action<uint, double> performEditCallback;
            readonly Action editCompletedCallback;

            public ComponentHandler(Action<uint, double> performEditCallback, Action editCompletedCallback) : base(Vst3Native.IComponentHandlerUid)
            {
                this.performEditCallback = performEditCallback;
                this.editCompletedCallback = editCompletedCallback;
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
                editCompletedCallback();
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
