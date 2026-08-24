using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx
{
    /// <summary>OpenCLプログラムを現在レンダー中のバックエンドcontextでコンパイルする最小suite。</summary>
    internal static unsafe class OfxOpenClProgramSuite
    {
        static readonly nint pointer;

        static OfxOpenClProgramSuite()
        {
            var suite = (Native*)NativeMemory.AllocZeroed((nuint)sizeof(Native));
            suite->compileProgram = (nint)(delegate* unmanaged[Cdecl]<byte*, int, void*, int>)&CompileProgram;
            pointer = (nint)suite;
        }

        public static nint Pointer => pointer;

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int CompileProgram(byte* source, int optional, void* result)
        {
            _ = optional;
            if (source is null || result is null || OpenClGpuRenderBackend.Current is not { IsAvailable: true } backend)
                return OfxStatus.Failed;
            try
            {
                var sourceText = Marshal.PtrToStringUTF8((nint)source);
                if (string.IsNullOrEmpty(sourceText))
                    return OfxStatus.Failed;
                *(nint*)result = backend.CompileProgram(sourceText);
                return OfxStatus.OK;
            }
            catch (OpenClException e)
            {
                OfxHostLog.Info($"OfxOpenCLProgramSuite.compileProgram が失敗しました。{e.Message}");
                *(nint*)result = 0;
                return OfxStatus.Failed;
            }
            catch (Exception e)
            {
                OfxHostLog.Info($"OfxOpenCLProgramSuite.compileProgram で例外: {e}");
                *(nint*)result = 0;
                return OfxStatus.Failed;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct Native
        {
            public nint compileProgram;
        }
    }
}
