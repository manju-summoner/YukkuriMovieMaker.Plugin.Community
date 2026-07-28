using System;
using System.Collections.Concurrent;
using System.Threading;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx
{
    /// <summary>
    /// ホスト側オブジェクトとネイティブハンドル（不透明ポインタ）の対応表。
    /// GCHandle ではなく連番キーを使うことで、プラグインが不正なハンドルを渡しても
    /// クラッシュせず kOfxStatErrBadHandle を返せるようにする。
    /// </summary>
    internal static class OfxHandleTable
    {
        static readonly ConcurrentDictionary<nint, object> objects = new();
        static long nextHandle = 0x0FF00000;    // 0（NULL）や小さい値との衝突を避けるための開始値

        public static nint Allocate(object obj)
        {
            var handle = (nint)Interlocked.Increment(ref nextHandle);
            objects[handle] = obj;
            return handle;
        }

        public static void Free(nint handle)
        {
            objects.TryRemove(handle, out _);
        }

        public static T? Get<T>(nint handle) where T : class
        {
            return objects.TryGetValue(handle, out var obj) ? obj as T : null;
        }
    }

    /// <summary>
    /// ネイティブハンドルを持つホスト側オブジェクトの基底。
    /// ハンドルは初回参照時に確保し、Dispose で対応表から除去する。
    /// </summary>
    internal abstract class OfxObject : IDisposable
    {
        nint handle;

        public nint Handle
        {
            get
            {
                // multiThreadで生成したワーカースレッドからも参照されるため、初回確保はCASで競合を解決する
                var current = Interlocked.CompareExchange(ref handle, 0, 0);
                if (current != 0)
                    return current;
                var allocated = OfxHandleTable.Allocate(this);
                var winner = Interlocked.CompareExchange(ref handle, allocated, 0);
                if (winner != 0)
                {
                    OfxHandleTable.Free(allocated);
                    return winner;
                }
                return allocated;
            }
        }

        public virtual void Dispose()
        {
            var current = Interlocked.Exchange(ref handle, 0);
            if (current != 0)
                OfxHandleTable.Free(current);
        }
    }
}
