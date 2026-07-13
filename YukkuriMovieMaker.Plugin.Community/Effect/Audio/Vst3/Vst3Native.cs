using System;
using System.IO;
using System.Runtime.InteropServices;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    /// <summary>
    /// YukkuriMovieMaker.Vst3Bridge.dll のP/Invoke定義。
    /// 構造体レイアウトはブリッジ側 Ymm4Vst3ClassInfo と一致させること。
    /// </summary>
    internal static unsafe class Vst3Native
    {
        public const string DllName = "YukkuriMovieMaker.Vst3Bridge";
        internal const int RequiredBridgeApiVersion = 1;

        /// <summary>
        /// ブリッジDLL・スキャナーEXEの配置フォルダー
        /// </summary>
        internal static string Vst3BinaryDirectory => Path.Combine(AppDirectories.ResourceDirectory, "bin", "x64", "vst3");
        public const int RestartReloadComponent = 1 << 0;
        public const int RestartIoChanged = 1 << 1;
        public const int RestartLatencyChanged = 1 << 3;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int GetApiVersionCallback();

        static Vst3Native()
        {
            NativeLibrary.SetDllImportResolver(typeof(Vst3Native).Assembly, (libraryName, _, _) =>
            {
                if (libraryName is not DllName)
                    return IntPtr.Zero;
                var path = Path.Combine(Vst3BinaryDirectory, $"{DllName}.dll");
                if (!File.Exists(path) || !NativeLibrary.TryLoad(path, out var handle))
                    return IntPtr.Zero;
                if (TryGetBridgeApiVersion(handle, out var version)
                    && version == RequiredBridgeApiVersion)
                    return handle;
                NativeLibrary.Free(handle);
                throw new DllNotFoundException(
                    $"互換性のある{DllName}.dllが見つかりません。"
                    + $"必要なAPIバージョン={RequiredBridgeApiVersion} 候補={path} (API {version})");
            });
        }

        internal static bool TryGetBridgeApiVersion(IntPtr handle, out int version)
        {
            version = 0;
            if (!NativeLibrary.TryGetExport(handle, nameof(Ymm4Vst3GetApiVersion), out var address))
                return false;
            version = Marshal.GetDelegateForFunctionPointer<GetApiVersionCallback>(address)();
            return true;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NativeClassInfo
        {
            public fixed byte ClassId[64];
            public fixed byte Name[256];
            public fixed byte Vendor[256];
            public fixed byte Category[128];
            public fixed byte SubCategories[256];
            public fixed byte Version[64];
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void ViewResizeCallback(IntPtr context, int width, int height);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void ParameterChangeCallback(IntPtr context, uint paramId, double normalizedValue);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void MeterParameterChangeCallback(
            IntPtr context,
            uint paramId,
            double normalizedValue,
            long samplePosition);

        [DllImport(DllName)]
        public static extern int Ymm4Vst3GetApiVersion();

        [DllImport(DllName)]
        public static extern IntPtr Ymm4Vst3ModuleOpen([MarshalAs(UnmanagedType.LPUTF8Str)] string path, byte* errorBuf, int errorBufSize);

        [DllImport(DllName)]
        public static extern void Ymm4Vst3ModuleClose(IntPtr module);

        [DllImport(DllName)]
        public static extern int Ymm4Vst3ModuleGetClassCount(IntPtr module);

        [DllImport(DllName)]
        public static extern int Ymm4Vst3ModuleGetClassInfo(IntPtr module, int index, out NativeClassInfo info);

        [DllImport(DllName)]
        public static extern IntPtr Ymm4Vst3PluginCreate(IntPtr module, [MarshalAs(UnmanagedType.LPUTF8Str)] string classId, byte* errorBuf, int errorBufSize);

        [DllImport(DllName)]
        public static extern int Ymm4Vst3PluginSetup(IntPtr plugin, double sampleRate, int maxBlockSize);

        [DllImport(DllName)]
        public static extern int Ymm4Vst3PluginProcess(IntPtr plugin, float* inL, float* inR, float* outL, float* outR, int numFrames, long projectTimeSamples);

        [DllImport(DllName)]
        public static extern int Ymm4Vst3PluginProcessWithTransport(
            IntPtr plugin,
            float* inL, float* inR,
            float* outL, float* outR,
            int numFrames,
            long projectTimeSamples,
            double tempo,
            int timeSignatureNumerator,
            int timeSignatureDenominator,
            int isTempoValid,
            int captureMeterParameters);

        [DllImport(DllName)]
        public static extern int Ymm4Vst3PluginPump(IntPtr plugin);

        [DllImport(DllName)]
        public static extern int Ymm4Vst3PluginDrainEditorParameterChanges(
            IntPtr plugin,
            ParameterChangeCallback callback,
            IntPtr context);

        [DllImport(DllName)]
        public static extern int Ymm4Vst3PluginDrainMeterParameterChanges(
            IntPtr plugin,
            MeterParameterChangeCallback callback,
            IntPtr context);

        [DllImport(DllName)]
        public static extern int Ymm4Vst3PluginSetParameter(IntPtr plugin, uint paramId, double normalizedValue);

        [DllImport(DllName)]
        public static extern int Ymm4Vst3PluginSetControllerParameter(IntPtr plugin, uint paramId, double normalizedValue);

        [DllImport(DllName)]
        public static extern int Ymm4Vst3PluginReset(IntPtr plugin);

        [DllImport(DllName)]
        public static extern int Ymm4Vst3PluginGetLatencySamples(IntPtr plugin);

        [DllImport(DllName)]
        public static extern int Ymm4Vst3PluginConsumeRestartFlags(IntPtr plugin);

#if DEBUG
        [DllImport(DllName)]
        public static extern void Ymm4Vst3PluginRequestRestartForTest(IntPtr plugin, int flags);

        [DllImport(DllName)]
        public static extern void Ymm4Vst3PluginPerformEditForTest(IntPtr plugin, uint paramId, double normalizedValue);

        [DllImport(DllName)]
        public static extern double Ymm4Vst3PluginGetControllerParameterForTest(IntPtr plugin, uint paramId);
#endif

        [DllImport(DllName)]
        public static extern int Ymm4Vst3PluginGetState(IntPtr plugin, out IntPtr componentData, out int componentSize, out IntPtr controllerData, out int controllerSize);

        [DllImport(DllName)]
        public static extern int Ymm4Vst3PluginSetState(IntPtr plugin, byte[]? componentData, int componentSize, byte[]? controllerData, int controllerSize);

        [DllImport(DllName)]
        public static extern void Ymm4Vst3PluginDestroy(IntPtr plugin);

        [DllImport(DllName)]
        public static extern void Ymm4Vst3Free(IntPtr buffer);

        [DllImport(DllName)]
        public static extern IntPtr Ymm4Vst3ViewCreate(IntPtr plugin);

        [DllImport(DllName)]
        public static extern int Ymm4Vst3ViewGetSize(IntPtr view, out int width, out int height);

        [DllImport(DllName)]
        public static extern int Ymm4Vst3ViewCanResize(IntPtr view);

        [DllImport(DllName)]
        public static extern int Ymm4Vst3ViewAttach(IntPtr view, IntPtr hwnd, ViewResizeCallback? resizeCallback, IntPtr callbackContext);

        [DllImport(DllName)]
        public static extern int Ymm4Vst3ViewOnSize(IntPtr view, ref int width, ref int height);

        [DllImport(DllName)]
        public static extern int Ymm4Vst3ViewSetContentScale(IntPtr view, float scaleFactor);

        [DllImport(DllName)]
        public static extern int Ymm4Vst3ViewIsContentScaleSupported(IntPtr view);

        [DllImport(DllName)]
        public static extern void Ymm4Vst3ViewDestroy(IntPtr view);

        public static string FixedUtf8ToString(byte* buffer, int maxLength)
        {
            var length = 0;
            while (length < maxLength && buffer[length] != 0)
                length++;
            return System.Text.Encoding.UTF8.GetString(buffer, length);
        }
    }
}
