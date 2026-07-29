using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx
{
    /// <summary>
    /// OFXパラメータ（OfxParamHandle の実体）。
    /// describe時はディスクリプタとして定義のみを持ち、インスタンス化時に
    /// <see cref="EnsureInstanceValues"/> で値ストアを初期化して paramGetValue / paramSetValue に応える。
    /// </summary>
    internal sealed class OfxParam : OfxObject
    {
        public string Name { get; }
        public string ParamType { get; }
        public OfxPropertySet Props { get; }

        // インスタンス値（ディスクリプタの間は null）
        public double[]? DoubleValues { get; private set; }
        public int[]? IntValues { get; private set; }
        public string? StringValue { get; set; }
        nint nativeStringCache;

        /// <summary>
        /// プラグインが paramSetValue / paramSetValueAtTime / paramCopy で値を書き換えたときの通知先
        /// （エフェクトインスタンスが設定する）。GetClipPreferences のスレーブパラメータ変更を
        /// プラグイン起点の変更でも検知するために使う。
        /// マルチスレッドスイートのワーカースレッドから呼ばれうるため、通知先はスレッドセーフにすること
        /// </summary>
        public Action<OfxParam>? PluginValueSet { get; set; }

        /// <summary>
        /// <see cref="PluginValueSet"/> を例外を漏らさず呼ぶ。
        /// 値の書き込み自体は完了しているため、通知の失敗でスイート関数のステータスを失敗にしない
        /// </summary>
        public void NotifyPluginValueSet()
        {
            try
            {
                PluginValueSet?.Invoke(this);
            }
            catch (Exception ex)
            {
                OfxHostLog.Info($"PluginValueSet の通知で例外: {ex}");
            }
        }

        public OfxParam(string name, string paramType)
        {
            Name = name;
            ParamType = paramType;
            Props = new OfxPropertySet { DebugName = $"param({name})" };
            FillDefaultProperties();
            Props.SealDefaults();
        }

        /// <summary>値の次元数（値を持たない型は0）</summary>
        public int Dimension => ParamType switch
        {
            OfxConstants.ParamTypeInteger or OfxConstants.ParamTypeDouble or OfxConstants.ParamTypeBoolean
                or OfxConstants.ParamTypeChoice => 1,
            OfxConstants.ParamTypeDouble2D or OfxConstants.ParamTypeInteger2D => 2,
            OfxConstants.ParamTypeDouble3D or OfxConstants.ParamTypeInteger3D or OfxConstants.ParamTypeRGB => 3,
            OfxConstants.ParamTypeRGBA => 4,
            OfxConstants.ParamTypeString or OfxConstants.ParamTypeCustom or OfxConstants.ParamTypeStrChoice => 1,
            _ => 0,
        };

        public bool IsDoubleType => ParamType
            is OfxConstants.ParamTypeDouble
            or OfxConstants.ParamTypeDouble2D
            or OfxConstants.ParamTypeDouble3D
            or OfxConstants.ParamTypeRGB
            or OfxConstants.ParamTypeRGBA;

        public bool IsIntType => ParamType
            is OfxConstants.ParamTypeInteger
            or OfxConstants.ParamTypeInteger2D
            or OfxConstants.ParamTypeInteger3D
            or OfxConstants.ParamTypeBoolean
            or OfxConstants.ParamTypeChoice;

        public bool IsStringType => ParamType
            is OfxConstants.ParamTypeString
            or OfxConstants.ParamTypeCustom
            or OfxConstants.ParamTypeStrChoice;

        public bool HasInstanceValues => DoubleValues is not null || IntValues is not null || StringValue is not null;

        /// <summary>
        /// 型ごとの既定プロパティを規格の初期値で埋める。
        /// openfx-misc等が使うOFX C++ Supportライブラリはdescribe時にプロパティの存在検証を行うため、
        /// 主要プロパティは読み出しに応えられるよう事前定義しておく必要がある。
        /// </summary>
        void FillDefaultProperties()
        {
            Props.SetString(OfxConstants.PropType, OfxConstants.TypeParameter);
            Props.SetString(OfxConstants.PropName, Name);
            Props.SetString(OfxConstants.PropLabel, Name);
            Props.SetString(OfxConstants.PropShortLabel, Name);
            Props.SetString(OfxConstants.PropLongLabel, Name);
            Props.SetString(OfxConstants.ParamPropType, ParamType);
            Props.SetInt(OfxConstants.ParamPropSecret, 0);
            Props.SetInt(OfxConstants.ParamPropCanUndo, 1);
            Props.SetString(OfxConstants.ParamPropHint, "");
            Props.SetString(OfxConstants.ParamPropScriptName, Name);
            Props.SetString(OfxConstants.ParamPropParent, "");
            Props.SetInt(OfxConstants.ParamPropEnabled, 1);
            Props.SetPointer(OfxConstants.ParamPropDataPtr, 0);

            var dimension = Dimension;
            var hasValue = dimension > 0;
            if (hasValue)
            {
                Props.SetInt(OfxConstants.ParamPropPersistant, 1);
                Props.SetInt(OfxConstants.ParamPropEvaluateOnChange, 1);
                Props.SetInt(OfxConstants.ParamPropIsAnimating, 0);
                Props.SetInt(OfxConstants.ParamPropIsAutoKeying, 0);
                Props.SetString(OfxConstants.ParamPropCacheInvalidation, OfxConstants.ParamInvalidateValueChange);
                // 本ホストのアニメーションはYMM4側でフレーム毎に評価して値を反映する方式で、
                // paramGetValueAtTime が要求時刻の値を返せない（常に現在値）。アニメーション対応と
                // 宣言すると時刻指定取得の契約を満たせないため、OFX側には非対応と申告する
                Props.SetInt(OfxConstants.ParamPropAnimates, 0);
            }

            if (IsDoubleType)
            {
                for (var i = 0; i < dimension; i++)
                {
                    Props.SetDouble(OfxConstants.ParamPropDefault, 0.0, i);
                    Props.SetDouble(OfxConstants.ParamPropMin, double.MinValue, i);
                    Props.SetDouble(OfxConstants.ParamPropMax, double.MaxValue, i);
                    Props.SetDouble(OfxConstants.ParamPropDisplayMin, double.MinValue, i);
                    Props.SetDouble(OfxConstants.ParamPropDisplayMax, double.MaxValue, i);
                }
                Props.SetDouble(OfxConstants.ParamPropIncrement, 1.0);
                Props.SetInt(OfxConstants.ParamPropDigits, 2);
                if (ParamType is OfxConstants.ParamTypeDouble or OfxConstants.ParamTypeDouble2D or OfxConstants.ParamTypeDouble3D)
                {
                    Props.SetString(OfxConstants.ParamPropDoubleType, OfxConstants.ParamDoubleTypePlain);
                    Props.SetString(OfxConstants.ParamPropDefaultCoordinateSystem, OfxConstants.ParamCoordinatesCanonical);
                }
                if (ParamType == OfxConstants.ParamTypeDouble)
                    Props.SetInt(OfxConstants.ParamPropShowTimeMarker, 0);
                switch (ParamType)
                {
                    case OfxConstants.ParamTypeDouble2D:
                        Props.SetStringN(OfxConstants.ParamPropDimensionLabel, "x", "y");
                        break;
                    case OfxConstants.ParamTypeDouble3D:
                        Props.SetStringN(OfxConstants.ParamPropDimensionLabel, "x", "y", "z");
                        break;
                    case OfxConstants.ParamTypeRGB:
                        Props.SetStringN(OfxConstants.ParamPropDimensionLabel, "r", "g", "b");
                        break;
                    case OfxConstants.ParamTypeRGBA:
                        Props.SetStringN(OfxConstants.ParamPropDimensionLabel, "r", "g", "b", "a");
                        break;
                }
            }
            else if (IsIntType)
            {
                for (var i = 0; i < dimension; i++)
                {
                    Props.SetInt(OfxConstants.ParamPropDefault, 0, i);
                    if (ParamType is not OfxConstants.ParamTypeBoolean and not OfxConstants.ParamTypeChoice)
                    {
                        Props.SetInt(OfxConstants.ParamPropMin, int.MinValue, i);
                        Props.SetInt(OfxConstants.ParamPropMax, int.MaxValue, i);
                        Props.SetInt(OfxConstants.ParamPropDisplayMin, int.MinValue, i);
                        Props.SetInt(OfxConstants.ParamPropDisplayMax, int.MaxValue, i);
                    }
                }
                switch (ParamType)
                {
                    case OfxConstants.ParamTypeInteger2D:
                        Props.SetStringN(OfxConstants.ParamPropDimensionLabel, "x", "y");
                        break;
                    case OfxConstants.ParamTypeInteger3D:
                        Props.SetStringN(OfxConstants.ParamPropDimensionLabel, "x", "y", "z");
                        break;
                    case OfxConstants.ParamTypeChoice:
                        Props.SetEmpty(OfxConstants.ParamPropChoiceOption, OfxPropertyType.String);
                        break;
                }
            }
            else if (IsStringType)
            {
                Props.SetString(OfxConstants.ParamPropDefault, "");
                if (ParamType == OfxConstants.ParamTypeString)
                {
                    Props.SetString(OfxConstants.ParamPropStringMode, OfxConstants.ParamStringIsSingleLine);
                    Props.SetInt(OfxConstants.ParamPropStringFilePathExists, 1);
                }
                if (ParamType == OfxConstants.ParamTypeStrChoice)
                {
                    Props.SetEmpty(OfxConstants.ParamPropChoiceOption, OfxPropertyType.String);
                    Props.SetEmpty(OfxConstants.ParamPropChoiceEnum, OfxPropertyType.String);
                }
            }
            else if (ParamType == OfxConstants.ParamTypeGroup)
            {
                Props.SetInt(OfxConstants.ParamPropGroupOpen, 1);
            }
            else if (ParamType == OfxConstants.ParamTypePage)
            {
                Props.SetEmpty(OfxConstants.ParamPropPageChild, OfxPropertyType.String);
            }
        }

        /// <summary>
        /// インスタンス値ストアを kOfxParamPropDefault から初期化する（インスタンス化時に呼ぶ）
        /// </summary>
        public void EnsureInstanceValues()
        {
            if (HasInstanceValues || Dimension == 0)
                return;
            if (IsDoubleType)
            {
                DoubleValues = new double[Dimension];
                for (var i = 0; i < Dimension; i++)
                    DoubleValues[i] = Props.GetDoubleOrDefault(OfxConstants.ParamPropDefault, 0, i);
            }
            else if (IsIntType)
            {
                IntValues = new int[Dimension];
                for (var i = 0; i < Dimension; i++)
                    IntValues[i] = Props.GetIntOrDefault(OfxConstants.ParamPropDefault, 0, i);
            }
            else if (IsStringType)
            {
                StringValue = Props.GetStringOrDefault(OfxConstants.ParamPropDefault, "");
            }
        }

        /// <summary>
        /// paramGetValue で文字列値を返すためのネイティブバッファ。
        /// 値が変わるまでは同じバッファを返し、前回返したポインタの有効性を保つ
        /// （呼び出しのたびに解放すると、連続したparamGetValueで前回のchar*がダングリングになる）。
        /// 値の変更または Dispose まで有効。
        /// </summary>
        public nint GetNativeStringValue()
        {
            // multiThreadのワーカースレッドからも呼ばれうるため、キャッシュの入れ替えを直列化する
            lock (nativeStringSync)
            {
                var value = StringValue ?? "";
                if (nativeStringCache != 0 && string.Equals(nativeStringCacheValue, value, StringComparison.Ordinal))
                    return nativeStringCache;
                if (nativeStringCache != 0)
                    Marshal.FreeCoTaskMem(nativeStringCache);
                nativeStringCache = Marshal.StringToCoTaskMemUTF8(value);
                nativeStringCacheValue = value;
                return nativeStringCache;
            }
        }
        string? nativeStringCacheValue;
        readonly object nativeStringSync = new();

        public override void Dispose()
        {
            lock (nativeStringSync)
            {
                if (nativeStringCache != 0)
                {
                    Marshal.FreeCoTaskMem(nativeStringCache);
                    nativeStringCache = 0;
                    nativeStringCacheValue = null;
                }
            }
            Props.Dispose();
            base.Dispose();
        }
    }

    /// <summary>
    /// OFXパラメータ集合（OfxParamSetHandle の実体）。定義順を保持する。
    /// </summary>
    internal sealed class OfxParamSet : OfxObject
    {
        readonly List<OfxParam> parameters = [];
        // FindはApplyTo経由で毎フレーム・パラメータ数ぶん呼ばれるため辞書を併設する
        readonly Dictionary<string, OfxParam> parametersByName = new(StringComparer.Ordinal);

        public OfxPropertySet Props { get; } = new();

        public IReadOnlyList<OfxParam> Parameters => parameters;

        public OfxParam Define(string paramType, string name)
        {
            if (parametersByName.ContainsKey(name))
                throw new InvalidOperationException($"パラメータが二重定義されました: {name}");
            var param = new OfxParam(name, paramType);
            parameters.Add(param);
            parametersByName[name] = param;
            return param;
        }

        public OfxParam? Find(string name) => parametersByName.TryGetValue(name, out var param) ? param : null;

        public override void Dispose()
        {
            foreach (var param in parameters)
                param.Dispose();
            parameters.Clear();
            Props.Dispose();
            base.Dispose();
        }
    }
}
