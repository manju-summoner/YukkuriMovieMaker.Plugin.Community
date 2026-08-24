using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx
{
    /// <summary>
    /// OFXのプロパティセット（型付き・多次元の名前付きプロパティの集合）。
    /// OfxPropertySetHandle の実体。プラグインからは OfxPropertySuiteV1 経由で読み書きされる。
    /// propGetString でプラグインへ返す char* は、このオブジェクトが所有するネイティブバッファに
    /// キャッシュし、値の変更または Dispose まで有効性を保証する。
    /// </summary>
    internal sealed class OfxPropertySet : OfxObject
    {
        enum Kind
        {
            Int,
            Double,
            Pointer,
            String,
        }

        sealed class Entry
        {
            public Kind Kind;
            public readonly List<object> Values = [];
            /// <summary>propReset 用の既定値スナップショット（SealDefaults 時点の値）</summary>
            public object[]? Defaults;
            /// <summary>propGetString 用のネイティブ文字列キャッシュ（index対応）。値変更時に無効化する</summary>
            public List<nint>? NativeStrings;
        }

        readonly object sync = new();
        readonly Dictionary<string, Entry> entries = [];

        /// <summary>デバッグログ用の持ち主の説明（例: "host", "effectDescriptor(net.sf.openfx.Foo)"）</summary>
        public string DebugName { get; set; } = "";

        //====================================================================
        // マネージド側からの設定（初期値の構築・ホスト側の書き込みに使う）
        //====================================================================

        public void SetInt(string name, int value, int index = 0) => Set(name, Kind.Int, index, value);
        public void SetDouble(string name, double value, int index = 0) => Set(name, Kind.Double, index, value);
        public void SetPointer(string name, nint value, int index = 0) => Set(name, Kind.Pointer, index, value);
        public void SetString(string name, string value, int index = 0) => Set(name, Kind.String, index, value);

        public void SetIntN(string name, params int[] values) => SetN(name, Kind.Int, Array.ConvertAll(values, v => (object)v));
        public void SetDoubleN(string name, params double[] values) => SetN(name, Kind.Double, Array.ConvertAll(values, v => (object)v));
        public void SetStringN(string name, params string[] values) => SetN(name, Kind.String, Array.ConvertAll(values, v => (object)v));

        /// <summary>プロパティを空の次元0で定義する（存在はするが値が無い状態）</summary>
        public void SetEmpty(string name, OfxPropertyType type)
        {
            lock (sync)
            {
                var entry = GetOrCreateEntry(name, ToKind(type));
                entry.Values.Clear();
                InvalidateNativeStrings(entry);
            }
        }

        void Set(string name, Kind kind, int index, object value)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            lock (sync)
            {
                var entry = GetOrCreateEntry(name, kind);
                while (entry.Values.Count <= index)
                    entry.Values.Add(DefaultValue(kind));
                entry.Values[index] = value;
                InvalidateNativeStrings(entry);
            }
        }

        void SetN(string name, Kind kind, object[] values)
        {
            lock (sync)
            {
                var entry = GetOrCreateEntry(name, kind);
                entry.Values.Clear();
                entry.Values.AddRange(values);
                InvalidateNativeStrings(entry);
            }
        }

        Entry GetOrCreateEntry(string name, Kind kind)
        {
            if (!entries.TryGetValue(name, out var entry))
            {
                entry = new Entry { Kind = kind };
                entries[name] = entry;
            }
            else if (entry.Kind != kind)
            {
                // 型が変わる上書きは値・既定値を破棄して置き換える
                entry.Kind = kind;
                entry.Values.Clear();
                entry.Defaults = null;
                InvalidateNativeStrings(entry);
            }
            return entry;
        }

        static object DefaultValue(Kind kind) => kind switch
        {
            Kind.Int => 0,
            Kind.Double => 0.0,
            Kind.Pointer => (nint)0,
            Kind.String => "",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        static Kind ToKind(OfxPropertyType type) => type switch
        {
            OfxPropertyType.Int => Kind.Int,
            OfxPropertyType.Double => Kind.Double,
            OfxPropertyType.Pointer => Kind.Pointer,
            OfxPropertyType.String => Kind.String,
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

        //====================================================================
        // マネージド側からの取得（ホストがdescribe結果を読むときに使う）
        //====================================================================

        public int GetIntOrDefault(string name, int defaultValue, int index = 0)
        {
            lock (sync)
            {
                if (!entries.TryGetValue(name, out var entry) || index >= entry.Values.Count)
                    return defaultValue;
                return entry.Values[index] switch
                {
                    int i => i,
                    double d => (int)d,
                    _ => defaultValue,
                };
            }
        }

        public double GetDoubleOrDefault(string name, double defaultValue, int index = 0)
        {
            lock (sync)
            {
                if (!entries.TryGetValue(name, out var entry) || index >= entry.Values.Count)
                    return defaultValue;
                return entry.Values[index] switch
                {
                    double d => d,
                    int i => i,
                    _ => defaultValue,
                };
            }
        }

        public string GetStringOrDefault(string name, string defaultValue, int index = 0)
        {
            lock (sync)
            {
                if (!entries.TryGetValue(name, out var entry) || index >= entry.Values.Count)
                    return defaultValue;
                return entry.Values[index] as string ?? defaultValue;
            }
        }

        public string[] GetStrings(string name)
        {
            lock (sync)
            {
                if (!entries.TryGetValue(name, out var entry))
                    return [];
                var result = new List<string>(entry.Values.Count);
                foreach (var value in entry.Values)
                {
                    if (value is string s)
                        result.Add(s);
                }
                return [.. result];
            }
        }

        public double[] GetDoubles(string name)
        {
            lock (sync)
            {
                if (!entries.TryGetValue(name, out var entry))
                    return [];
                var result = new List<double>(entry.Values.Count);
                foreach (var value in entry.Values)
                {
                    if (value is double d)
                        result.Add(d);
                    else if (value is int i)
                        result.Add(i);
                }
                return [.. result];
            }
        }

        public int GetDimension(string name)
        {
            lock (sync)
            {
                return entries.TryGetValue(name, out var entry) ? entry.Values.Count : 0;
            }
        }

        public bool Contains(string name)
        {
            lock (sync)
            {
                return entries.ContainsKey(name);
            }
        }

        /// <summary>
        /// 現在の値を propReset の復元先（既定値）としてスナップショットする。
        /// ホストが既定プロパティを埋め終えた時点で呼ぶ
        /// </summary>
        public void SealDefaults()
        {
            lock (sync)
            {
                foreach (var entry in entries.Values)
                    entry.Defaults = [.. entry.Values];
            }
        }

        /// <summary>別のプロパティセットの内容をこのセットへ複製する（describeInContext用の派生ディスクリプタ作成などに使う）</summary>
        public void CopyFrom(OfxPropertySet source)
        {
            lock (source.sync)
            {
                lock (sync)
                {
                    foreach (var (name, entry) in source.entries)
                    {
                        var copy = new Entry { Kind = entry.Kind };
                        copy.Values.AddRange(entry.Values);
                        // 既定値スナップショットも複製しないと、複製先への propReset が次元0への消去になる
                        copy.Defaults = entry.Defaults is null ? null : [.. entry.Defaults];
                        // 差し替え前のエントリがネイティブ文字列を確保済みなら解放してから置き換える
                        if (entries.TryGetValue(name, out var existing))
                            InvalidateNativeStrings(existing);
                        entries[name] = copy;
                    }
                }
            }
        }

        //====================================================================
        // ネイティブ側（OfxPropertySuiteV1）からの読み書き
        //====================================================================

        public int NativeSetInt(string name, int index, int value)
        {
            if (index < 0)
                return OfxStatus.ErrBadIndex;
            Set(name, Kind.Int, index, value);
            return OfxStatus.OK;
        }

        public int NativeSetDouble(string name, int index, double value)
        {
            if (index < 0)
                return OfxStatus.ErrBadIndex;
            Set(name, Kind.Double, index, value);
            return OfxStatus.OK;
        }

        public int NativeSetPointer(string name, int index, nint value)
        {
            if (index < 0)
                return OfxStatus.ErrBadIndex;
            Set(name, Kind.Pointer, index, value);
            return OfxStatus.OK;
        }

        public int NativeSetString(string name, int index, string value)
        {
            if (index < 0)
                return OfxStatus.ErrBadIndex;
            Set(name, Kind.String, index, value);
            return OfxStatus.OK;
        }

        public int NativeGetInt(string name, int index, out int value)
        {
            value = 0;
            lock (sync)
            {
                if (!entries.TryGetValue(name, out var entry))
                    return NotFound(name);
                if (index < 0 || index >= entry.Values.Count)
                    return OfxStatus.ErrBadIndex;
                switch (entry.Values[index])
                {
                    case int i:
                        value = i;
                        return OfxStatus.OK;
                    case double d:
                        value = (int)d;
                        return OfxStatus.OK;
                    default:
                        return OfxStatus.ErrValue;
                }
            }
        }

        public int NativeGetDouble(string name, int index, out double value)
        {
            value = 0;
            lock (sync)
            {
                if (!entries.TryGetValue(name, out var entry))
                    return NotFound(name);
                if (index < 0 || index >= entry.Values.Count)
                    return OfxStatus.ErrBadIndex;
                switch (entry.Values[index])
                {
                    case double d:
                        value = d;
                        return OfxStatus.OK;
                    case int i:
                        value = i;
                        return OfxStatus.OK;
                    default:
                        return OfxStatus.ErrValue;
                }
            }
        }

        public int NativeGetPointer(string name, int index, out nint value)
        {
            value = 0;
            lock (sync)
            {
                if (!entries.TryGetValue(name, out var entry))
                    return NotFound(name);
                if (index < 0 || index >= entry.Values.Count)
                    return OfxStatus.ErrBadIndex;
                if (entry.Values[index] is nint p)
                {
                    value = p;
                    return OfxStatus.OK;
                }
                return OfxStatus.ErrValue;
            }
        }

        /// <summary>
        /// propGetString 用。返すポインタはこのプロパティセットが所有し、
        /// 値の変更または Dispose まで有効。
        /// </summary>
        public int NativeGetString(string name, int index, out nint value)
        {
            value = 0;
            lock (sync)
            {
                if (!entries.TryGetValue(name, out var entry))
                    return NotFound(name);
                if (index < 0 || index >= entry.Values.Count)
                    return OfxStatus.ErrBadIndex;
                if (entry.Values[index] is not string s)
                    return OfxStatus.ErrValue;

                entry.NativeStrings ??= [];
                while (entry.NativeStrings.Count <= index)
                    entry.NativeStrings.Add(0);
                if (entry.NativeStrings[index] == 0)
                    entry.NativeStrings[index] = Marshal.StringToCoTaskMemUTF8(s);
                value = entry.NativeStrings[index];
                return OfxStatus.OK;
            }
        }

        public int NativeReset(string name)
        {
            lock (sync)
            {
                if (!entries.TryGetValue(name, out var entry))
                    return NotFound(name);
                // 仕様上「既定値へ戻す」。既定値をスナップショット済み（SealDefaults）ならそれを復元する
                entry.Values.Clear();
                if (entry.Defaults is not null)
                    entry.Values.AddRange(entry.Defaults);
                InvalidateNativeStrings(entry);
                return OfxStatus.OK;
            }
        }

        public int NativeGetDimension(string name, out int count)
        {
            lock (sync)
            {
                if (entries.TryGetValue(name, out var entry))
                {
                    count = entry.Values.Count;
                    return OfxStatus.OK;
                }
                count = 0;
                return NotFound(name);
            }
        }

        int NotFound(string name)
        {
            OfxHostLog.Debug($"プロパティ未定義: {DebugName}.{name}");
            return OfxStatus.ErrUnknown;
        }

        void InvalidateNativeStrings(Entry entry)
        {
            if (entry.NativeStrings is null)
                return;
            foreach (var ptr in entry.NativeStrings)
            {
                if (ptr != 0)
                    Marshal.FreeCoTaskMem(ptr);
            }
            entry.NativeStrings = null;
        }

        public override void Dispose()
        {
            lock (sync)
            {
                foreach (var entry in entries.Values)
                    InvalidateNativeStrings(entry);
                entries.Clear();
            }
            base.Dispose();
        }
    }

    internal enum OfxPropertyType
    {
        Int,
        Double,
        Pointer,
        String,
    }
}
