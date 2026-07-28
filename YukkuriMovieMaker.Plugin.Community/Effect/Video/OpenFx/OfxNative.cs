using System.Runtime.InteropServices;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx
{
    // OFX C API のネイティブ構造体定義。
    // レイアウトは openfx/include/ofxCore.h と一致させること（x64前提）。

    /// <summary>
    /// OfxPlugin 構造体（OfxGetPlugin が返すもの）
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct OfxPluginNative
    {
        public nint pluginApi;          // const char*
        public int apiVersion;
        public nint pluginIdentifier;   // const char*
        public uint pluginVersionMajor;
        public uint pluginVersionMinor;
        public nint setHost;            // void (*)(OfxHost*)
        public nint mainEntry;          // OfxStatus (*)(const char* action, const void* handle, OfxPropertySetHandle inArgs, OfxPropertySetHandle outArgs)
    }

    /// <summary>
    /// OfxHost 構造体（ホストがプラグインへ渡すもの）
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct OfxHostNative
    {
        public nint host;       // OfxPropertySetHandle
        public nint fetchSuite; // const void* (*)(OfxPropertySetHandle host, const char* suiteName, int suiteVersion)
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct OfxRectI
    {
        public int x1, y1, x2, y2;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct OfxRectD
    {
        public double x1, y1, x2, y2;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct OfxPointI
    {
        public int x, y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct OfxPointD
    {
        public double x, y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct OfxRangeI
    {
        public int min, max;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct OfxRangeD
    {
        public double min, max;
    }
}
