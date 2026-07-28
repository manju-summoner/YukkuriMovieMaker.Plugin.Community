using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx
{
    /// <summary>
    /// OfxParameterSuiteV1 のホスト実装。
    /// 関数ポインタの並び順は openfx/include/ofxParam.h の構造体定義と一致させること。
    ///
    /// paramGetValue / paramSetValue 系はC APIの可変長引数（...）関数だが、本実装は固定シグネチャで受ける。
    /// これは Windows x64 呼び出し規約の以下の性質に依存している（x64専用。ARM64では成立しない）：
    ///   1. 可変長引数は固定引数と同じ引数スロット（RCX/RDX/R8/R9→スタック）に順番に積まれる
    ///   2. 可変長引数関数の呼び出しでは、浮動小数点引数はXMMレジスタに加えて対応する汎用レジスタにも複製される
    /// このため、可変部を nint スロットとして宣言すれば、ポインタ引数はそのまま、double値は
    /// ビット再解釈（Int64BitsToDouble）で取り出せる。パラメータの次元数・型はハンドルから分かるため、
    /// 実際に使用するスロット数だけを読む（未使用スロットの値は不定だが参照しない）。
    /// </summary>
    internal static unsafe class OfxParameterSuite
    {
        [StructLayout(LayoutKind.Sequential)]
        struct SuiteNative
        {
            public nint paramDefine;
            public nint paramGetHandle;
            public nint paramSetGetPropertySet;
            public nint paramGetPropertySet;
            public nint paramGetValue;
            public nint paramGetValueAtTime;
            public nint paramGetDerivative;
            public nint paramGetIntegral;
            public nint paramSetValue;
            public nint paramSetValueAtTime;
            public nint paramGetNumKeys;
            public nint paramGetKeyTime;
            public nint paramGetKeyIndex;
            public nint paramDeleteKey;
            public nint paramDeleteAllKeys;
            public nint paramCopy;
            public nint paramEditBegin;
            public nint paramEditEnd;
        }

        static readonly string[] knownParamTypes =
        [
            OfxConstants.ParamTypeInteger,
            OfxConstants.ParamTypeDouble,
            OfxConstants.ParamTypeBoolean,
            OfxConstants.ParamTypeChoice,
            OfxConstants.ParamTypeStrChoice,
            OfxConstants.ParamTypeRGBA,
            OfxConstants.ParamTypeRGB,
            OfxConstants.ParamTypeDouble2D,
            OfxConstants.ParamTypeInteger2D,
            OfxConstants.ParamTypeDouble3D,
            OfxConstants.ParamTypeInteger3D,
            OfxConstants.ParamTypeString,
            OfxConstants.ParamTypeCustom,
            OfxConstants.ParamTypeGroup,
            OfxConstants.ParamTypePage,
            OfxConstants.ParamTypePushButton,
        ];

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
                    suite->paramDefine = (nint)(delegate* unmanaged[Cdecl]<nint, byte*, byte*, nint*, int>)&ParamDefine;
                    suite->paramGetHandle = (nint)(delegate* unmanaged[Cdecl]<nint, byte*, nint*, nint*, int>)&ParamGetHandle;
                    suite->paramSetGetPropertySet = (nint)(delegate* unmanaged[Cdecl]<nint, nint*, int>)&ParamSetGetPropertySet;
                    suite->paramGetPropertySet = (nint)(delegate* unmanaged[Cdecl]<nint, nint*, int>)&ParamGetPropertySet;
                    suite->paramGetValue = (nint)(delegate* unmanaged[Cdecl]<nint, nint, nint, nint, nint, int>)&ParamGetValue;
                    suite->paramGetValueAtTime = (nint)(delegate* unmanaged[Cdecl]<nint, double, nint, nint, nint, nint, int>)&ParamGetValueAtTime;
                    suite->paramGetDerivative = (nint)(delegate* unmanaged[Cdecl]<nint, double, nint, nint, nint, nint, int>)&ParamGetDerivative;
                    suite->paramGetIntegral = (nint)(delegate* unmanaged[Cdecl]<nint, double, double, nint, nint, nint, nint, int>)&ParamGetIntegral;
                    suite->paramSetValue = (nint)(delegate* unmanaged[Cdecl]<nint, nint, nint, nint, nint, int>)&ParamSetValue;
                    suite->paramSetValueAtTime = (nint)(delegate* unmanaged[Cdecl]<nint, double, nint, nint, nint, nint, int>)&ParamSetValueAtTime;
                    suite->paramGetNumKeys = (nint)(delegate* unmanaged[Cdecl]<nint, uint*, int>)&ParamGetNumKeys;
                    suite->paramGetKeyTime = (nint)(delegate* unmanaged[Cdecl]<nint, uint, double*, int>)&ParamGetKeyTime;
                    suite->paramGetKeyIndex = (nint)(delegate* unmanaged[Cdecl]<nint, double, int, int*, int>)&ParamGetKeyIndex;
                    suite->paramDeleteKey = (nint)(delegate* unmanaged[Cdecl]<nint, double, int>)&ParamDeleteKey;
                    suite->paramDeleteAllKeys = (nint)(delegate* unmanaged[Cdecl]<nint, int>)&ParamDeleteAllKeys;
                    suite->paramCopy = (nint)(delegate* unmanaged[Cdecl]<nint, nint, double, OfxRangeD*, int>)&ParamCopy;
                    suite->paramEditBegin = (nint)(delegate* unmanaged[Cdecl]<nint, byte*, int>)&ParamEditBegin;
                    suite->paramEditEnd = (nint)(delegate* unmanaged[Cdecl]<nint, int>)&ParamEditEnd;
                    suitePointer = (nint)suite;
                }
                return suitePointer;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int ParamDefine(nint paramSet, byte* paramType, byte* name, nint* propertySet)
        {
            try
            {
                var set = OfxHandleTable.Get<OfxParamSet>(paramSet);
                if (set is null)
                    return OfxStatus.ErrBadHandle;
                var typeName = Marshal.PtrToStringUTF8((nint)paramType);
                var paramName = Marshal.PtrToStringUTF8((nint)name);
                if (string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(paramName))
                    return OfxStatus.ErrValue;
                if (Array.IndexOf(knownParamTypes, typeName) < 0)
                {
                    OfxHostLog.Info($"未対応のパラメータ型: {typeName} ({paramName})");
                    return OfxStatus.ErrUnsupported;
                }
                if (set.Find(paramName) is not null)
                    return OfxStatus.ErrExists;
                var param = set.Define(typeName, paramName);
                if (propertySet is not null)
                    *propertySet = param.Props.Handle;
                return OfxStatus.OK;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"paramDefine で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int ParamGetHandle(nint paramSet, byte* name, nint* param, nint* propertySet)
        {
            try
            {
                var set = OfxHandleTable.Get<OfxParamSet>(paramSet);
                if (set is null)
                    return OfxStatus.ErrBadHandle;
                var paramName = Marshal.PtrToStringUTF8((nint)name);
                if (string.IsNullOrEmpty(paramName))
                    return OfxStatus.ErrValue;
                var found = set.Find(paramName);
                if (found is null)
                    return OfxStatus.ErrUnknown;
                if (param is not null)
                    *param = found.Handle;
                if (propertySet is not null)
                    *propertySet = found.Props.Handle;
                return OfxStatus.OK;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"paramGetHandle で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int ParamSetGetPropertySet(nint paramSet, nint* propHandle)
        {
            try
            {
                if (propHandle is null)
                    return OfxStatus.ErrValue;
                var set = OfxHandleTable.Get<OfxParamSet>(paramSet);
                if (set is null)
                    return OfxStatus.ErrBadHandle;
                *propHandle = set.Props.Handle;
                return OfxStatus.OK;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"paramSetGetPropertySet で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int ParamGetPropertySet(nint param, nint* propHandle)
        {
            try
            {
                if (propHandle is null)
                    return OfxStatus.ErrValue;
                var found = OfxHandleTable.Get<OfxParam>(param);
                if (found is null)
                    return OfxStatus.ErrBadHandle;
                *propHandle = found.Props.Handle;
                return OfxStatus.OK;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"paramGetPropertySet で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        //====================================================================
        // 値の取得・設定（可変長引数のスロット読み）
        //====================================================================

        static int WriteValues(OfxParam param, nint* slots, int slotCount)
        {
            var dimension = param.Dimension;
            if (dimension == 0)
                return OfxStatus.ErrUnsupported;
            if (dimension > slotCount)
                return OfxStatus.Failed;
            if (param.IsStringType)
            {
                if (slots[0] == 0)
                    return OfxStatus.ErrValue;
                *(nint*)slots[0] = param.GetNativeStringValue();
                return OfxStatus.OK;
            }
            if (param.DoubleValues is { } doubles)
            {
                for (var i = 0; i < dimension; i++)
                {
                    if (slots[i] == 0)
                        return OfxStatus.ErrValue;
                    *(double*)slots[i] = doubles[i];
                }
                return OfxStatus.OK;
            }
            if (param.IntValues is { } ints)
            {
                for (var i = 0; i < dimension; i++)
                {
                    if (slots[i] == 0)
                        return OfxStatus.ErrValue;
                    *(int*)slots[i] = ints[i];
                }
                return OfxStatus.OK;
            }
            // インスタンス値が初期化されていない（ディスクリプタに対する呼び出し）
            return OfxStatus.ErrBadHandle;
        }

        static int ReadValues(OfxParam param, nint* slots, int slotCount)
        {
            var dimension = param.Dimension;
            if (dimension == 0)
                return OfxStatus.ErrUnsupported;
            if (dimension > slotCount)
                return OfxStatus.Failed;
            if (param.IsStringType)
            {
                param.StringValue = Marshal.PtrToStringUTF8(slots[0]) ?? "";
                return OfxStatus.OK;
            }
            if (param.DoubleValues is { } doubles)
            {
                // 可変長引数のdouble値は汎用レジスタ側にも複製されているためビット再解釈で取り出す
                for (var i = 0; i < dimension; i++)
                    doubles[i] = BitConverter.Int64BitsToDouble(slots[i]);
                return OfxStatus.OK;
            }
            if (param.IntValues is { } ints)
            {
                for (var i = 0; i < dimension; i++)
                    ints[i] = (int)slots[i];
                return OfxStatus.OK;
            }
            return OfxStatus.ErrBadHandle;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int ParamGetValue(nint param, nint a1, nint a2, nint a3, nint a4)
        {
            try
            {
                var found = OfxHandleTable.Get<OfxParam>(param);
                if (found is null)
                    return OfxStatus.ErrBadHandle;
                var slots = stackalloc nint[4] { a1, a2, a3, a4 };
                return WriteValues(found, slots, 4);
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"paramGetValue で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int ParamGetValueAtTime(nint param, double time, nint a2, nint a3, nint a4, nint a5)
        {
            try
            {
                // アニメーションはYMM4側でフレーム毎に評価して値を反映するため、時刻指定でも現在値を返す
                // （kOfxParamPropAnimates=0 を申告しているため、時刻によらず現在値を返すのは契約に反しない）
                var found = OfxHandleTable.Get<OfxParam>(param);
                if (found is null)
                    return OfxStatus.ErrBadHandle;
                var slots = stackalloc nint[4] { a2, a3, a4, a5 };
                return WriteValues(found, slots, 4);
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"paramGetValueAtTime で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int ParamGetDerivative(nint param, double time, nint a2, nint a3, nint a4, nint a5)
        {
            try
            {
                // ホスト側でキーフレームを保持しないため微分は常に0
                var found = OfxHandleTable.Get<OfxParam>(param);
                if (found is null)
                    return OfxStatus.ErrBadHandle;
                if (!found.IsDoubleType || found.Dimension > 4)
                    return OfxStatus.ErrUnsupported;
                var slots = stackalloc nint[4] { a2, a3, a4, a5 };
                for (var i = 0; i < found.Dimension; i++)
                {
                    if (slots[i] == 0)
                        return OfxStatus.ErrValue;
                    *(double*)slots[i] = 0;
                }
                return OfxStatus.OK;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"paramGetDerivative で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int ParamGetIntegral(nint param, double time1, double time2, nint a3, nint a4, nint a5, nint a6)
        {
            try
            {
                // 値は時間に対して一定として近似（現在値 × 区間長）
                var found = OfxHandleTable.Get<OfxParam>(param);
                if (found is null)
                    return OfxStatus.ErrBadHandle;
                if (!found.IsDoubleType || found.Dimension > 4 || found.DoubleValues is not { } doubles)
                    return OfxStatus.ErrUnsupported;
                var slots = stackalloc nint[4] { a3, a4, a5, a6 };
                for (var i = 0; i < found.Dimension; i++)
                {
                    if (slots[i] == 0)
                        return OfxStatus.ErrValue;
                    *(double*)slots[i] = doubles[i] * (time2 - time1);
                }
                return OfxStatus.OK;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"paramGetIntegral で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int ParamSetValue(nint param, nint a1, nint a2, nint a3, nint a4)
        {
            try
            {
                var found = OfxHandleTable.Get<OfxParam>(param);
                if (found is null)
                    return OfxStatus.ErrBadHandle;
                var slots = stackalloc nint[4] { a1, a2, a3, a4 };
                return ReadValues(found, slots, 4);
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"paramSetValue で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int ParamSetValueAtTime(nint param, double time, nint a2, nint a3, nint a4, nint a5)
        {
            try
            {
                // キーフレームは保持しないため現在値の設定として扱う
                var found = OfxHandleTable.Get<OfxParam>(param);
                if (found is null)
                    return OfxStatus.ErrBadHandle;
                var slots = stackalloc nint[4] { a2, a3, a4, a5 };
                return ReadValues(found, slots, 4);
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"paramSetValueAtTime で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        //====================================================================
        // キーフレーム関連（ホスト側でアニメーションを評価するため、プラグインからは常に「キー無し」に見せる）
        //====================================================================

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int ParamGetNumKeys(nint param, uint* numberOfKeys)
        {
            try
            {
                if (numberOfKeys is null)
                    return OfxStatus.ErrValue;
                if (OfxHandleTable.Get<OfxParam>(param) is null)
                    return OfxStatus.ErrBadHandle;
                *numberOfKeys = 0;
                return OfxStatus.OK;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"paramGetNumKeys で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int ParamGetKeyTime(nint param, uint nthKey, double* time)
        {
            return OfxStatus.ErrBadIndex;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int ParamGetKeyIndex(nint param, double time, int direction, int* index)
        {
            return OfxStatus.Failed;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int ParamDeleteKey(nint param, double time)
        {
            return OfxStatus.OK;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int ParamDeleteAllKeys(nint param)
        {
            return OfxStatus.OK;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int ParamCopy(nint paramTo, nint paramFrom, double dstOffset, OfxRangeD* frameRange)
        {
            try
            {
                var to = OfxHandleTable.Get<OfxParam>(paramTo);
                var from = OfxHandleTable.Get<OfxParam>(paramFrom);
                if (to is null || from is null)
                    return OfxStatus.ErrBadHandle;
                if (to.ParamType != from.ParamType)
                    return OfxStatus.ErrValue;
                if (from.DoubleValues is { } fromDoubles && to.DoubleValues is { } toDoubles)
                    fromDoubles.CopyTo(toDoubles, 0);
                if (from.IntValues is { } fromInts && to.IntValues is { } toInts)
                    fromInts.CopyTo(toInts, 0);
                if (from.StringValue is not null)
                    to.StringValue = from.StringValue;
                return OfxStatus.OK;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"paramCopy で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int ParamEditBegin(nint paramSet, byte* name)
        {
            return OfxStatus.OK;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int ParamEditEnd(nint paramSet)
        {
            return OfxStatus.OK;
        }
    }
}
