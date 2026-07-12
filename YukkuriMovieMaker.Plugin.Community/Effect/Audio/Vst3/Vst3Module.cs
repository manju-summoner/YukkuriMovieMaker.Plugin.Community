using System;
using System.Collections.Generic;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    /// <summary>
    /// VST3モジュール（.vst3ファイル/バンドル）1つ分のラッパー。
    /// プラグインインスタンス生成後は、インスタンス側がネイティブ層でモジュール参照を保持するため、
    /// このラッパーを先に破棄しても問題ない。
    /// </summary>
    internal sealed unsafe class Vst3Module : IDisposable
    {
        IntPtr handle;

        Vst3Module(IntPtr handle)
        {
            this.handle = handle;
        }

        public static Vst3Module Open(string path)
        {
            var errorBuf = stackalloc byte[1024];
            var handle = Vst3Native.Ymm4Vst3ModuleOpen(path, errorBuf, 1024);
            if (handle == IntPtr.Zero)
                throw new InvalidOperationException($"VST3モジュールを開けませんでした。path={path} error={Vst3Native.FixedUtf8ToString(errorBuf, 1024)}");
            return new Vst3Module(handle);
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
