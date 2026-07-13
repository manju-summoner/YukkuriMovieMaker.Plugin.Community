using System.IO;
using System.Runtime.InteropServices;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    internal sealed class Vst3Module
    {
        static readonly Dictionary<string, Vst3Module> cache = new(StringComparer.OrdinalIgnoreCase);
        static readonly object gate = new();

        readonly string modulePath;
        readonly IntPtr library;
        readonly IntPtr factory;
        int referenceCount;

        Vst3Module(string modulePath, IntPtr library, IntPtr factory)
        {
            this.modulePath = modulePath;
            this.library = library;
            this.factory = factory;
        }

        public static Vst3Module Acquire(string path)
        {
            var modulePath = ResolveModulePath(path);
            lock (gate)
            {
                if (cache.TryGetValue(modulePath, out var module))
                {
                    module.referenceCount++;
                    return module;
                }

                var library = NativeLibrary.Load(modulePath);
                try
                {
                    if (NativeLibrary.TryGetExport(library, "InitDll", out var initDll))
                    {
                        var init = Marshal.GetDelegateForFunctionPointer<Vst3Native.ModuleEntryDelegate>(initDll);
                        if (init() == 0)
                            throw new InvalidOperationException($"InitDll failed: {modulePath}");
                    }

                    var getPluginFactory = Marshal.GetDelegateForFunctionPointer<Vst3Native.GetPluginFactoryDelegate>(
                        NativeLibrary.GetExport(library, "GetPluginFactory"));
                    var factory = getPluginFactory();
                    if (factory == IntPtr.Zero)
                        throw new InvalidOperationException($"GetPluginFactory returned null: {modulePath}");

                    module = new Vst3Module(modulePath, library, factory) { referenceCount = 1 };
                    cache.Add(modulePath, module);
                    return module;
                }
                catch
                {
                    NativeLibrary.Free(library);
                    throw;
                }
            }
        }

        public void Release()
        {
            lock (gate)
            {
                if (--referenceCount > 0)
                    return;

                cache.Remove(modulePath);
                Vst3Native.GetVtableMethod<Vst3Native.ReleaseDelegate>(factory, 2)(factory);
                if (NativeLibrary.TryGetExport(library, "ExitDll", out var exitDll))
                    Marshal.GetDelegateForFunctionPointer<Vst3Native.ModuleEntryDelegate>(exitDll)();
                NativeLibrary.Free(library);
            }
        }

        public IEnumerable<byte[]> EnumerateAudioClassIds()
        {
            foreach (var (cid, _) in EnumerateAudioModuleClasses())
                yield return cid;
        }

        public IntPtr CreateInstance(byte[] cid, byte[] iid)
        {
            var createInstance = Vst3Native.GetVtableMethod<Vst3Native.CreateInstanceDelegate>(factory, 6);
            if (createInstance(factory, cid, iid, out var instance) == Vst3Native.ResultOk)
                return instance;
            return IntPtr.Zero;
        }

        IEnumerable<(byte[] Cid, string Name)> EnumerateAudioModuleClasses()
        {
            var countClasses = Vst3Native.GetVtableMethod<Vst3Native.CountClassesDelegate>(factory, 4);
            var getClassInfo = Vst3Native.GetVtableMethod<Vst3Native.GetClassInfoDelegate>(factory, 5);

            var count = countClasses(factory);
            var info = Marshal.AllocHGlobal(116);
            try
            {
                for (var i = 0; i < count; i++)
                {
                    if (getClassInfo(factory, i, info) != Vst3Native.ResultOk)
                        continue;

                    var category = Marshal.PtrToStringAnsi(info + 20, 32).TrimEnd('\0');
                    if (category != Vst3Native.AudioModuleClassCategory)
                        continue;

                    var cid = new byte[16];
                    Marshal.Copy(info, cid, 0, 16);
                    var name = Marshal.PtrToStringAnsi(info + 52, 64).TrimEnd('\0');
                    yield return (cid, name);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(info);
            }
        }

        static string ResolveModulePath(string path)
        {
            if (File.Exists(path))
                return Path.GetFullPath(path);

            if (Directory.Exists(path))
            {
                var architecture = RuntimeInformation.ProcessArchitecture switch
                {
                    Architecture.Arm64 => "arm64-win",
                    _ => "x86_64-win",
                };
                var binaryDirectory = Path.Combine(path, "Contents", architecture);
                if (Directory.Exists(binaryDirectory))
                {
                    var binary = Directory.EnumerateFiles(binaryDirectory, "*.vst3").FirstOrDefault();
                    if (binary is not null)
                        return Path.GetFullPath(binary);
                }
            }

            throw new FileNotFoundException($"VST3 module not found: {path}");
        }
    }
}
