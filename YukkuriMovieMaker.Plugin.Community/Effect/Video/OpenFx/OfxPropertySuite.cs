using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx
{
    /// <summary>
    /// OfxPropertySuiteV1 のホスト実装。
    /// 関数ポインタの並び順は openfx/include/ofxProperty.h の構造体定義と一致させること。
    /// </summary>
    internal static unsafe class OfxPropertySuite
    {
        [StructLayout(LayoutKind.Sequential)]
        struct SuiteNative
        {
            public nint propSetPointer;
            public nint propSetString;
            public nint propSetDouble;
            public nint propSetInt;
            public nint propSetPointerN;
            public nint propSetStringN;
            public nint propSetDoubleN;
            public nint propSetIntN;
            public nint propGetPointer;
            public nint propGetString;
            public nint propGetDouble;
            public nint propGetInt;
            public nint propGetPointerN;
            public nint propGetStringN;
            public nint propGetDoubleN;
            public nint propGetIntN;
            public nint propReset;
            public nint propGetDimension;
        }

        static readonly object initSync = new();
        static nint suitePointer;

        /// <summary>スイート構造体のネイティブポインタ（プロセス生存中は有効）</summary>
        public static nint Pointer
        {
            get
            {
                lock (initSync)
                {
                    if (suitePointer != 0)
                        return suitePointer;
                    var suite = (SuiteNative*)NativeMemory.AllocZeroed((nuint)sizeof(SuiteNative));
                    suite->propSetPointer = (nint)(delegate* unmanaged[Cdecl]<nint, byte*, int, nint, int>)&PropSetPointer;
                    suite->propSetString = (nint)(delegate* unmanaged[Cdecl]<nint, byte*, int, byte*, int>)&PropSetString;
                    suite->propSetDouble = (nint)(delegate* unmanaged[Cdecl]<nint, byte*, int, double, int>)&PropSetDouble;
                    suite->propSetInt = (nint)(delegate* unmanaged[Cdecl]<nint, byte*, int, int, int>)&PropSetInt;
                    suite->propSetPointerN = (nint)(delegate* unmanaged[Cdecl]<nint, byte*, int, nint*, int>)&PropSetPointerN;
                    suite->propSetStringN = (nint)(delegate* unmanaged[Cdecl]<nint, byte*, int, nint*, int>)&PropSetStringN;
                    suite->propSetDoubleN = (nint)(delegate* unmanaged[Cdecl]<nint, byte*, int, double*, int>)&PropSetDoubleN;
                    suite->propSetIntN = (nint)(delegate* unmanaged[Cdecl]<nint, byte*, int, int*, int>)&PropSetIntN;
                    suite->propGetPointer = (nint)(delegate* unmanaged[Cdecl]<nint, byte*, int, nint*, int>)&PropGetPointer;
                    suite->propGetString = (nint)(delegate* unmanaged[Cdecl]<nint, byte*, int, nint*, int>)&PropGetString;
                    suite->propGetDouble = (nint)(delegate* unmanaged[Cdecl]<nint, byte*, int, double*, int>)&PropGetDouble;
                    suite->propGetInt = (nint)(delegate* unmanaged[Cdecl]<nint, byte*, int, int*, int>)&PropGetInt;
                    suite->propGetPointerN = (nint)(delegate* unmanaged[Cdecl]<nint, byte*, int, nint*, int>)&PropGetPointerN;
                    suite->propGetStringN = (nint)(delegate* unmanaged[Cdecl]<nint, byte*, int, nint*, int>)&PropGetStringN;
                    suite->propGetDoubleN = (nint)(delegate* unmanaged[Cdecl]<nint, byte*, int, double*, int>)&PropGetDoubleN;
                    suite->propGetIntN = (nint)(delegate* unmanaged[Cdecl]<nint, byte*, int, int*, int>)&PropGetIntN;
                    suite->propReset = (nint)(delegate* unmanaged[Cdecl]<nint, byte*, int>)&PropReset;
                    suite->propGetDimension = (nint)(delegate* unmanaged[Cdecl]<nint, byte*, int*, int>)&PropGetDimension;
                    suitePointer = (nint)suite;
                }
                return suitePointer;
            }
        }

        static bool TryResolve(nint properties, byte* property, out OfxPropertySet set, out string name, out int status)
        {
            set = null!;
            name = "";
            var resolved = OfxHandleTable.Get<OfxPropertySet>(properties);
            if (resolved is null)
            {
                status = OfxStatus.ErrBadHandle;
                return false;
            }
            var propertyName = Marshal.PtrToStringUTF8((nint)property);
            if (string.IsNullOrEmpty(propertyName))
            {
                status = OfxStatus.ErrValue;
                return false;
            }
            set = resolved;
            name = propertyName;
            status = OfxStatus.OK;
            return true;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int PropSetPointer(nint properties, byte* property, int index, nint value)
        {
            try
            {
                if (!TryResolve(properties, property, out var set, out var name, out var status))
                    return status;
                return set.NativeSetPointer(name, index, value);
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"propSetPointer で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int PropSetString(nint properties, byte* property, int index, byte* value)
        {
            try
            {
                if (!TryResolve(properties, property, out var set, out var name, out var status))
                    return status;
                return set.NativeSetString(name, index, Marshal.PtrToStringUTF8((nint)value) ?? "");
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"propSetString で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int PropSetDouble(nint properties, byte* property, int index, double value)
        {
            try
            {
                if (!TryResolve(properties, property, out var set, out var name, out var status))
                    return status;
                return set.NativeSetDouble(name, index, value);
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"propSetDouble で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int PropSetInt(nint properties, byte* property, int index, int value)
        {
            try
            {
                if (!TryResolve(properties, property, out var set, out var name, out var status))
                    return status;
                return set.NativeSetInt(name, index, value);
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"propSetInt で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int PropSetPointerN(nint properties, byte* property, int count, nint* values)
        {
            try
            {
                if (!TryResolve(properties, property, out var set, out var name, out var status))
                    return status;
                if (count < 0 || (count > 0 && values is null))
                    return OfxStatus.ErrValue;
                // N系のsetは次元をちょうどcountに置き換える（既存の末尾要素を残さない）
                set.SetEmpty(name, OfxPropertyType.Pointer);
                for (var i = 0; i < count; i++)
                {
                    var result = set.NativeSetPointer(name, i, values[i]);
                    if (result != OfxStatus.OK)
                        return result;
                }
                return OfxStatus.OK;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"propSetPointerN で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int PropSetStringN(nint properties, byte* property, int count, nint* values)
        {
            try
            {
                if (!TryResolve(properties, property, out var set, out var name, out var status))
                    return status;
                if (count < 0 || (count > 0 && values is null))
                    return OfxStatus.ErrValue;
                // N系のsetは次元をちょうどcountに置き換える（既存の末尾要素を残さない）
                set.SetEmpty(name, OfxPropertyType.String);
                for (var i = 0; i < count; i++)
                {
                    var result = set.NativeSetString(name, i, Marshal.PtrToStringUTF8(values[i]) ?? "");
                    if (result != OfxStatus.OK)
                        return result;
                }
                return OfxStatus.OK;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"propSetStringN で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int PropSetDoubleN(nint properties, byte* property, int count, double* values)
        {
            try
            {
                if (!TryResolve(properties, property, out var set, out var name, out var status))
                    return status;
                if (count < 0 || (count > 0 && values is null))
                    return OfxStatus.ErrValue;
                // N系のsetは次元をちょうどcountに置き換える（既存の末尾要素を残さない）
                set.SetEmpty(name, OfxPropertyType.Double);
                for (var i = 0; i < count; i++)
                {
                    var result = set.NativeSetDouble(name, i, values[i]);
                    if (result != OfxStatus.OK)
                        return result;
                }
                return OfxStatus.OK;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"propSetDoubleN で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int PropSetIntN(nint properties, byte* property, int count, int* values)
        {
            try
            {
                if (!TryResolve(properties, property, out var set, out var name, out var status))
                    return status;
                if (count < 0 || (count > 0 && values is null))
                    return OfxStatus.ErrValue;
                // N系のsetは次元をちょうどcountに置き換える（既存の末尾要素を残さない）
                set.SetEmpty(name, OfxPropertyType.Int);
                for (var i = 0; i < count; i++)
                {
                    var result = set.NativeSetInt(name, i, values[i]);
                    if (result != OfxStatus.OK)
                        return result;
                }
                return OfxStatus.OK;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"propSetIntN で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int PropGetPointer(nint properties, byte* property, int index, nint* value)
        {
            try
            {
                if (!TryResolve(properties, property, out var set, out var name, out var status))
                    return status;
                if (value is null)
                    return OfxStatus.ErrValue;
                var result = set.NativeGetPointer(name, index, out var v);
                if (result == OfxStatus.OK)
                    *value = v;
                return result;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"propGetPointer で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int PropGetString(nint properties, byte* property, int index, nint* value)
        {
            try
            {
                if (!TryResolve(properties, property, out var set, out var name, out var status))
                    return status;
                if (value is null)
                    return OfxStatus.ErrValue;
                var result = set.NativeGetString(name, index, out var v);
                if (result == OfxStatus.OK)
                    *value = v;
                return result;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"propGetString で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int PropGetDouble(nint properties, byte* property, int index, double* value)
        {
            try
            {
                if (!TryResolve(properties, property, out var set, out var name, out var status))
                    return status;
                if (value is null)
                    return OfxStatus.ErrValue;
                var result = set.NativeGetDouble(name, index, out var v);
                if (result == OfxStatus.OK)
                    *value = v;
                return result;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"propGetDouble で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int PropGetInt(nint properties, byte* property, int index, int* value)
        {
            try
            {
                if (!TryResolve(properties, property, out var set, out var name, out var status))
                    return status;
                if (value is null)
                    return OfxStatus.ErrValue;
                var result = set.NativeGetInt(name, index, out var v);
                if (result == OfxStatus.OK)
                    *value = v;
                return result;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"propGetInt で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int PropGetPointerN(nint properties, byte* property, int count, nint* values)
        {
            try
            {
                if (!TryResolve(properties, property, out var set, out var name, out var status))
                    return status;
                if (count < 0 || (count > 0 && values is null))
                    return OfxStatus.ErrValue;
                for (var i = 0; i < count; i++)
                {
                    var result = set.NativeGetPointer(name, i, out var v);
                    if (result != OfxStatus.OK)
                        return result;
                    values[i] = v;
                }
                return OfxStatus.OK;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"propGetPointerN で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int PropGetStringN(nint properties, byte* property, int count, nint* values)
        {
            try
            {
                if (!TryResolve(properties, property, out var set, out var name, out var status))
                    return status;
                if (count < 0 || (count > 0 && values is null))
                    return OfxStatus.ErrValue;
                for (var i = 0; i < count; i++)
                {
                    var result = set.NativeGetString(name, i, out var v);
                    if (result != OfxStatus.OK)
                        return result;
                    values[i] = v;
                }
                return OfxStatus.OK;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"propGetStringN で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int PropGetDoubleN(nint properties, byte* property, int count, double* values)
        {
            try
            {
                if (!TryResolve(properties, property, out var set, out var name, out var status))
                    return status;
                if (count < 0 || (count > 0 && values is null))
                    return OfxStatus.ErrValue;
                for (var i = 0; i < count; i++)
                {
                    var result = set.NativeGetDouble(name, i, out var v);
                    if (result != OfxStatus.OK)
                        return result;
                    values[i] = v;
                }
                return OfxStatus.OK;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"propGetDoubleN で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int PropGetIntN(nint properties, byte* property, int count, int* values)
        {
            try
            {
                if (!TryResolve(properties, property, out var set, out var name, out var status))
                    return status;
                if (count < 0 || (count > 0 && values is null))
                    return OfxStatus.ErrValue;
                for (var i = 0; i < count; i++)
                {
                    var result = set.NativeGetInt(name, i, out var v);
                    if (result != OfxStatus.OK)
                        return result;
                    values[i] = v;
                }
                return OfxStatus.OK;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"propGetIntN で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int PropReset(nint properties, byte* property)
        {
            try
            {
                if (!TryResolve(properties, property, out var set, out var name, out var status))
                    return status;
                return set.NativeReset(name);
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"propReset で例外: {ex}");
                return OfxStatus.Failed;
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static int PropGetDimension(nint properties, byte* property, int* count)
        {
            try
            {
                if (!TryResolve(properties, property, out var set, out var name, out var status))
                    return status;
                if (count is null)
                    return OfxStatus.ErrValue;
                var result = set.NativeGetDimension(name, out var v);
                *count = v;
                return result;
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"propGetDimension で例外: {ex}");
                return OfxStatus.Failed;
            }
        }
    }
}
