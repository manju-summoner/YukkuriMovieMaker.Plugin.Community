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
    /// OfxProgressSuiteV1 の最小ホスト実装。YMM4では進捗UIを出さず、処理継続を返す。
    /// </summary>
    internal static unsafe class OfxProgressSuite
    {
        [StructLayout(LayoutKind.Sequential)]
        struct SuiteNative
        {
            public nint progressStart;
            public nint progressUpdate;
            public nint progressEnd;
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
                    suite->progressStart = (nint)(delegate* unmanaged[Cdecl]<nint, byte*, int>)&ProgressStart;
                    suite->progressUpdate = (nint)(delegate* unmanaged[Cdecl]<nint, double, int>)&ProgressUpdate;
                    suite->progressEnd = (nint)(delegate* unmanaged[Cdecl]<nint, int>)&ProgressEnd;
                    suitePointer = (nint)suite;
                }
                return suitePointer;
            }
        }

        static bool IsValidInstance(nint effectInstance)
            => OfxHandleTable.Get<OfxEffectInstance>(effectInstance) is not null;

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int ProgressStart(nint effectInstance, byte* label)
        {
            try
            {
                return IsValidInstance(effectInstance) ? OfxStatus.OK : OfxStatus.ErrBadHandle;
            }
            catch
            {
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int ProgressUpdate(nint effectInstance, double progress)
        {
            try
            {
                return IsValidInstance(effectInstance) ? OfxStatus.OK : OfxStatus.ErrBadHandle;
            }
            catch
            {
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int ProgressEnd(nint effectInstance)
        {
            try
            {
                return IsValidInstance(effectInstance) ? OfxStatus.OK : OfxStatus.ErrBadHandle;
            }
            catch
            {
                return OfxStatus.Failed;
            }
        }
    }

    /// <summary>
    /// OfxTimeLineSuiteV1 のホスト実装。時刻移動はYMM4のレンダリング駆動と競合するため非対応。
    /// </summary>
    internal static unsafe class OfxTimeLineSuite
    {
        [StructLayout(LayoutKind.Sequential)]
        struct SuiteNative
        {
            public nint getTime;
            public nint gotoTime;
            public nint getTimeBounds;
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
                    suite->getTime = (nint)(delegate* unmanaged[Cdecl]<nint, double*, int>)&GetTime;
                    suite->gotoTime = (nint)(delegate* unmanaged[Cdecl]<nint, double, int>)&GotoTime;
                    suite->getTimeBounds = (nint)(delegate* unmanaged[Cdecl]<nint, double*, double*, int>)&GetTimeBounds;
                    suitePointer = (nint)suite;
                }
                return suitePointer;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int GetTime(nint instance, double* time)
        {
            try
            {
                var effect = OfxHandleTable.Get<OfxEffectInstance>(instance);
                if (effect is null)
                    return OfxStatus.ErrBadHandle;
                if (time is null)
                    return OfxStatus.ErrValue;
                *time = effect.CurrentTime;
                return OfxStatus.OK;
            }
            catch
            {
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int GotoTime(nint instance, double time)
        {
            try
            {
                return OfxHandleTable.Get<OfxEffectInstance>(instance) is null
                    ? OfxStatus.ErrBadHandle
                    : OfxStatus.Failed;
            }
            catch
            {
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int GetTimeBounds(nint instance, double* firstTime, double* lastTime)
        {
            try
            {
                var effect = OfxHandleTable.Get<OfxEffectInstance>(instance);
                if (effect is null)
                    return OfxStatus.ErrBadHandle;
                if (firstTime is null || lastTime is null)
                    return OfxStatus.ErrValue;
                *firstTime = 0;
                *lastTime = Math.Max(0, effect.DurationFrames - 1);
                return OfxStatus.OK;
            }
            catch
            {
                return OfxStatus.Failed;
            }
        }
    }

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

                // ワーカーは必ず専用スレッドで実行し、呼び出し元スレッド（下にプラグインのネイティブフレームがある
                // リバースP/Invoke混在スタック）では直接呼ばない。プラグイン内のゼロ除算等のハードウェア例外が
                // 混在スタックを跨いで伝播すると、.NET 10のマネージドEHがディスパッチループに陥りStackOverflowで落ちる
                // （openfx-misc ColorBarsのExtent=Size・サイズ0で実測。LegacyExceptionHandlingスイッチは.NET 10で削除済み）。
                // 専用スレッドなら「マネージド起点スタック→ネイティブ1区間→フォールト」の形になり、通常のcatchで受け止められる。
                // プール飽和時も進行が保証されるため、スレッドプールも使わない。
                //
                // 仕様上ネストした multiThread は逐次実行になる。極端なスレッド数の要求も逐次で処理する（呼び出し回数の契約は守る）
                if (nThreads == 1 || isSpawnedThread || nThreads > 1024)
                {
                    for (var i = 0u; i < nThreads; i++)
                    {
                        var failure = RunWorkerOnDedicatedThread(function, i, nThreads, customArg);
                        if (failure is not null)
                        {
                            OfxHostLog.Info($"multiThread ワーカーで例外: {failure}");
                            return OfxStatus.Failed;
                        }
                    }
                    return OfxStatus.OK;
                }

                var threads = new Thread[nThreads];
                var failures = new Exception?[nThreads];
                for (var i = 0u; i < nThreads; i++)
                {
                    var index = i;
                    threads[index] = new Thread(() => failures[index] = RunWorker(function, index, nThreads, customArg))
                    {
                        IsBackground = true,
                        Name = $"OFX multiThread worker {index}",
                    };
                    threads[index].Start();
                }
                // ワーカーがcustomArgへアクセスしたままプラグインへ制御を返さない（ネイティブ側のuse-after-free防止）
                foreach (var thread in threads)
                    thread.Join();
                var firstFailure = Array.Find(failures, f => f is not null);
                if (firstFailure is not null)
                {
                    OfxHostLog.Info($"multiThread ワーカーで例外: {firstFailure}");
                    return OfxStatus.Failed;
                }
                return OfxStatus.OK;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"multiThread で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        /// <summary>
        /// ワーカー1つ分を専用スレッドで実行して完了を待つ（逐次実行用）。
        /// ネストしたmultiThreadも呼び出し元と別スレッドになるため、
        /// 「mutexを保持したままネストし、ネスト側ワーカーが同じmutexを取る」プラグインは
        /// 再帰ロックが効かず待ちになる制約がある（同一スレッド実行はEHディスパッチループのSOEを踏むため不可。
        /// 該当パターンのプラグインは現状未確認）
        /// </summary>
        static Exception? RunWorkerOnDedicatedThread(delegate* unmanaged[Cdecl]<uint, uint, nint, void> function, uint index, uint nThreads, nint customArg)
        {
            Exception? failure = null;
            var thread = new Thread(() => failure = RunWorker(function, index, nThreads, customArg))
            {
                IsBackground = true,
                Name = $"OFX multiThread worker {index}",
            };
            thread.Start();
            thread.Join();
            return failure;
        }

        /// <summary>
        /// ワーカー本体。専用スレッド上（純粋なマネージド起点スタック）で呼ぶこと。
        /// プラグイン内のハードウェア例外はここのcatchで受け止めて呼び出し側へ返す（伝播はネイティブ1区間だけを跨ぐ安全な形）
        /// </summary>
        static Exception? RunWorker(delegate* unmanaged[Cdecl]<uint, uint, nint, void> function, uint index, uint nThreads, nint customArg)
        {
            try
            {
                isSpawnedThread = true;
                spawnedThreadIndex = index;
                function(index, nThreads, customArg);
                return null;
            }
            catch (Exception e)
            {
                return e;
            }
            finally
            {
                isSpawnedThread = false;
                spawnedThreadIndex = 0;
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
