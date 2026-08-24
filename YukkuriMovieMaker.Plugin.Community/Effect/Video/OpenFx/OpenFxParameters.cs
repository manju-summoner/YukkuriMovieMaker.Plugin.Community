using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx
{
    /// <summary>
    /// OFXプラグインのパラメータ1つ分（YMM4のプロパティエディタに動的に表示する要素）。
    /// VOICEPEAKの感情パラメータと同じ方式（ImmutableListの要素 + CustomDisplayAttributeBase）で、
    /// 実行時型のプロパティが列挙されるため、型ごとの派生クラスを1つのリストへ混在させて使う。
    /// 表示メタデータ（ラベル・グループ・範囲など）はプラグイン欠落時もUIを再現できるよう
    /// プロジェクトファイルへ保存する。
    /// </summary>
    public abstract class OfxParameterBase : Animatable
    {
        /// <summary>OFXパラメータ名（kOfxPropName。識別子）</summary>
        public string Name { get => name; set => Set(ref name, value); }
        string name = "";

        /// <summary>表示名（kOfxPropLabel）</summary>
        public string Label { get => label; set => Set(ref label, value); }
        string label = "";

        /// <summary>説明（kOfxParamPropHint）</summary>
        public string Description { get => description; set => Set(ref description, value); }
        string description = "";

        /// <summary>表示グループ名（OFXのグループパラメータのラベル）</summary>
        public string Group { get => group; set => Set(ref group, value); }
        string group = "";

        /// <summary>表示順（describeInContextでの定義順）</summary>
        public int Order { get => order; set => Set(ref order, value); }
        int order;

        protected override IEnumerable<IAnimatable> GetAnimatables() => [];

        /// <summary>現在の値をOFXエフェクトインスタンスへ反映する</summary>
        internal abstract void ApplyTo(OfxEffectInstance instance, int frame, int length, int fps);

        /// <summary>
        /// ApplyToがインスタンスへ設定するのと同じ実効値（クランプ・丸め適用後。Animationは指定フレームの評価値）を列挙へ追加する。
        /// 失敗後の再試行判定（前回失敗時と同じ試行入力かどうかの比較）に使う
        /// </summary>
        internal abstract void CollectValues(ICollection<object?> values, int frame, int length, int fps);

        /// <summary>
        /// Animationプロパティの差し替え（ファクトリの値引き継ぎ・デシリアライズ）用のセッター処理。
        /// 自動プロパティのセッターはUndoRedoableのSetを通らないため、
        /// Undo/Redoイベントの購読をここで明示的に付け替える。
        /// 注意: SetKeyFrames/SetAnimationParametersの伝播は行わないため、Animationの差し替えは
        /// エフェクトのParametersリストへ組み込む前（ファクトリ内・デシリアライズ時）に限ること
        /// （リスト代入時にAnimatable.Setが全要素へキーフレーム情報を伝播して辻褄が合う）
        /// </summary>
        protected void SetAnimationField(ref Animation field, Animation value)
        {
            if (ReferenceEquals(field, value))
                return;
            UnSubscribeChildUndoRedoable(field);
            field = value;
            SubscribeChildUndoRedoable(field);
        }
    }

    /// <summary>
    /// 数値パラメータ（Double / Integer）。アニメーション可能。
    /// </summary>
    public class OfxNumberParameter : OfxParameterBase
    {
        public bool IsInteger { get => isInteger; set => Set(ref isInteger, value); }
        bool isInteger;

        /// <summary>OFXのハード範囲（kOfxParamPropMin/Max。反映時にクランプする）</summary>
        public double Min { get => min; set => Set(ref min, value); }
        double min = double.MinValue;
        public double Max { get => max; set => Set(ref max, value); }
        double max = double.MaxValue;

        /// <summary>スライダーの表示範囲（kOfxParamPropDisplayMin/Max由来）</summary>
        public double DisplayMin { get => displayMin; set => Set(ref displayMin, value); }
        double displayMin;
        public double DisplayMax { get => displayMax; set => Set(ref displayMax, value); }
        double displayMax = 1;

        /// <summary>小数点以下の桁数（kOfxParamPropDigits）</summary>
        public int Digits { get => digits; set => Set(ref digits, value); }
        int digits = 2;

        [OfxParamDisplay]
        [OfxAnimationSlider]
        public Animation Value { get => value; set => SetAnimationField(ref this.value, value); }
        Animation value = new(0);

        internal string StringFormat => IsInteger ? "F0" : $"F{Math.Clamp(Digits, 0, 6)}";

        protected override IEnumerable<IAnimatable> GetAnimatables() => [value];

        internal override void ApplyTo(OfxEffectInstance instance, int frame, int length, int fps)
        {
            var value = Math.Clamp(Value.GetValue(frame, length, fps), Min, Max);
            if (IsInteger)
                instance.SetIntParam(Name, (int)Math.Round(value));
            else
                instance.SetDoubleParam(Name, value);
        }

        internal override void CollectValues(ICollection<object?> values, int frame, int length, int fps)
        {
            var value = Math.Clamp(Value.GetValue(frame, length, fps), Min, Max);
            values.Add(IsInteger ? (int)Math.Round(value) : value);
        }
    }

    /// <summary>
    /// 2次元数値パラメータ（Double2D / Integer2D）。アニメーション可能。
    /// ハード範囲（Min/Max）はOFX仕様で次元ごとに異なる値を宣言できるため次元別に保持する
    /// </summary>
    public class OfxNumber2DParameter : OfxParameterBase
    {
        public bool IsInteger { get => isInteger; set => Set(ref isInteger, value); }
        bool isInteger;
        public double MinX { get => minX; set => Set(ref minX, value); }
        double minX = double.MinValue;
        public double MaxX { get => maxX; set => Set(ref maxX, value); }
        double maxX = double.MaxValue;
        public double MinY { get => minY; set => Set(ref minY, value); }
        double minY = double.MinValue;
        public double MaxY { get => maxY; set => Set(ref maxY, value); }
        double maxY = double.MaxValue;
        public double DisplayMin { get => displayMin; set => Set(ref displayMin, value); }
        double displayMin;
        public double DisplayMax { get => displayMax; set => Set(ref displayMax, value); }
        double displayMax = 1;
        public int Digits { get => digits; set => Set(ref digits, value); }
        int digits = 2;

        [OfxParamDisplay(0)]
        [OfxAnimationSlider]
        public Animation X { get => x; set => SetAnimationField(ref x, value); }
        Animation x = new(0);

        [OfxParamDisplay(1)]
        [OfxAnimationSlider]
        public Animation Y { get => y; set => SetAnimationField(ref y, value); }
        Animation y = new(0);

        internal string StringFormat => IsInteger ? "F0" : $"F{Math.Clamp(Digits, 0, 6)}";

        protected override IEnumerable<IAnimatable> GetAnimatables() => [x, y];

        internal override void ApplyTo(OfxEffectInstance instance, int frame, int length, int fps)
        {
            var x = Math.Clamp(X.GetValue(frame, length, fps), MinX, MaxX);
            var y = Math.Clamp(Y.GetValue(frame, length, fps), MinY, MaxY);
            if (IsInteger)
                instance.SetIntParam(Name, (int)Math.Round(x), (int)Math.Round(y));
            else
                instance.SetDoubleParam(Name, x, y);
        }

        internal override void CollectValues(ICollection<object?> values, int frame, int length, int fps)
        {
            var x = Math.Clamp(X.GetValue(frame, length, fps), MinX, MaxX);
            var y = Math.Clamp(Y.GetValue(frame, length, fps), MinY, MaxY);
            if (IsInteger)
            {
                values.Add((int)Math.Round(x));
                values.Add((int)Math.Round(y));
            }
            else
            {
                values.Add(x);
                values.Add(y);
            }
        }
    }

    /// <summary>
    /// 3次元数値パラメータ（Double3D / Integer3D）。アニメーション可能。
    /// ハード範囲（Min/Max）はOFX仕様で次元ごとに異なる値を宣言できるため次元別に保持する
    /// </summary>
    public class OfxNumber3DParameter : OfxParameterBase
    {
        public bool IsInteger { get => isInteger; set => Set(ref isInteger, value); }
        bool isInteger;
        public double MinX { get => minX; set => Set(ref minX, value); }
        double minX = double.MinValue;
        public double MaxX { get => maxX; set => Set(ref maxX, value); }
        double maxX = double.MaxValue;
        public double MinY { get => minY; set => Set(ref minY, value); }
        double minY = double.MinValue;
        public double MaxY { get => maxY; set => Set(ref maxY, value); }
        double maxY = double.MaxValue;
        public double MinZ { get => minZ; set => Set(ref minZ, value); }
        double minZ = double.MinValue;
        public double MaxZ { get => maxZ; set => Set(ref maxZ, value); }
        double maxZ = double.MaxValue;
        public double DisplayMin { get => displayMin; set => Set(ref displayMin, value); }
        double displayMin;
        public double DisplayMax { get => displayMax; set => Set(ref displayMax, value); }
        double displayMax = 1;
        public int Digits { get => digits; set => Set(ref digits, value); }
        int digits = 2;

        [OfxParamDisplay(0)]
        [OfxAnimationSlider]
        public Animation X { get => x; set => SetAnimationField(ref x, value); }
        Animation x = new(0);

        [OfxParamDisplay(1)]
        [OfxAnimationSlider]
        public Animation Y { get => y; set => SetAnimationField(ref y, value); }
        Animation y = new(0);

        [OfxParamDisplay(2)]
        [OfxAnimationSlider]
        public Animation Z { get => z; set => SetAnimationField(ref z, value); }
        Animation z = new(0);

        internal string StringFormat => IsInteger ? "F0" : $"F{Math.Clamp(Digits, 0, 6)}";

        protected override IEnumerable<IAnimatable> GetAnimatables() => [x, y, z];

        internal override void ApplyTo(OfxEffectInstance instance, int frame, int length, int fps)
        {
            var x = Math.Clamp(X.GetValue(frame, length, fps), MinX, MaxX);
            var y = Math.Clamp(Y.GetValue(frame, length, fps), MinY, MaxY);
            var z = Math.Clamp(Z.GetValue(frame, length, fps), MinZ, MaxZ);
            if (IsInteger)
                instance.SetIntParam(Name, (int)Math.Round(x), (int)Math.Round(y), (int)Math.Round(z));
            else
                instance.SetDoubleParam(Name, x, y, z);
        }

        internal override void CollectValues(ICollection<object?> values, int frame, int length, int fps)
        {
            var x = Math.Clamp(X.GetValue(frame, length, fps), MinX, MaxX);
            var y = Math.Clamp(Y.GetValue(frame, length, fps), MinY, MaxY);
            var z = Math.Clamp(Z.GetValue(frame, length, fps), MinZ, MaxZ);
            if (IsInteger)
            {
                values.Add((int)Math.Round(x));
                values.Add((int)Math.Round(y));
                values.Add((int)Math.Round(z));
            }
            else
            {
                values.Add(x);
                values.Add(y);
                values.Add(z);
            }
        }
    }

    /// <summary>
    /// 色パラメータ（RGB / RGBA）
    /// </summary>
    public class OfxColorParameter : OfxParameterBase
    {
        public bool HasAlpha { get => hasAlpha; set => Set(ref hasAlpha, value); }
        bool hasAlpha;

        [OfxParamDisplay]
        [ColorPicker]
        public Color Value { get => value; set => Set(ref this.value, value); }
        Color value = Colors.White;

        internal override void ApplyTo(OfxEffectInstance instance, int frame, int length, int fps)
        {
            if (HasAlpha)
                instance.SetDoubleParam(Name, Value.R / 255.0, Value.G / 255.0, Value.B / 255.0, Value.A / 255.0);
            else
                instance.SetDoubleParam(Name, Value.R / 255.0, Value.G / 255.0, Value.B / 255.0);
        }

        internal override void CollectValues(ICollection<object?> values, int frame, int length, int fps)
        {
            values.Add(HasAlpha ? Value : Color.FromRgb(Value.R, Value.G, Value.B));
        }
    }

    /// <summary>
    /// 真偽値パラメータ
    /// </summary>
    public class OfxBooleanParameter : OfxParameterBase
    {
        [OfxParamDisplay]
        [ToggleSlider]
        public bool Value { get => value; set => Set(ref this.value, value); }
        bool value;

        internal override void ApplyTo(OfxEffectInstance instance, int frame, int length, int fps)
        {
            instance.SetBoolParam(Name, Value);
        }

        internal override void CollectValues(ICollection<object?> values, int frame, int length, int fps)
        {
            values.Add(Value);
        }
    }

    /// <summary>
    /// 選択肢パラメータ（Choice。値は選択肢のインデックス）
    /// </summary>
    public class OfxChoiceParameter : OfxParameterBase
    {
        public ImmutableList<string> Options { get => options; set => Set(ref options, value); }
        ImmutableList<string> options = [];

        [OfxParamDisplay]
        [OfxChoiceComboBox]
        public int Value { get => value; set => Set(ref this.value, value); }
        int value;

        internal override void ApplyTo(OfxEffectInstance instance, int frame, int length, int fps)
        {
            instance.SetIntParam(Name, Math.Clamp(Value, 0, Math.Max(0, Options.Count - 1)));
        }

        internal override void CollectValues(ICollection<object?> values, int frame, int length, int fps)
        {
            values.Add(Math.Clamp(Value, 0, Math.Max(0, Options.Count - 1)));
        }
    }

    /// <summary>
    /// 文字列パラメータ（String / Custom / StrChoice）
    /// </summary>
    public class OfxStringParameter : OfxParameterBase
    {
        [OfxParamDisplay]
        [TextEditor]
        public string Value { get => value; set => Set(ref this.value, value); }
        string value = "";

        internal override void ApplyTo(OfxEffectInstance instance, int frame, int length, int fps)
        {
            instance.SetStringParam(Name, Value);
        }

        internal override void CollectValues(ICollection<object?> values, int frame, int length, int fps)
        {
            values.Add(Value);
        }
    }
}
