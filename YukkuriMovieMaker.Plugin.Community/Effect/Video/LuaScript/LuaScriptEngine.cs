using System.Collections.Concurrent;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Debugging;
using MoonSharp.Interpreter.Loaders;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.LuaScript
{
    internal sealed class LuaScriptEngine : IDisposable
    {
        private readonly record struct ExecutionResult(
            LuaScriptException? Exception,
            bool TimedOut);

        private sealed record ExecutionJob(
            string Code,
            AviUtlScriptContext Context,
            CancellationToken Cancellation,
            TaskCompletionSource<ExecutionResult> Completion);

        private sealed class ExecutionThread : IDisposable
        {
            private sealed class CancellationDebugger : IDebugger
            {
                private static readonly DebuggerAction s_runAction = new() { Action = DebuggerAction.ActionType.Run };

                private CancellationToken _token;

                internal void UpdateToken(CancellationToken token) => _token = token;

                public DebuggerCaps GetDebuggerCaps() => 0;
                public void SetDebugService(DebugService debugService) { }
                public void SetSourceCode(SourceCode sourceCode) { }
                public void SetByteCode(string[] byteCode) { }
                public bool SignalRuntimeException(ScriptRuntimeException ex) => false;
                public void SignalExecutionEnded() { }
                public void Update(WatchType watchType, IEnumerable<WatchItem> items) { }
                public List<DynamicExpression> GetWatchItems() => [];
                public void RefreshBreakpoints(IEnumerable<SourceRef> refs) { }

                public bool IsPauseRequested() => _token.IsCancellationRequested;

                public DebuggerAction GetAction(int ip, SourceRef sourceref)
                {
                    _token.ThrowIfCancellationRequested();
                    return s_runAction;
                }
            }

            private readonly BlockingCollection<ExecutionJob> _queue = new(boundedCapacity: 1);
            private readonly Thread _thread;
            private readonly CancellationDebugger _debugger = new();

            private Script? _script;
            private DynValue? _compiledChunk;
            private string _lastCompiledCode = string.Empty;
            private Table? _objTable;
            private Table? _sceneTable;
            private CancellationToken _activeCancellation;
            private AviUtlScriptContext? _activeContext;

            internal ExecutionThread()
            {
                _thread = new Thread(WorkerLoop)
                {
                    IsBackground = true,
                    Name = "LuaScriptWorker"
                };
                _thread.Start();
            }

            internal ExecutionResult TryExecute(string code, AviUtlScriptContext ctx, int timeoutMs)
            {
                using var cts = new CancellationTokenSource();
                var tcs = new TaskCompletionSource<ExecutionResult>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

                if (!_queue.TryAdd(new ExecutionJob(code, ctx, cts.Token, tcs)))
                    return new ExecutionResult(
                        new LuaScriptRuntimeException("Script execution queue is full."), false);

                if (tcs.Task.Wait(timeoutMs))
                    return tcs.Task.Result;

                cts.Cancel();
                return new ExecutionResult(null, TimedOut: true);
            }

            private void WorkerLoop()
            {
                try
                {
                    foreach (var job in _queue.GetConsumingEnumerable())
                        ProcessJob(job);
                }
                catch (OperationCanceledException) { }
                catch (InvalidOperationException) { }
            }

            private void ProcessJob(ExecutionJob job)
            {
                if (job.Cancellation.IsCancellationRequested)
                {
                    job.Completion.TrySetResult(new ExecutionResult(null, TimedOut: true));
                    return;
                }

                _activeCancellation = job.Cancellation;
                _activeContext = job.Context;
                _debugger.UpdateToken(job.Cancellation);

                try
                {
                    EnsureScript();
                    EnsureCompiled(job.Code);
                    SetupGlobals(job.Context);
                    _script!.Call(_compiledChunk!);
                    ReadBackGlobals(job.Context);
                    job.Completion.TrySetResult(new ExecutionResult(null, false));
                }
                catch (OperationCanceledException) when (job.Cancellation.IsCancellationRequested)
                {
                    job.Completion.TrySetResult(new ExecutionResult(null, TimedOut: true));
                }
                catch (LuaScriptException ex)
                {
                    job.Completion.TrySetResult(new ExecutionResult(ex, false));
                }
                catch (ScriptRuntimeException ex)
                {
                    job.Completion.TrySetResult(new ExecutionResult(
                        new LuaScriptRuntimeException(ex.DecoratedMessage ?? ex.Message, ex), false));
                }
                catch (Exception ex)
                {
                    job.Completion.TrySetResult(new ExecutionResult(
                        new LuaScriptRuntimeException(ex.Message, ex), false));
                }
                finally
                {
                    _debugger.UpdateToken(default);
                    _activeContext = null;
                    _activeCancellation = default;
                }
            }

            private void EnsureScript()
            {
                if (_script is not null) return;
                var script = new Script(
                    CoreModules.Basic |
                    CoreModules.Math |
                    CoreModules.String |
                    CoreModules.Table |
                    CoreModules.Bit32 |
                    CoreModules.TableIterators |
                    CoreModules.Metatables |
                    CoreModules.ErrorHandling);
                script.Options.ScriptLoader = new DisabledFileScriptLoader();
                script.AttachDebugger(_debugger);
                _script = script;
            }

            private void EnsureCompiled(string code)
            {
                if (_compiledChunk is not null && code == _lastCompiledCode) return;

                DynValue chunk;
                try
                {
                    chunk = _script!.LoadString(code, null, "LuaScript");
                }
                catch (SyntaxErrorException ex)
                {
                    throw new LuaScriptCompilationException(ex.DecoratedMessage ?? ex.Message, ex);
                }

                _compiledChunk = chunk;
                _lastCompiledCode = code;
            }

            private void SetupGlobals(AviUtlScriptContext ctx)
            {
                var script = _script!;

                if (_objTable is null)
                {
                    _sceneTable = new Table(script);
                    script.Globals["scene"] = _sceneTable;

                    _objTable = new Table(script);
                    RegisterPixelCallbacks(_objTable);
                    script.Globals["obj"] = _objTable;
                }

                script.Globals["time"] = ctx.Time;
                script.Globals["frame"] = ctx.Frame;
                script.Globals["totalframe"] = ctx.TotalFrame;
                script.Globals["framerate"] = ctx.Framerate;
                script.Globals["timelineframe"] = ctx.TimelineFrame;
                script.Globals["timelinetime"] = ctx.TimelineTime;
                script.Globals["layer"] = ctx.Layer;

                _sceneTable!["width"] = ctx.SceneWidth;
                _sceneTable!["height"] = ctx.SceneHeight;

                _objTable!["w"] = ctx.ImageWidth;
                _objTable!["h"] = ctx.ImageHeight;
                _objTable!["cx"] = ctx.ImageWidth / 2d;
                _objTable!["cy"] = ctx.ImageHeight / 2d;
                _objTable!["x"] = ctx.X;
                _objTable!["y"] = ctx.Y;
                _objTable!["z"] = ctx.Z;
                _objTable!["ox"] = ctx.Ox;
                _objTable!["oy"] = ctx.Oy;
                _objTable!["zoom"] = ctx.Zoom;
                _objTable!["aspect"] = ctx.Aspect;
                _objTable!["alpha"] = ctx.Alpha;
                _objTable!["rx"] = ctx.Rx;
                _objTable!["ry"] = ctx.Ry;
                _objTable!["rz"] = ctx.Rz;
                _objTable!["track0"] = ctx.Track0;
                _objTable!["track1"] = ctx.Track1;
                _objTable!["track2"] = ctx.Track2;
                _objTable!["track3"] = ctx.Track3;
            }

            private void RegisterPixelCallbacks(Table obj)
            {
                obj["getpixel"] = DynValue.NewCallback((_, args) =>
                {
                    _activeCancellation.ThrowIfCancellationRequested();
                    if (_activeContext is null) return DynValue.Nil;
                    int x = (int)(args[0].CastToNumber() ?? 0d);
                    int y = (int)(args[1].CastToNumber() ?? 0d);
                    var (r, g, b, a) = _activeContext.GetPixel(x, y);
                    return DynValue.NewTuple(
                        DynValue.NewNumber(r),
                        DynValue.NewNumber(g),
                        DynValue.NewNumber(b),
                        DynValue.NewNumber(a));
                });

                obj["setpixel"] = DynValue.NewCallback((_, args) =>
                {
                    _activeCancellation.ThrowIfCancellationRequested();
                    if (_activeContext is null) return DynValue.Void;
                    int x = (int)(args[0].CastToNumber() ?? 0d);
                    int y = (int)(args[1].CastToNumber() ?? 0d);
                    double r = args[2].CastToNumber() ?? 0d;
                    double g = args[3].CastToNumber() ?? 0d;
                    double b = args[4].CastToNumber() ?? 0d;
                    double a = args.Count > 5 ? args[5].CastToNumber() ?? 255d : 255d;
                    _activeContext.SetPixel(x, y, r, g, b, a);
                    return DynValue.Void;
                });

                obj["getpixeldata"] = DynValue.NewCallback((_, _) =>
                {
                    _activeCancellation.ThrowIfCancellationRequested();
                    if (_activeContext is null) return DynValue.Nil;
                    _activeContext.EnsurePixelBuffer();
                    return UserData.Create(new PixelDataProxy(_activeContext));
                });

                obj["putpixeldata"] = DynValue.NewCallback((_, _) => DynValue.Void);
            }

            private void ReadBackGlobals(AviUtlScriptContext ctx)
            {
                if (_objTable is null) return;

                ctx.X = _objTable.Get("x").CastToNumber() ?? ctx.X;
                ctx.Y = _objTable.Get("y").CastToNumber() ?? ctx.Y;
                ctx.Z = _objTable.Get("z").CastToNumber() ?? ctx.Z;
                ctx.Ox = _objTable.Get("ox").CastToNumber() ?? ctx.Ox;
                ctx.Oy = _objTable.Get("oy").CastToNumber() ?? ctx.Oy;
                ctx.Zoom = _objTable.Get("zoom").CastToNumber() ?? ctx.Zoom;
                ctx.Aspect = _objTable.Get("aspect").CastToNumber() ?? ctx.Aspect;
                ctx.Alpha = _objTable.Get("alpha").CastToNumber() ?? ctx.Alpha;
                ctx.Rx = _objTable.Get("rx").CastToNumber() ?? ctx.Rx;
                ctx.Ry = _objTable.Get("ry").CastToNumber() ?? ctx.Ry;
                ctx.Rz = _objTable.Get("rz").CastToNumber() ?? ctx.Rz;
            }

            public void Dispose()
            {
                _queue.CompleteAdding();
                _thread.Join();
                _queue.Dispose();
            }

            internal void AbandonAsync()
            {
                _queue.CompleteAdding();
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    _thread.Join();
                    _queue.Dispose();
                });
            }

            private sealed class DisabledFileScriptLoader : ScriptLoaderBase
            {
                public override object LoadFile(string file, Table globalContext)
                    => throw new NotSupportedException(
                        "File access is not allowed in Lua script effects.");

                public override bool ScriptFileExists(string name) => false;
            }
        }

        private const int ExecutionTimeoutMilliseconds = 5000;

        static LuaScriptEngine()
        {
            UserData.RegisterType<PixelDataProxy>();
        }

        private ExecutionThread _executionThread = new();
        private bool _disposed;

        public void Execute(string code, AviUtlScriptContext ctx)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var result = _executionThread.TryExecute(code, ctx, ExecutionTimeoutMilliseconds);

            if (result.TimedOut)
            {
                var stale = _executionThread;
                _executionThread = new ExecutionThread();
                stale.AbandonAsync();
                throw new LuaScriptRuntimeException(
                    $"Script execution timed out after {ExecutionTimeoutMilliseconds} ms.");
            }

            if (result.Exception is not null)
                throw result.Exception;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _executionThread.Dispose();
        }
    }
}
