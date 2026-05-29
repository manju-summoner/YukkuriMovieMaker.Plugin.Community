using System.Collections.Concurrent;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Loaders;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.LuaScript
{
    internal sealed class LuaScriptEngine : IDisposable
    {
        private const int ExecutionTimeoutMilliseconds = 5000;

        static LuaScriptEngine()
        {
            UserData.RegisterType<PixelDataProxy>();
        }

        private LuaExecutionThread _executionThread = new();

        public void Execute(string code, AviUtlScriptContext ctx)
        {
            bool completed = _executionThread.TryExecute(code, ctx, ExecutionTimeoutMilliseconds, out var exception);

            if (!completed)
            {
                _executionThread.Dispose();
                _executionThread = new LuaExecutionThread();
                throw new LuaScriptRuntimeException($"Script execution timed out after {ExecutionTimeoutMilliseconds} ms.");
            }

            if (exception is not null)
                throw exception;
        }

        public void Dispose() => _executionThread.Dispose();
    }

    internal sealed class LuaExecutionThread : IDisposable
    {
        private sealed record ExecutionJob(
            string Code,
            AviUtlScriptContext Context,
            TaskCompletionSource<LuaScriptRuntimeException?> Result);

        private readonly BlockingCollection<ExecutionJob> _queue = new(boundedCapacity: 1);
        private readonly Thread _thread;

        private Script? _script;
        private DynValue? _compiledChunk;
        private string _lastCompiledCode = string.Empty;
        private Table? _objTable;
        private Table? _sceneTable;
        private AviUtlScriptContext? _activeContext;

        internal LuaExecutionThread()
        {
            _thread = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "LuaScriptWorker"
            };
            _thread.Start();
        }

        internal bool TryExecute(string code, AviUtlScriptContext ctx, int timeoutMs, out LuaScriptRuntimeException? exception)
        {
            var tcs = new TaskCompletionSource<LuaScriptRuntimeException?>(TaskCreationOptions.RunContinuationsAsynchronously);
            _queue.Add(new ExecutionJob(code, ctx, tcs));

            if (!tcs.Task.Wait(timeoutMs))
            {
                exception = null;
                return false;
            }

            exception = tcs.Task.Result;
            return true;
        }

        private void WorkerLoop()
        {
            try
            {
                foreach (var job in _queue.GetConsumingEnumerable())
                    ProcessJob(job);
            }
            catch (ThreadInterruptedException) { }
            catch (OperationCanceledException) { }
            catch (InvalidOperationException) { }
        }

        private void ProcessJob(ExecutionJob job)
        {
            try
            {
                _activeContext = job.Context;
                EnsureScript();
                EnsureCompiled(job.Code);
                SetupGlobals(job.Context);
                _script!.Call(_compiledChunk!);
                ReadBackGlobals(job.Context);
                job.Result.TrySetResult(null);
            }
            catch (LuaScriptCompilationException ex)
            {
                job.Result.TrySetException(ex);
            }
            catch (ScriptRuntimeException ex)
            {
                job.Result.TrySetResult(new LuaScriptRuntimeException(ex.DecoratedMessage ?? ex.Message, ex));
            }
            catch (Exception ex)
            {
                job.Result.TrySetResult(new LuaScriptRuntimeException(ex.Message, ex));
            }
            finally
            {
                _activeContext = null;
            }
        }

        private void EnsureScript()
        {
            if (_script is not null) return;
            _script = CreateScript();
            _compiledChunk = null;
            _lastCompiledCode = string.Empty;
            _objTable = null;
            _sceneTable = null;
        }

        private static Script CreateScript()
        {
            var s = new Script(
                CoreModules.Basic |
                CoreModules.Math |
                CoreModules.String |
                CoreModules.Table |
                CoreModules.Bit32 |
                CoreModules.TableIterators);

            s.Options.ScriptLoader = new DisabledFileScriptLoader();
            return s;
        }

        private void EnsureCompiled(string code)
        {
            if (_compiledChunk is not null && code == _lastCompiledCode) return;

            try
            {
                _compiledChunk = _script!.LoadString(code, null, "LuaScript");
                _lastCompiledCode = code;
            }
            catch (SyntaxErrorException ex)
            {
                throw new LuaScriptCompilationException(ex.DecoratedMessage ?? ex.Message, ex);
            }
        }

        private void SetupGlobals(AviUtlScriptContext ctx)
        {
            var s = _script!;

            s.Globals["time"] = ctx.Time;
            s.Globals["frame"] = ctx.Frame;
            s.Globals["totalframe"] = ctx.TotalFrame;
            s.Globals["framerate"] = ctx.Framerate;
            s.Globals["timelineframe"] = ctx.TimelineFrame;
            s.Globals["timelinetime"] = ctx.TimelineTime;
            s.Globals["layer"] = ctx.Layer;

            _sceneTable ??= new Table(s);
            _sceneTable["width"] = ctx.SceneWidth;
            _sceneTable["height"] = ctx.SceneHeight;
            s.Globals["scene"] = _sceneTable;

            bool firstSetup = _objTable is null;
            _objTable ??= new Table(s);

            _objTable["w"] = ctx.ImageWidth;
            _objTable["h"] = ctx.ImageHeight;
            _objTable["cx"] = ctx.ImageWidth / 2d;
            _objTable["cy"] = ctx.ImageHeight / 2d;
            _objTable["x"] = ctx.X;
            _objTable["y"] = ctx.Y;
            _objTable["z"] = ctx.Z;
            _objTable["ox"] = ctx.Ox;
            _objTable["oy"] = ctx.Oy;
            _objTable["zoom"] = ctx.Zoom;
            _objTable["aspect"] = ctx.Aspect;
            _objTable["alpha"] = ctx.Alpha;
            _objTable["rx"] = ctx.Rx;
            _objTable["ry"] = ctx.Ry;
            _objTable["rz"] = ctx.Rz;
            _objTable["track0"] = ctx.Track0;
            _objTable["track1"] = ctx.Track1;
            _objTable["track2"] = ctx.Track2;
            _objTable["track3"] = ctx.Track3;

            if (firstSetup)
                RegisterPixelCallbacks(_objTable);

            s.Globals["obj"] = _objTable;
        }

        private void RegisterPixelCallbacks(Table obj)
        {
            obj["getpixel"] = DynValue.NewCallback((_, args) =>
            {
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
            _thread.Interrupt();
            _thread.Join(millisecondsTimeout: 100);
            _queue.Dispose();
        }

        private sealed class DisabledFileScriptLoader : ScriptLoaderBase
        {
            public override object LoadFile(string file, Table globalContext)
                => throw new NotSupportedException("File access is not allowed in Lua script effects.");

            public override bool ScriptFileExists(string name) => false;
        }
    }
}
