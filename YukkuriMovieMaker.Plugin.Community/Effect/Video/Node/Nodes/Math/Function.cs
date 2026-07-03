using Vortice.Mathematics;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Localize;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Math;

[Node(typeof(MathFunctionsCategory), nameof(TextNode.AbsNode), nameof(TextNode.AbsNodeDescription), typeof(TextNode))]
public class AbsNode : NodeLogic
{
    [InputPort(nameof(TextNode.Value), nameof(TextNode.ValueDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Value
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.AbsResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        Result = MathF.Abs(Value);
        return Task.CompletedTask;
    }
}

[Node(typeof(MathFunctionsCategory), nameof(TextNode.SignNode), nameof(TextNode.SignNodeDescription), typeof(TextNode))]
public class SignNode : NodeLogic
{
    [InputPort(nameof(TextNode.Value), nameof(TextNode.ValueDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Value
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.SignResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        Result = MathF.Sign(Value);
        return Task.CompletedTask;
    }
}

[Node(typeof(MathFunctionsCategory), nameof(TextNode.MinNode), nameof(TextNode.MinNodeDescription), typeof(TextNode))]
public class MinNode : NodeLogic
{
    [InputPort(nameof(TextNode.LeftOperand), nameof(TextNode.LeftOperandDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Left
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.RightOperand), nameof(TextNode.RightOperandDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Right
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.MinResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        Result = MathF.Min(Left, Right);
        return Task.CompletedTask;
    }
}

[Node(typeof(MathFunctionsCategory), nameof(TextNode.MaxNode), nameof(TextNode.MaxNodeDescription), typeof(TextNode))]
public class MaxNode : NodeLogic
{
    [InputPort(nameof(TextNode.LeftOperand), nameof(TextNode.LeftOperandDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Left
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.RightOperand), nameof(TextNode.RightOperandDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Right
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.MaxResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        Result = MathF.Max(Left, Right);
        return Task.CompletedTask;
    }
}

[Node(typeof(MathFunctionsCategory), nameof(TextNode.ClampNode), nameof(TextNode.ClampNodeDescription),
    typeof(TextNode))]
public class ClampNode : NodeLogic
{
    [InputPort(nameof(TextNode.Value), nameof(TextNode.ValueDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Value
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.Minimum), nameof(TextNode.MinimumDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Min
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.Maximum), nameof(TextNode.MaximumDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000, Default = 1f)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Max
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.ClampResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        var min = MathF.Min(Min, Max);
        var max = MathF.Max(Min, Max);

        Result = MathF.Min(MathF.Max(Value, min), max);
        return Task.CompletedTask;
    }
}

[Node(typeof(MathFunctionsCategory), nameof(TextNode.LerpNode), nameof(TextNode.LerpNodeDescription), typeof(TextNode))]
public class LerpNode : NodeLogic
{
    [InputPort(nameof(TextNode.StartValue), nameof(TextNode.StartValueDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float A
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.EndValue), nameof(TextNode.EndValueDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float B
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.InterpolationFactor), nameof(TextNode.InterpolationFactorDescription), typeof(TextNode))]
    [NumberPortControl(Min = 0, Max = 1)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float T
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.LerpResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        Result = A + (B - A) * T;
        return Task.CompletedTask;
    }
}

[Node(typeof(MathFunctionsCategory), nameof(TextNode.SqrtNode), nameof(TextNode.SqrtNodeDescription), typeof(TextNode))]
public class SqrtNode : NodeLogic
{
    [InputPort(nameof(TextNode.Value), nameof(TextNode.ValueDescription), typeof(TextNode))]
    [NumberPortControl(Min = 0, Max = 40000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Value
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.SqrtResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        Result = MathF.Sqrt(MathF.Max(0, Value));
        return Task.CompletedTask;
    }
}

[Node(typeof(MathFunctionsCategory), nameof(TextNode.PowerNode), nameof(TextNode.PowerNodeDescription),
    typeof(TextNode))]
public class PowerNode : NodeLogic
{
    [InputPort(nameof(TextNode.Base), nameof(TextNode.BaseDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Base
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.Exponent), nameof(TextNode.ExponentDescription), typeof(TextNode))]
    [NumberPortControl(Min = -100, Max = 100, Default = 2)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Exponent
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.PowerResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        Result = MathF.Pow(Base, Exponent);
        return Task.CompletedTask;
    }
}

[Node(typeof(MathFunctionsCategory), nameof(TextNode.FloorNode), nameof(TextNode.FloorNodeDescription),
    typeof(TextNode))]
public class FloorNode : NodeLogic
{
    [InputPort(nameof(TextNode.Value), nameof(TextNode.ValueDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Value
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.FloorResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        Result = MathF.Floor(Value);
        return Task.CompletedTask;
    }
}

[Node(typeof(MathFunctionsCategory), nameof(TextNode.CeilingNode), nameof(TextNode.CeilingNodeDescription),
    typeof(TextNode))]
public class CeilingNode : NodeLogic
{
    [InputPort(nameof(TextNode.Value), nameof(TextNode.ValueDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Value
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.CeilingResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        Result = MathF.Ceiling(Value);
        return Task.CompletedTask;
    }
}

[Node(typeof(MathFunctionsCategory), nameof(TextNode.RoundNode), nameof(TextNode.RoundNodeDescription),
    typeof(TextNode))]
public class RoundNode : NodeLogic
{
    [InputPort(nameof(TextNode.Value), nameof(TextNode.ValueDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Value
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.RoundResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        Result = MathF.Round(Value);
        return Task.CompletedTask;
    }
}

[Node(typeof(MathFunctionsCategory), nameof(TextNode.FractionNode), nameof(TextNode.FractionNodeDescription),
    typeof(TextNode))]
public class FractionNode : NodeLogic
{
    [InputPort(nameof(TextNode.Value), nameof(TextNode.ValueDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Value
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.FractionResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        Result = Value - MathF.Floor(Value);
        return Task.CompletedTask;
    }
}

[Node(typeof(MathFunctionsCategory), nameof(TextNode.SinNode), nameof(TextNode.SinNodeDescription), typeof(TextNode))]
public class SinNode : NodeLogic
{
    [InputPort(nameof(TextNode.Value), nameof(TextNode.ValueDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Value
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.SinResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        Result = MathF.Sin(Value);
        return Task.CompletedTask;
    }
}

[Node(typeof(MathFunctionsCategory), nameof(TextNode.CosNode), nameof(TextNode.CosNodeDescription), typeof(TextNode))]
public class CosNode : NodeLogic
{
    [InputPort(nameof(TextNode.Value), nameof(TextNode.ValueDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Value
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.CosResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        Result = MathF.Cos(Value);
        return Task.CompletedTask;
    }
}

[Node(typeof(MathFunctionsCategory), nameof(TextNode.TanNode), nameof(TextNode.TanNodeDescription), typeof(TextNode))]
public class TanNode : NodeLogic
{
    [InputPort(nameof(TextNode.Value), nameof(TextNode.ValueDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Value
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.TanResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        Result = MathF.Tan(Value);
        return Task.CompletedTask;
    }
}

[Node(typeof(MathFunctionsCategory), nameof(TextNode.Atan2Node), nameof(TextNode.Atan2NodeDescription),
    typeof(TextNode))]
public class Atan2Node : NodeLogic
{
    [InputPort(nameof(TextNode.YValue), nameof(TextNode.YValueDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Y
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.XValue), nameof(TextNode.XValueDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float X
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.Atan2ResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        Result = MathF.Atan2(Y, X);
        return Task.CompletedTask;
    }
}

[Node(typeof(MathFunctionsCategory), nameof(TextNode.DegreesToRadiansNode),
    nameof(TextNode.DegreesToRadiansNodeDescription), typeof(TextNode))]
public class DegreesToRadiansNode : NodeLogic
{
    [InputPort(nameof(TextNode.Value), nameof(TextNode.ValueDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Value
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.DegreesToRadiansResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        Result = Value * (MathF.PI / 180f);
        return Task.CompletedTask;
    }
}

[Node(typeof(MathFunctionsCategory), nameof(TextNode.RadiansToDegreesNode),
    nameof(TextNode.RadiansToDegreesNodeDescription), typeof(TextNode))]
public class RadiansToDegreesNode : NodeLogic
{
    [InputPort(nameof(TextNode.Value), nameof(TextNode.ValueDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Value
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.RadiansToDegreesResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        Result = Value * (180f / MathF.PI);
        return Task.CompletedTask;
    }
}