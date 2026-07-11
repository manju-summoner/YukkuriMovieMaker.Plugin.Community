using System.Windows.Media;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control.Bezier.Model;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Math;

[Node(typeof(MathBasicCategory), "イージング", "時間パラメーターをベジェ曲線で評価し、最小～最大の範囲に写像する")]
public class EasingNode : NodeLogic
{
    [InputPort("時間始端", "時間パラメーターの正規化に用いる開始時刻")]
    [NumberPortControl(Digits = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float TimeStart
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("時間終端", "時間パラメーターの正規化に用いる終了時刻")]
    [NumberPortControl(Digits = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float TimeEnd
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("時間パラメーター", "評価対象の時刻")]
    [NumberPortControl(Digits = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Time
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("最大", "出力値の上限")]
    [NumberPortControl(Digits = 3)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Max
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("最小", "出力値の下限")]
    [NumberPortControl(Digits = 3)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Min
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("ベジェ曲線", "時間始端から時間終端までの正規化された時間に対する値を定義する曲線")]
    [BezierPortControl]
    [PortColorSetting(nameof(Colors.LawnGreen))]
    public string Curve
    {
        get => GetInput<string>() ?? "";
        set => SetInput(value);
    }

    [OutputPort("値", "評価結果")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        var duration = TimeEnd - TimeStart;

        var t = System.Math.Abs(duration) < 1e-8f
            ? 0f
            : (Time - TimeStart) / duration;

        t = System.Math.Clamp(t, 0f, 1f);

        var curve = BezierParser.Deserialize(Curve);
        var y = (float)BezierEvaluator.Evaluate(curve, t);

        Result = Min + (Max - Min) * y;

        return Task.CompletedTask;
    }
}