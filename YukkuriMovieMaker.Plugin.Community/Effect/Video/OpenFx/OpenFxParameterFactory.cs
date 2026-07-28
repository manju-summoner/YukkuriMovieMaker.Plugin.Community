using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx
{
    /// <summary>
    /// OFXのdescribeInContext結果（パラメータ定義）から、YMM4のプロパティエディタに表示する
    /// パラメータリストを組み立てる。
    /// </summary>
    internal static class OpenFxParameterFactory
    {
        /// <summary>
        /// パラメータリストを構築する。previous に同名・同型のパラメータがあれば値を引き継ぐ
        /// （プラグインの再選択・プロジェクト読み込み後の再構築で値を失わないため）。
        /// excludeNames にはホストが駆動するパラメータ（トランジションの進行度等）を指定してUIから除外する
        /// </summary>
        public static ImmutableList<OfxParameterBase> Create(OfxEffectDescriptor contextDescriptor, ImmutableList<OfxParameterBase> previous, IReadOnlyCollection<string>? excludeNames = null)
        {
            var definitions = contextDescriptor.ParamSet.Parameters;

            // グループパラメータ名 → 表示ラベル
            var groupLabels = definitions
                .Where(d => d.ParamType == OfxConstants.ParamTypeGroup)
                .ToDictionary(
                    d => d.Name,
                    d => d.Props.GetStringOrDefault(OfxConstants.PropLabel, d.Name),
                    StringComparer.Ordinal);
            var groupParents = definitions
                .Where(d => d.ParamType == OfxConstants.ParamTypeGroup)
                .ToDictionary(
                    d => d.Name,
                    d => d.Props.GetStringOrDefault(OfxConstants.ParamPropParent, ""),
                    StringComparer.Ordinal);

            var result = new List<OfxParameterBase>();
            var order = 0;
            foreach (var definition in definitions)
            {
                // UIに出さない種別・非表示のパラメータはスキップする（描画時は既定値が使われる）。
                // kOfxParamPropPersistant=0 は「保存しない」の意味で表示可否とは無関係なので除外しない
                // （本ホストはパラメータを一律.ymmpへ保存するため、非永続宣言でも値は保存される）
                if (definition.ParamType is OfxConstants.ParamTypeGroup or OfxConstants.ParamTypePage or OfxConstants.ParamTypePushButton)
                    continue;
                if (definition.Props.GetIntOrDefault(OfxConstants.ParamPropSecret, 0) != 0)
                    continue;
                if (excludeNames is not null && excludeNames.Contains(definition.Name))
                    continue;

                var parameter = Build(definition);
                if (parameter is null)
                    continue;
                parameter.Name = definition.Name;
                parameter.Label = definition.Props.GetStringOrDefault(OfxConstants.PropLabel, definition.Name);
                parameter.Description = definition.Props.GetStringOrDefault(OfxConstants.ParamPropHint, "");
                parameter.Group = ResolveGroupLabel(definition, groupLabels, groupParents);
                parameter.Order = order++;

                var old = previous.FirstOrDefault(p => p.Name == parameter.Name && p.GetType() == parameter.GetType());
                if (old is not null)
                    CopyValue(old, parameter);

                result.Add(parameter);
            }
            return [.. result];
        }

        static string ResolveGroupLabel(OfxParam definition, Dictionary<string, string> groupLabels, Dictionary<string, string> groupParents)
        {
            // 最も近い祖先グループのうち、ラベルを持つものを表示グループにする
            var parent = definition.Props.GetStringOrDefault(OfxConstants.ParamPropParent, "");
            var depth = 0;
            while (!string.IsNullOrEmpty(parent) && depth++ < 16)
            {
                if (groupLabels.TryGetValue(parent, out var label) && !string.IsNullOrEmpty(label))
                    return label;
                if (!groupParents.TryGetValue(parent, out parent!))
                    break;
            }
            return "";
        }

        static OfxParameterBase? Build(OfxParam definition)
        {
            switch (definition.ParamType)
            {
                case OfxConstants.ParamTypeDouble:
                case OfxConstants.ParamTypeInteger:
                    return BuildNumber(definition, dimension: 1);
                case OfxConstants.ParamTypeDouble2D:
                case OfxConstants.ParamTypeInteger2D:
                    return BuildNumber(definition, dimension: 2);
                case OfxConstants.ParamTypeDouble3D:
                case OfxConstants.ParamTypeInteger3D:
                    return BuildNumber(definition, dimension: 3);
                case OfxConstants.ParamTypeRGBA:
                case OfxConstants.ParamTypeRGB:
                    return BuildColor(definition);
                case OfxConstants.ParamTypeBoolean:
                    return new OfxBooleanParameter
                    {
                        Value = definition.Props.GetIntOrDefault(OfxConstants.ParamPropDefault, 0) != 0,
                    };
                case OfxConstants.ParamTypeChoice:
                    return new OfxChoiceParameter
                    {
                        Options = [.. definition.Props.GetStrings(OfxConstants.ParamPropChoiceOption)],
                        Value = definition.Props.GetIntOrDefault(OfxConstants.ParamPropDefault, 0),
                    };
                case OfxConstants.ParamTypeString:
                case OfxConstants.ParamTypeCustom:
                case OfxConstants.ParamTypeStrChoice:
                    return new OfxStringParameter
                    {
                        Value = definition.Props.GetStringOrDefault(OfxConstants.ParamPropDefault, ""),
                    };
                default:
                    return null;
            }
        }

        static OfxParameterBase BuildNumber(OfxParam definition, int dimension)
        {
            var isInteger = definition.ParamType
                is OfxConstants.ParamTypeInteger
                or OfxConstants.ParamTypeInteger2D
                or OfxConstants.ParamTypeInteger3D;
            // ハード範囲は次元ごとに異なる値を宣言できる
            var mins = Enumerable.Range(0, dimension)
                .Select(i => definition.Props.GetDoubleOrDefault(OfxConstants.ParamPropMin, double.MinValue, i))
                .ToArray();
            var maxs = Enumerable.Range(0, dimension)
                .Select(i => definition.Props.GetDoubleOrDefault(OfxConstants.ParamPropMax, double.MaxValue, i))
                .ToArray();
            var defaults = Enumerable.Range(0, dimension)
                .Select(i => definition.Props.GetDoubleOrDefault(OfxConstants.ParamPropDefault, 0, i))
                .ToArray();
            var (displayMin, displayMax) = GetSliderRange(definition, mins[0], maxs[0], defaults);
            var digits = definition.Props.GetIntOrDefault(OfxConstants.ParamPropDigits, isInteger ? 0 : 2);

            return dimension switch
            {
                1 => new OfxNumberParameter
                {
                    IsInteger = isInteger,
                    Min = mins[0],
                    Max = maxs[0],
                    DisplayMin = displayMin,
                    DisplayMax = displayMax,
                    Digits = digits,
                    Value = new Animation(defaults[0]),
                },
                2 => new OfxNumber2DParameter
                {
                    IsInteger = isInteger,
                    MinX = mins[0],
                    MaxX = maxs[0],
                    MinY = mins[1],
                    MaxY = maxs[1],
                    DisplayMin = displayMin,
                    DisplayMax = displayMax,
                    Digits = digits,
                    X = new Animation(defaults[0]),
                    Y = new Animation(defaults[1]),
                },
                _ => new OfxNumber3DParameter
                {
                    IsInteger = isInteger,
                    MinX = mins[0],
                    MaxX = maxs[0],
                    MinY = mins[1],
                    MaxY = maxs[1],
                    MinZ = mins[2],
                    MaxZ = maxs[2],
                    DisplayMin = displayMin,
                    DisplayMax = displayMax,
                    Digits = digits,
                    X = new Animation(defaults[0]),
                    Y = new Animation(defaults[1]),
                    Z = new Animation(defaults[2]),
                },
            };
        }

        /// <summary>
        /// スライダーの表示範囲を決める。DisplayMin/Max（無ければMin/Max）を使い、
        /// 未設定（±DBL_MAX等）の場合は既定値を含む常識的な範囲へフォールバックする
        /// （スライダー範囲は表示上のもので、反映時のクランプはMin/Maxで行う）。
        /// </summary>
        static (double Min, double Max) GetSliderRange(OfxParam definition, double hardMin, double hardMax, double[] defaults)
        {
            const double sliderLimit = 1e6;
            var min = definition.Props.Contains(OfxConstants.ParamPropDisplayMin)
                ? definition.Props.GetDoubleOrDefault(OfxConstants.ParamPropDisplayMin, hardMin)
                : hardMin;
            var max = definition.Props.Contains(OfxConstants.ParamPropDisplayMax)
                ? definition.Props.GetDoubleOrDefault(OfxConstants.ParamPropDisplayMax, hardMax)
                : hardMax;

            var defaultValue = defaults.Length > 0 ? defaults[0] : 0;
            if (!double.IsFinite(min) || min < -sliderLimit)
                min = Math.Min(0, defaultValue);
            if (!double.IsFinite(max) || max > sliderLimit)
                max = Math.Max(100, defaultValue);
            if (min >= max)
                (min, max) = (Math.Min(min, defaultValue), Math.Min(min, defaultValue) + 100);
            return (min, max);
        }

        static OfxColorParameter BuildColor(OfxParam definition)
        {
            var hasAlpha = definition.ParamType == OfxConstants.ParamTypeRGBA;
            byte Component(int index, byte fallback)
            {
                var value = definition.Props.GetDoubleOrDefault(OfxConstants.ParamPropDefault, fallback / 255.0, index);
                return (byte)Math.Clamp(Math.Round(value * 255), 0, 255);
            }
            return new OfxColorParameter
            {
                HasAlpha = hasAlpha,
                Value = Color.FromArgb(hasAlpha ? Component(3, 255) : (byte)255, Component(0, 255), Component(1, 255), Component(2, 255)),
            };
        }

        static void CopyValue(OfxParameterBase source, OfxParameterBase destination)
        {
            // Animationはインスタンスごと引き継ぐ（キーフレームを保持するため）。
            // 引き継いだ後は旧パラメータへダミーを差してUndo購読を外す（購読リーク防止）
            switch (source, destination)
            {
                case (OfxNumberParameter from, OfxNumberParameter to):
                    to.Value = from.Value;
                    from.Value = new Animation(0);
                    break;
                case (OfxNumber2DParameter from, OfxNumber2DParameter to):
                    to.X = from.X;
                    to.Y = from.Y;
                    from.X = new Animation(0);
                    from.Y = new Animation(0);
                    break;
                case (OfxNumber3DParameter from, OfxNumber3DParameter to):
                    to.X = from.X;
                    to.Y = from.Y;
                    to.Z = from.Z;
                    from.X = new Animation(0);
                    from.Y = new Animation(0);
                    from.Z = new Animation(0);
                    break;
                case (OfxColorParameter from, OfxColorParameter to):
                    to.Value = from.Value;
                    break;
                case (OfxBooleanParameter from, OfxBooleanParameter to):
                    to.Value = from.Value;
                    break;
                case (OfxChoiceParameter from, OfxChoiceParameter to):
                    to.Value = Math.Clamp(from.Value, 0, Math.Max(0, to.Options.Count - 1));
                    break;
                case (OfxStringParameter from, OfxStringParameter to):
                    to.Value = from.Value;
                    break;
            }
        }
    }
}
