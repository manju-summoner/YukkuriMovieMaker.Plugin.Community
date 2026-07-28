using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx
{
    /// <summary>
    /// imageMemoryAlloc で確保するメモリブロック（OfxImageMemoryHandle の実体）
    /// </summary>
    internal sealed unsafe class OfxImageMemory : OfxObject
    {
        void* data;

        public OfxImageMemory(nuint bytes)
        {
            data = NativeMemory.Alloc(bytes);
        }

        public nint Data => (nint)data;

        public override void Dispose()
        {
            if (data is not null)
            {
                NativeMemory.Free(data);
                data = null;
            }
            base.Dispose();
        }
    }

    /// <summary>
    /// OfxImageEffectSuiteV1 のホスト実装。
    /// 関数ポインタの並び順は openfx/include/ofxImageEffect.h の構造体定義と一致させること。
    /// クリップのインスタンス操作（clipGetHandle / clipGetImage 等）はインスタンス実装側
    /// （OfxEffectInstance）が対応する。
    /// </summary>
    internal static unsafe class OfxImageEffectSuite
    {
        [StructLayout(LayoutKind.Sequential)]
        struct SuiteNative
        {
            public nint getPropertySet;
            public nint getParamSet;
            public nint clipDefine;
            public nint clipGetHandle;
            public nint clipGetPropertySet;
            public nint clipGetImage;
            public nint clipReleaseImage;
            public nint clipGetRegionOfDefinition;
            public nint abort;
            public nint imageMemoryAlloc;
            public nint imageMemoryFree;
            public nint imageMemoryLock;
            public nint imageMemoryUnlock;
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
                    suite->getPropertySet = (nint)(delegate* unmanaged[Cdecl]<nint, nint*, int>)&GetPropertySet;
                    suite->getParamSet = (nint)(delegate* unmanaged[Cdecl]<nint, nint*, int>)&GetParamSet;
                    suite->clipDefine = (nint)(delegate* unmanaged[Cdecl]<nint, byte*, nint*, int>)&ClipDefine;
                    suite->clipGetHandle = (nint)(delegate* unmanaged[Cdecl]<nint, byte*, nint*, nint*, int>)&ClipGetHandle;
                    suite->clipGetPropertySet = (nint)(delegate* unmanaged[Cdecl]<nint, nint*, int>)&ClipGetPropertySet;
                    suite->clipGetImage = (nint)(delegate* unmanaged[Cdecl]<nint, double, OfxRectD*, nint*, int>)&ClipGetImage;
                    suite->clipReleaseImage = (nint)(delegate* unmanaged[Cdecl]<nint, int>)&ClipReleaseImage;
                    suite->clipGetRegionOfDefinition = (nint)(delegate* unmanaged[Cdecl]<nint, double, OfxRectD*, int>)&ClipGetRegionOfDefinition;
                    suite->abort = (nint)(delegate* unmanaged[Cdecl]<nint, int>)&Abort;
                    suite->imageMemoryAlloc = (nint)(delegate* unmanaged[Cdecl]<nint, nuint, nint*, int>)&ImageMemoryAlloc;
                    suite->imageMemoryFree = (nint)(delegate* unmanaged[Cdecl]<nint, int>)&ImageMemoryFree;
                    suite->imageMemoryLock = (nint)(delegate* unmanaged[Cdecl]<nint, nint*, int>)&ImageMemoryLock;
                    suite->imageMemoryUnlock = (nint)(delegate* unmanaged[Cdecl]<nint, int>)&ImageMemoryUnlock;
                    suitePointer = (nint)suite;
                }
                return suitePointer;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int GetPropertySet(nint imageEffect, nint* propHandle)
        {
            try
            {
                if (propHandle is null)
                    return OfxStatus.ErrValue;
                if (OfxHandleTable.Get<IOfxImageEffectObject>(imageEffect) is { } effect)
                {
                    *propHandle = effect.Props.Handle;
                    return OfxStatus.OK;
                }
                return OfxStatus.ErrBadHandle;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"getPropertySet で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int GetParamSet(nint imageEffect, nint* paramSet)
        {
            try
            {
                if (paramSet is null)
                    return OfxStatus.ErrValue;
                if (OfxHandleTable.Get<IOfxImageEffectObject>(imageEffect) is { } effect)
                {
                    *paramSet = effect.ParamSet.Handle;
                    return OfxStatus.OK;
                }
                return OfxStatus.ErrBadHandle;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"getParamSet で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int ClipDefine(nint imageEffect, byte* name, nint* propertySet)
        {
            try
            {
                var descriptor = OfxHandleTable.Get<OfxEffectDescriptor>(imageEffect);
                if (descriptor is null)
                    return OfxStatus.ErrBadHandle;
                var clipName = Marshal.PtrToStringUTF8((nint)name);
                if (string.IsNullOrEmpty(clipName))
                    return OfxStatus.ErrValue;
                var clip = descriptor.DefineClip(clipName);
                if (propertySet is not null)
                    *propertySet = clip.Props.Handle;
                return OfxStatus.OK;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"clipDefine で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int ClipGetHandle(nint imageEffect, byte* name, nint* clip, nint* propertySet)
        {
            try
            {
                var instance = OfxHandleTable.Get<OfxEffectInstance>(imageEffect);
                if (instance is null)
                    return OfxStatus.ErrBadHandle;
                var clipName = Marshal.PtrToStringUTF8((nint)name);
                if (string.IsNullOrEmpty(clipName))
                    return OfxStatus.ErrValue;
                var found = instance.FindClip(clipName);
                if (found is null)
                    return OfxStatus.ErrUnknown;
                if (clip is not null)
                    *clip = found.Handle;
                if (propertySet is not null)
                    *propertySet = found.Props.Handle;
                return OfxStatus.OK;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"clipGetHandle で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int ClipGetPropertySet(nint clip, nint* propHandle)
        {
            try
            {
                if (propHandle is null)
                    return OfxStatus.ErrValue;
                var found = OfxHandleTable.Get<OfxClipInstance>(clip);
                if (found is null)
                    return OfxStatus.ErrBadHandle;
                *propHandle = found.Props.Handle;
                return OfxStatus.OK;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"clipGetPropertySet で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int ClipGetImage(nint clip, double time, OfxRectD* region, nint* imageHandle)
        {
            try
            {
                if (imageHandle is null)
                    return OfxStatus.ErrValue;
                // ハンドルのNULL判定だけを行うプラグインが未初期化値を読まないよう、失敗時も出力を初期化する
                *imageHandle = 0;
                var found = OfxHandleTable.Get<OfxClipInstance>(clip);
                if (found is null)
                    return OfxStatus.ErrBadHandle;
                // フレーム毎にホストが CurrentImage へ差し込んだ画像のみを供給する
                // （時間・領域指定による別フレームの取得はテンポラルアクセス非対応のため行わない）
                if (found.CurrentImage is not { } image)
                {
                    OfxHostLog.Debug($"clipGetImage: クリップ {found.Name} に画像が供給されていません");
                    return OfxStatus.Failed;
                }
                // 別フレームを要求された場合、現在フレームを返すと黙って誤った出力になるため失敗させる
                if (Math.Abs(time - found.CurrentTime) > 0.5)
                {
                    OfxHostLog.Info($"clipGetImage: テンポラルアクセスは未対応です。clip={found.Name} 要求時刻={time} 現在={found.CurrentTime}");
                    return OfxStatus.Failed;
                }
                *imageHandle = image.Props.Handle;
                return OfxStatus.OK;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"clipGetImage で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int ClipReleaseImage(nint imageHandle)
        {
            try
            {
                // 画像の寿命はホスト（レンダリング1回分）が管理するため、ハンドル検証のみ行う
                return OfxHandleTable.Get<OfxPropertySet>(imageHandle) is null
                    ? OfxStatus.ErrBadHandle
                    : OfxStatus.OK;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"clipReleaseImage で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int ClipGetRegionOfDefinition(nint clip, double time, OfxRectD* bounds)
        {
            try
            {
                if (bounds is null)
                    return OfxStatus.ErrValue;
                var found = OfxHandleTable.Get<OfxClipInstance>(clip);
                if (found is null)
                    return OfxStatus.ErrBadHandle;
                // レンダリング中に画像が供給されているクリップ（RoD拡張された出力等）はその矩形を返す
                if (found.CurrentImage is { } image)
                {
                    bounds->x1 = image.OffsetX;
                    bounds->y1 = image.OffsetY;
                    bounds->x2 = image.OffsetX + image.Width;
                    bounds->y2 = image.OffsetY + image.Height;
                    return OfxStatus.OK;
                }
                bounds->x1 = 0;
                bounds->y1 = 0;
                bounds->x2 = found.Width;
                bounds->y2 = found.Height;
                return OfxStatus.OK;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"clipGetRegionOfDefinition で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int Abort(nint imageEffect)
        {
            // 中断要求は現状無し（プレビュー中断連携は将来の拡張点）
            return 0;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int ImageMemoryAlloc(nint instanceHandle, nuint nBytes, nint* memoryHandle)
        {
            try
            {
                if (memoryHandle is null)
                    return OfxStatus.ErrValue;
                var memory = new OfxImageMemory(nBytes);
                *memoryHandle = memory.Handle;
                return OfxStatus.OK;
            }
            catch (OutOfMemoryException)
            {
                return OfxStatus.ErrMemory;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"imageMemoryAlloc で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int ImageMemoryFree(nint memoryHandle)
        {
            try
            {
                var memory = OfxHandleTable.Get<OfxImageMemory>(memoryHandle);
                if (memory is null)
                    return OfxStatus.ErrBadHandle;
                memory.Dispose();
                return OfxStatus.OK;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"imageMemoryFree で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int ImageMemoryLock(nint memoryHandle, nint* returnedPtr)
        {
            try
            {
                if (returnedPtr is null)
                    return OfxStatus.ErrValue;
                var memory = OfxHandleTable.Get<OfxImageMemory>(memoryHandle);
                if (memory is null)
                    return OfxStatus.ErrBadHandle;
                *returnedPtr = memory.Data;
                return OfxStatus.OK;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"imageMemoryLock で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int ImageMemoryUnlock(nint memoryHandle)
        {
            try
            {
                return OfxHandleTable.Get<OfxImageMemory>(memoryHandle) is null
                    ? OfxStatus.ErrBadHandle
                    : OfxStatus.OK;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"imageMemoryUnlock で例外: {ex}");
                return OfxStatus.Failed;
            }
        }
    }
}
