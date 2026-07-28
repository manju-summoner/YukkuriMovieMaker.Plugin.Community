using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx
{
    /// <summary>
    /// OfxMemorySuiteV1 のホスト実装（ofxMemory.h と一致させること）
    /// </summary>
    internal static unsafe class OfxMemorySuite
    {
        [StructLayout(LayoutKind.Sequential)]
        struct SuiteNative
        {
            public nint memoryAlloc;
            public nint memoryFree;
        }

        static readonly object initSync = new();
        static nint suitePointer;

        public static nint Pointer
        {
            get
            {
                lock (initSync)
                {
                    if (suitePointer != 0)
                        return suitePointer;
                    var suite = (SuiteNative*)NativeMemory.AllocZeroed((nuint)sizeof(SuiteNative));
                    suite->memoryAlloc = (nint)(delegate* unmanaged[Cdecl]<nint, nuint, nint*, int>)&MemoryAlloc;
                    suite->memoryFree = (nint)(delegate* unmanaged[Cdecl]<nint, int>)&MemoryFree;
                    suitePointer = (nint)suite;
                }
                return suitePointer;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int MemoryAlloc(nint handle, nuint nBytes, nint* allocatedData)
        {
            try
            {
                if (allocatedData is null)
                    return OfxStatus.ErrValue;
                *allocatedData = (nint)NativeMemory.Alloc(nBytes);
                return OfxStatus.OK;
            }
            catch (OutOfMemoryException)
            {
                return OfxStatus.ErrMemory;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"memoryAlloc で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int MemoryFree(nint allocatedData)
        {
            try
            {
                if (allocatedData != 0)
                    NativeMemory.Free((void*)allocatedData);
                return OfxStatus.OK;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"memoryFree で例外: {ex}");
                return OfxStatus.Failed;
            }
        }
    }

    /// <summary>
    /// OfxMultiThreadSuiteV1 のホスト実装（ofxMultiThread.h と一致させること）
    /// </summary>
    internal static unsafe class OfxMultiThreadSuite
    {
        [StructLayout(LayoutKind.Sequential)]
        struct SuiteNative
        {
            public nint multiThread;
            public nint multiThreadNumCPUs;
            public nint multiThreadIndex;
            public nint multiThreadIsSpawnedThread;
            public nint mutexCreate;
            public nint mutexDestroy;
            public nint mutexLock;
            public nint mutexUnLock;
            public nint mutexTryLock;
        }

        /// <summary>
        /// OFXの再帰mutex。所有スレッドは再帰的にロックでき、解放は他スレッドからも行える
        /// （初期ロックカウント付きで生成された場合、作成側が所有しない「ロック済み」状態から始まるため）。
        /// </summary>
        sealed class OfxMutex : OfxObject
        {
            readonly object gate = new();
            int lockCount;
            Thread? owner;

            public OfxMutex(int initialLockCount)
            {
                lockCount = Math.Max(0, initialLockCount);
                // 初期ロックは作成スレッドの所有とする。無所有のロック状態から始めると、
                // 作成スレッド自身の直後のLockが解放不能な待ちに入ってしまう
                if (lockCount > 0)
                    owner = Thread.CurrentThread;
            }

            public void Lock()
            {
                lock (gate)
                {
                    while (lockCount > 0 && owner != Thread.CurrentThread)
                        Monitor.Wait(gate);
                    owner = Thread.CurrentThread;
                    lockCount++;
                }
            }

            public bool TryLock()
            {
                lock (gate)
                {
                    if (lockCount > 0 && owner != Thread.CurrentThread)
                        return false;
                    owner = Thread.CurrentThread;
                    lockCount++;
                    return true;
                }
            }

            public bool Unlock()
            {
                lock (gate)
                {
                    if (lockCount == 0)
                        return false;
                    lockCount--;
                    if (lockCount == 0)
                    {
                        owner = null;
                        Monitor.PulseAll(gate);
                    }
                    return true;
                }
            }
        }

        [ThreadStatic]
        static uint spawnedThreadIndex;
        [ThreadStatic]
        static bool isSpawnedThread;

        static readonly object initSync = new();
        static nint suitePointer;

        public static nint Pointer
        {
            get
            {
                lock (initSync)
                {
                    if (suitePointer != 0)
                        return suitePointer;
                    var suite = (SuiteNative*)NativeMemory.AllocZeroed((nuint)sizeof(SuiteNative));
                    suite->multiThread = (nint)(delegate* unmanaged[Cdecl]<nint, uint, nint, int>)&MultiThread;
                    suite->multiThreadNumCPUs = (nint)(delegate* unmanaged[Cdecl]<uint*, int>)&MultiThreadNumCPUs;
                    suite->multiThreadIndex = (nint)(delegate* unmanaged[Cdecl]<uint*, int>)&MultiThreadIndex;
                    suite->multiThreadIsSpawnedThread = (nint)(delegate* unmanaged[Cdecl]<int>)&MultiThreadIsSpawnedThread;
                    suite->mutexCreate = (nint)(delegate* unmanaged[Cdecl]<nint*, int, int>)&MutexCreate;
                    suite->mutexDestroy = (nint)(delegate* unmanaged[Cdecl]<nint, int>)&MutexDestroy;
                    suite->mutexLock = (nint)(delegate* unmanaged[Cdecl]<nint, int>)&MutexLock;
                    suite->mutexUnLock = (nint)(delegate* unmanaged[Cdecl]<nint, int>)&MutexUnLock;
                    suite->mutexTryLock = (nint)(delegate* unmanaged[Cdecl]<nint, int>)&MutexTryLock;
                    suitePointer = (nint)suite;
                }
                return suitePointer;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int MultiThread(nint func, uint nThreads, nint customArg)
        {
            try
            {
                if (func == 0)
                    return OfxStatus.ErrValue;
                var function = (delegate* unmanaged[Cdecl]<uint, uint, nint, void>)func;
                if (nThreads == 0)
                    nThreads = (uint)Environment.ProcessorCount;

                // 仕様上ネストした multiThread は逐次実行になる。
                // 極端なスレッド数の要求もタスクを量産せず逐次で処理する（呼び出し回数の契約は守る）
                if (nThreads == 1 || isSpawnedThread || nThreads > 1024)
                {
                    var outerIsSpawned = isSpawnedThread;
                    var outerIndex = spawnedThreadIndex;
                    try
                    {
                        for (var i = 0u; i < nThreads; i++)
                        {
                            isSpawnedThread = true;
                            spawnedThreadIndex = i;
                            function(i, nThreads, customArg);
                        }
                    }
                    finally
                    {
                        isSpawnedThread = outerIsSpawned;
                        spawnedThreadIndex = outerIndex;
                    }
                    return OfxStatus.OK;
                }

                // 呼び出しスレッド（多くはスレッドプール上のレンダリングスレッド）を遊ばせて待つと
                // プール飽和時に注入待ちでストールするため、インデックス0は呼び出しスレッドで実行する
                var tasks = new Task[nThreads - 1];
                for (var i = 1u; i < nThreads; i++)
                {
                    var index = i;
                    tasks[index - 1] = Task.Run(() =>
                    {
                        isSpawnedThread = true;
                        spawnedThreadIndex = index;
                        try
                        {
                            function(index, nThreads, customArg);
                        }
                        finally
                        {
                            isSpawnedThread = false;
                            spawnedThreadIndex = 0;
                        }
                    });
                }
                try
                {
                    isSpawnedThread = true;
                    spawnedThreadIndex = 0;
                    try
                    {
                        function(0, nThreads, customArg);
                    }
                    finally
                    {
                        isSpawnedThread = false;
                        spawnedThreadIndex = 0;
                    }
                }
                finally
                {
                    // インデックス0が失敗しても、ワーカーがcustomArgへアクセスしたまま
                    // プラグインへ制御を返さない（ネイティブ側のuse-after-free防止）
                    Task.WaitAll(tasks);
                }
                return OfxStatus.OK;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"multiThread で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int MultiThreadNumCPUs(uint* nCPUs)
        {
            if (nCPUs is null)
                return OfxStatus.ErrValue;
            *nCPUs = (uint)Environment.ProcessorCount;
            return OfxStatus.OK;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int MultiThreadIndex(uint* threadIndex)
        {
            if (threadIndex is null)
                return OfxStatus.ErrValue;
            *threadIndex = isSpawnedThread ? spawnedThreadIndex : 0;
            return OfxStatus.OK;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int MultiThreadIsSpawnedThread()
        {
            return isSpawnedThread ? 1 : 0;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int MutexCreate(nint* mutex, int lockCount)
        {
            try
            {
                if (mutex is null)
                    return OfxStatus.ErrValue;
                var created = new OfxMutex(lockCount);
                *mutex = created.Handle;
                return OfxStatus.OK;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"mutexCreate で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int MutexDestroy(nint mutex)
        {
            try
            {
                var found = OfxHandleTable.Get<OfxMutex>(mutex);
                if (found is null)
                    return OfxStatus.ErrBadHandle;
                found.Dispose();
                return OfxStatus.OK;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"mutexDestroy で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int MutexLock(nint mutex)
        {
            try
            {
                var found = OfxHandleTable.Get<OfxMutex>(mutex);
                if (found is null)
                    return OfxStatus.ErrBadHandle;
                found.Lock();
                return OfxStatus.OK;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"mutexLock で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int MutexUnLock(nint mutex)
        {
            try
            {
                var found = OfxHandleTable.Get<OfxMutex>(mutex);
                if (found is null)
                    return OfxStatus.ErrBadHandle;
                return found.Unlock() ? OfxStatus.OK : OfxStatus.Failed;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"mutexUnLock で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int MutexTryLock(nint mutex)
        {
            try
            {
                var found = OfxHandleTable.Get<OfxMutex>(mutex);
                if (found is null)
                    return OfxStatus.ErrBadHandle;
                return found.TryLock() ? OfxStatus.OK : OfxStatus.Failed;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"mutexTryLock で例外: {ex}");
                return OfxStatus.Failed;
            }
        }
    }

    /// <summary>
    /// OfxMessageSuiteV1 / V2 のホスト実装（ofxMessage.h と一致させること）。
    /// メッセージはログへ流す（UI表示との連携は将来の拡張点）。
    /// message はprintf形式の可変長引数を取るため、OfxParameterSuite と同じ
    /// Win x64 varargs スロット読みで最大4個の可変引数を整形する。
    /// </summary>
    internal static unsafe class OfxMessageSuite
    {
        [StructLayout(LayoutKind.Sequential)]
        struct SuiteV1Native
        {
            public nint message;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SuiteV2Native
        {
            public nint message;
            public nint setPersistentMessage;
            public nint clearPersistentMessage;
        }

        static readonly object initSync = new();
        static nint suiteV1Pointer;
        static nint suiteV2Pointer;

        public static nint PointerV1
        {
            get
            {
                lock (initSync)
                {
                    if (suiteV1Pointer != 0)
                        return suiteV1Pointer;
                    var suite = (SuiteV1Native*)NativeMemory.AllocZeroed((nuint)sizeof(SuiteV1Native));
                    suite->message = (nint)(delegate* unmanaged[Cdecl]<nint, byte*, byte*, byte*, nint, nint, nint, nint, int>)&Message;
                    suiteV1Pointer = (nint)suite;
                    return suiteV1Pointer;
                }
            }
        }

        public static nint PointerV2
        {
            get
            {
                lock (initSync)
                {
                    if (suiteV2Pointer != 0)
                        return suiteV2Pointer;
                    var suite = (SuiteV2Native*)NativeMemory.AllocZeroed((nuint)sizeof(SuiteV2Native));
                    suite->message = (nint)(delegate* unmanaged[Cdecl]<nint, byte*, byte*, byte*, nint, nint, nint, nint, int>)&Message;
                    suite->setPersistentMessage = (nint)(delegate* unmanaged[Cdecl]<nint, byte*, byte*, byte*, nint, nint, nint, nint, int>)&SetPersistentMessage;
                    suite->clearPersistentMessage = (nint)(delegate* unmanaged[Cdecl]<nint, int>)&ClearPersistentMessage;
                    suiteV2Pointer = (nint)suite;
                    return suiteV2Pointer;
                }
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int Message(nint handle, byte* messageType, byte* messageId, byte* format, nint a4, nint a5, nint a6, nint a7)
        {
            try
            {
                var type = Marshal.PtrToStringUTF8((nint)messageType) ?? "";
                var text = FormatMessage(format, a4, a5, a6, a7);
                OfxHostLog.Info($"プラグインからのメッセージ ({type}): {text}");
                // 質問メッセージはUIを出せないため常に「はい」で応答する
                return type == OfxConstants.MessageQuestion ? OfxStatus.ReplyYes : OfxStatus.OK;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"message で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int SetPersistentMessage(nint handle, byte* messageType, byte* messageId, byte* format, nint a4, nint a5, nint a6, nint a7)
        {
            try
            {
                var type = Marshal.PtrToStringUTF8((nint)messageType) ?? "";
                var text = FormatMessage(format, a4, a5, a6, a7);
                OfxHostLog.Info($"プラグインからの永続メッセージ ({type}): {text}");
                return OfxStatus.OK;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"setPersistentMessage で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int ClearPersistentMessage(nint handle)
        {
            return OfxStatus.OK;
        }

        /// <summary>
        /// printf形式の書式を最小限（%d %i %u %x %f %e %g %s %c %%）だけ解釈して整形する。
        /// 解釈できない書式が現れた場合は以降を書式文字列のまま返す。
        /// </summary>
        internal static string FormatMessage(byte* format, nint a4, nint a5, nint a6, nint a7)
        {
            var formatString = Marshal.PtrToStringUTF8((nint)format) ?? "";
            var slots = stackalloc nint[4] { a4, a5, a6, a7 };
            var slotIndex = 0;
            var builder = new StringBuilder(formatString.Length + 32);
            for (var i = 0; i < formatString.Length; i++)
            {
                var c = formatString[i];
                if (c != '%')
                {
                    builder.Append(c);
                    continue;
                }
                if (i + 1 >= formatString.Length)
                    break;
                // フラグ・幅・精度は読み飛ばす（値の整形は既定書式で行う）
                var j = i + 1;
                while (j < formatString.Length && (char.IsDigit(formatString[j]) || formatString[j] is '-' or '+' or ' ' or '#' or '.' or 'l' or 'h'))
                    j++;
                if (j >= formatString.Length)
                    break;
                var spec = formatString[j];
                if (spec == '%')
                {
                    builder.Append('%');
                    i = j;
                    continue;
                }
                if (slotIndex >= 4)
                {
                    // 引数を使い切った場合は以降を書式のまま出力する
                    builder.Append(formatString[i..]);
                    break;
                }
                var slot = slots[slotIndex];
                switch (spec)
                {
                    case 'd' or 'i':
                        builder.Append((int)slot);
                        slotIndex++;
                        break;
                    case 'u':
                        builder.Append((uint)slot);
                        slotIndex++;
                        break;
                    case 'x' or 'X':
                        builder.Append(((uint)slot).ToString(spec == 'x' ? "x" : "X"));
                        slotIndex++;
                        break;
                    case 'f' or 'e' or 'E' or 'g' or 'G':
                        builder.Append(BitConverter.Int64BitsToDouble(slot));
                        slotIndex++;
                        break;
                    case 's':
                        builder.Append(Marshal.PtrToStringUTF8(slot) ?? "");
                        slotIndex++;
                        break;
                    case 'c':
                        builder.Append((char)(int)slot);
                        slotIndex++;
                        break;
                    default:
                        // 未対応の書式は以降をそのまま出力する
                        builder.Append(formatString[i..]);
                        return builder.ToString();
                }
                i = j;
            }
            return builder.ToString();
        }
    }
}
