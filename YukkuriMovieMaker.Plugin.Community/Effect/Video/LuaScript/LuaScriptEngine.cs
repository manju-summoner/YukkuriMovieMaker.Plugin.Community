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

        private Script? _script;
        private DynValue? _compiledChunk;
        private string _lastCompiledCode = string.Empty;

        private Table? _objTable;
        private Table? _sceneTable;

        private AviUtlScriptContext? _currentContext;
        private PixelDataProxy? _pixelProxy;

        private Script CreateScript()
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

        private void EnsureScript()
        {
            if (_script is not null) return;
            _script = CreateScript();
            _compiledChunk = null;
            _lastCompiledCode = string.Empty;
            _objTable = null;
            _sceneTable = null;
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
                if (_currentContext is null) return DynValue.Nil;
                int x = (int)(args[0].CastToNumber() ?? 0d);
                int y = (int)(args[1].CastToNumber() ?? 0d);
                var (r, g, b, a) = _currentContext.GetPixel(x, y);
                return DynValue.NewTuple(
                    DynValue.NewNumber(r),
                    DynValue.NewNumber(g),
                    DynValue.NewNumber(b),
                    DynValue.NewNumber(a));
            });

            obj["setpixel"] = DynValue.NewCallback((_, args) =>
            {
                if (_currentContext is null) return DynValue.Void;
                int x = (int)(args[0].CastToNumber() ?? 0d);
                int y = (int)(args[1].CastToNumber() ?? 0d);
                double r = args[2].CastToNumber() ?? 0d;
                double g = args[3].CastToNumber() ?? 0d;
                double b = args[4].CastToNumber() ?? 0d;
                double a = args.Count > 5 ? args[5].CastToNumber() ?? 255d : 255d;
                _currentContext.SetPixel(x, y, r, g, b, a);
                return DynValue.Void;
            });

            obj["getpixeldata"] = DynValue.NewCallback((_, _) =>
            {
                if (_currentContext is null) return DynValue.Nil;
                _currentContext.EnsurePixelBuffer();
                _pixelProxy ??= new PixelDataProxy(_currentContext);
                return UserData.Create(_pixelProxy);
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

        public void Execute(string code, AviUtlScriptContext ctx)
        {
            EnsureScript();
            EnsureCompiled(code);

            _currentContext = ctx;
            _pixelProxy = null;

            SetupGlobals(ctx);

            var scriptToCall = _script!;
            var chunkToCall = _compiledChunk!;

            var task = Task.Run(() =>
            {
                try
                {
                    scriptToCall.Call(chunkToCall);
                }
                catch (ScriptRuntimeException ex)
                {
                    throw new LuaScriptRuntimeException(ex.DecoratedMessage ?? ex.Message, ex);
                }
            });

            bool completed = task.Wait(ExecutionTimeoutMilliseconds);

            _currentContext = null;

            if (!completed)
            {
                _script = null;
                _compiledChunk = null;
                _lastCompiledCode = string.Empty;
                _objTable = null;
                _sceneTable = null;
                throw new LuaScriptRuntimeException($"Script execution timed out after {ExecutionTimeoutMilliseconds} ms.");
            }

            if (task.IsFaulted)
            {
                var inner = task.Exception!.InnerException;
                if (inner is LuaScriptRuntimeException rte)
                    throw rte;
                throw new LuaScriptRuntimeException(inner?.Message ?? task.Exception.Message, inner ?? task.Exception);
            }

            ReadBackGlobals(ctx);
        }

        public void Dispose()
        {
            _script = null;
            _compiledChunk = null;
            _objTable = null;
            _sceneTable = null;
            _currentContext = null;
            _pixelProxy = null;
        }

        private sealed class DisabledFileScriptLoader : ScriptLoaderBase
        {
            public override object LoadFile(string file, Table globalContext)
                => throw new NotSupportedException("File access is not allowed in Lua script effects.");

            public override bool ScriptFileExists(string name) => false;
        }
    }
}
