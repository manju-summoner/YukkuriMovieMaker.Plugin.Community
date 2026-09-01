using System.Windows.Media;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control.Bezier.Model;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Localize;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.MathCalc;

[Node(typeof(MathBasicCategory), nameof(TextNode.EasingNode), nameof(TextNode.EasingNodeDescription), typeof(TextNode))]
public class EasingNode : NodeLogic
{
    [InputPort(nameof(TextNode.TimeStartLabel), nameof(TextNode.TimeStartDescription), typeof(TextNode))]
    [NumberPortControl(Digits = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float TimeStart
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.TimeEndLabel), nameof(TextNode.TimeEndDescription), typeof(TextNode))]
    [NumberPortControl(Digits = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float TimeEnd
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.TimeParameterLabel), nameof(TextNode.TimeParameterDescription), typeof(TextNode))]
    [NumberPortControl(Digits = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Time
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.EasingMaxLabel), nameof(TextNode.EasingMaxDescription), typeof(TextNode))]
    [NumberPortControl(Digits = 3)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Max
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.EasingMinLabel), nameof(TextNode.EasingMinDescription), typeof(TextNode))]
    [NumberPortControl(Digits = 3)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Min
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.BezierCurveLabel), nameof(TextNode.BezierCurveDescription), typeof(TextNode))]
    [BezierPortControl]
    [PortColorSetting(nameof(Colors.LawnGreen))]
    public string Curve
    {
        get => GetInput<string>() ?? "";
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.EasingResultLabel), nameof(TextNode.EasingResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        var duration = TimeEnd - TimeStart;

        var t = Math.Abs(duration) < 1e-8f
            ? 0f
            : (Time - TimeStart) / duration;

        t = Math.Clamp(t, 0f, 1f);

        var curve = BezierParser.Deserialize(Curve);
        var y = (float)BezierEvaluator.Evaluate(curve, t);

        Result = Min + (Max - Min) * y;

        return Task.CompletedTask;
    }
}