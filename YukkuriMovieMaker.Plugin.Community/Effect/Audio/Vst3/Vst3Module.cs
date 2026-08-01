using System;
using System.Collections.Generic;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    /// <summary>
    /// VST3モジュール（.vst3ファイル/バンドル）1つ分のラッパー。
    /// プラグインインスタンス生成後は、インスタンス側がネイティブ層でモジュール参照を保持するため、
    /// このラッパーを先に破棄しても問題ない。
    /// 一度プラグインインスタンスを生成したモジュールのDLLはプロセス終了までピン留めされ、実行中にアンロードされることはない。
    /// </summary>
    internal sealed unsafe class Vst3Module : IDisposable
    {
        // 一度インスタンスを生成したモジュールはプロセス終了までアンロードしない（一般的なDAWと同じ方針）。
        // 全インスタンスの破棄でDLLがアンマップされると、プラグインが登録したままの
        // ウィンドウプロシージャやフックが無効な飛び先になり、以後のメッセージ配送で
        // クラッシュする（CFGのFAST_FAIL_GUARD_ICALL_CHECK_FAILURE。ZL Equalizer 2で実証）
        static readonly object pinSync = new();
        static readonly Dictionary<string, IntPtr> pinnedModules = new(StringComparer.OrdinalIgnoreCase);

        IntPtr handle;
        readonly string path;

        Vst3Module(IntPtr handle, string path)
        {
            this.handle = handle;
            this.path = path;
        }

        public static Vst3Module Open(string path)
        {
            var errorBuf = stackalloc byte[1024];
            var handle = Vst3Native.Ymm4Vst3ModuleOpen(path, errorBuf, 1024);
            if (handle == IntPtr.Zero)
                throw new InvalidOperationException($"VST3モジュールを開けませんでした。path={path} error={Vst3Native.FixedUtf8ToString(errorBuf, 1024)}");
            return new Vst3Module(handle, path);
        }

        /// <summary>
        /// モジュールへの参照を1つ確保したままにして、プロセス終了までDLLをピン留めする
        /// </summary>
        static void PinModule(string path)
        {
            lock (pinSync)
            {
                if (pinnedModules.ContainsKey(path))
                    return;
                var errorBuf = stackalloc byte[1024];
                var pin = Vst3Native.Ymm4Vst3ModuleOpen(path, errorBuf, 1024);
                if (pin != IntPtr.Zero)
                    pinnedModules[path] = pin;
            }
        }

        /// <summary>
        /// モジュール内のオーディオエフェクトクラス（Audio Module Class）を列挙する
        /// </summary>
        public IReadOnlyList<Vst3ClassInfo> GetAudioModuleClasses()
        {
            ObjectDisposedException.ThrowIf(handle == IntPtr.Zero, this);
            var result = new List<Vst3ClassInfo>();
            var count = Vst3Native.Ymm4Vst3ModuleGetClassCount(handle);
            for (var i = 0; i < count; i++)
            {
                if (Vst3Native.Ymm4Vst3ModuleGetClassInfo(handle, i, out var info) == 0)
                    continue;
                var classInfo = new Vst3ClassInfo(
                    Vst3Native.FixedUtf8ToString(info.ClassId, 64),
                    Vst3Native.FixedUtf8ToString(info.Name, 256),
                    Vst3Native.FixedUtf8ToString(info.Vendor, 256),
                    Vst3Native.FixedUtf8ToString(info.Category, 128),
                    Vst3Native.FixedUtf8ToString(info.SubCategories, 256),
                    Vst3Native.FixedUtf8ToString(info.Version, 64));
                if (classInfo.IsAudioModuleClass)
                    result.Add(classInfo);
            }
            return result;
        }

        public Vst3Plugin CreatePlugin(string classId)
        {
            ObjectDisposedException.ThrowIf(handle == IntPtr.Zero, this);
            var errorBuf = stackalloc byte[1024];
            var plugin = Vst3Native.Ymm4Vst3PluginCreate(handle, classId, errorBuf, 1024);
            if (plugin == IntPtr.Zero)
                throw new InvalidOperationException($"VST3プラグインを作成できませんでした。classId={classId} error={Vst3Native.FixedUtf8ToString(errorBuf, 1024)}");
            // ピン留めはインスタンスを実際に生成したモジュールに限る。
            // 一覧スキャンは隔離プロセスで行うため、本体でのOpenは実利用時だけになる。
            PinModule(path);
            return new Vst3Plugin(plugin);
        }

        public void Dispose()
        {
            if (handle == IntPtr.Zero)
                return;
            Vst3Native.Ymm4Vst3ModuleClose(handle);
            handle = IntPtr.Zero;
        }
    }

    internal record Vst3ClassInfo(string ClassId, string Name, string Vendor, string Category, string SubCategories, string Version)
    {
        public bool IsAudioModuleClass => Category is "Audio Module Class";
        /// <summary>
        /// エフェクト系プラグインかどうか（インストゥルメント除外）
        /// </summary>
        public bool IsEffect => SubCategories.Contains("Fx", StringComparison.OrdinalIgnoreCase);
    }
}
