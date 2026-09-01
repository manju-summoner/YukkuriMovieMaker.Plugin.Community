using Vortice.Mathematics;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Localize;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.MathCalc;

#region Basic Operators

[Node(typeof(MathAdvancedCategory), nameof(TextNode.ModuloNode), nameof(TextNode.ModuloNodeDescription),
    typeof(TextNode))]
public class ModuloNode : NodeLogic
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
    [NumberPortControl(Min = -40000, Max = 40000, Default = 1)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Right
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.ModuloResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        Result = MathF.Abs(Right) < 0.0001f ? 0f : Left % Right;
        return Task.CompletedTask;
    }
}

[Node(typeof(MathAdvancedCategory), nameof(TextNode.ReciprocalNode), nameof(TextNode.ReciprocalNodeDescription),
    typeof(TextNode))]
public class ReciprocalNode : NodeLogic
{
    [InputPort(nameof(TextNode.Value), nameof(TextNode.ValueDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000, Default = 1)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Value
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.ReciprocalResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        Result = MathF.Abs(Value) < 0.0001f ? 0f : 1f / Value;
        return Task.CompletedTask;
    }
}

[Node(typeof(MathAdvancedCategory), nameof(TextNode.OneMinusNode), nameof(TextNode.OneMinusNodeDescription),
    typeof(TextNode))]
public class OneMinusNode : NodeLogic
{
    [InputPort(nameof(TextNode.Value), nameof(TextNode.ValueDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Value
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.OneMinusResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        Result = 1f - Value;
        return Task.CompletedTask;
    }
}

[Node(typeof(MathAdvancedCategory), nameof(TextNode.SaturateNode), nameof(TextNode.SaturateNodeDescription),
    typeof(TextNode))]
public class SaturateNode : NodeLogic
{
    [InputPort(nameof(TextNode.Value), nameof(TextNode.ValueDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Value
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.SaturateResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        Result = MathF.Min(MathF.Max(Value, 0f), 1f);
        return Task.CompletedTask;
    }
}

#endregion

#region Interpolation

[Node(typeof(MathAdvancedCategory), nameof(TextNode.InverseLerpNode), nameof(TextNode.InverseLerpNodeDescription),
    typeof(TextNode))]
public class InverseLerpNode : NodeLogic
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
    [NumberPortControl(Min = -40000, Max = 40000, Default = 1)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float B
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.Value), nameof(TextNode.ValueDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Value
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.InverseLerpResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        var d = B - A;
        Result = MathF.Abs(d) < 0.0001f ? 0f : (Value - A) / d;
        return Task.CompletedTask;
    }
}

[Node(typeof(MathAdvancedCategory), nameof(TextNode.SmoothStepNode), nameof(TextNode.SmoothStepNodeDescription),
    typeof(TextNode))]
public class SmoothStepNode : NodeLogic
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
    [NumberPortControl(Min = -40000, Max = 40000, Default = 1)]
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

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.SmoothStepResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        var t = MathF.Min(MathF.Max(T, 0f), 1f);
        t = t * t * (3f - 2f * t);
        Result = A + (B - A) * t;
        return Task.CompletedTask;
    }
}

[Node(typeof(MathAdvancedCategory), nameof(TextNode.StepNode), nameof(TextNode.StepNodeDescription), typeof(TextNode))]
public class StepNode : NodeLogic
{
    [InputPort(nameof(TextNode.Edge), nameof(TextNode.EdgeDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Edge
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.Value), nameof(TextNode.ValueDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Value
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.StepResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        Result = Value < Edge ? 0f : 1f;
        return Task.CompletedTask;
    }
}

#endregion

#region Periodic

[Node(typeof(MathAdvancedCategory), nameof(TextNode.RepeatNode), nameof(TextNode.RepeatNodeDescription),
    typeof(TextNode))]
public class RepeatNode : NodeLogic
{
    [InputPort(nameof(TextNode.Value), nameof(TextNode.ValueDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Value
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.Length), nameof(TextNode.LengthDescription), typeof(TextNode))]
    [NumberPortControl(Min = 0.0001f, Max = 40000, Default = 1f)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Length
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.RepeatResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        if (Length <= 0.0001f)
        {
            Result = 0;
            return Task.CompletedTask;
        }

        Result = Value - MathF.Floor(Value / Length) * Length;
        return Task.CompletedTask;
    }
}

[Node(typeof(MathAdvancedCategory), nameof(TextNode.PingPongNode), nameof(TextNode.PingPongNodeDescription),
    typeof(TextNode))]
public class PingPongNode : NodeLogic
{
    [InputPort(nameof(TextNode.Value), nameof(TextNode.ValueDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Value
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.Length), nameof(TextNode.LengthDescription), typeof(TextNode))]
    [NumberPortControl(Min = 0.0001f, Max = 40000, Default = 1f)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Length
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.PingPongResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        if (Length <= 0.0001f)
        {
            Result = 0;
            return Task.CompletedTask;
        }

        var t = Value - MathF.Floor(Value / (Length * 2f)) * (Length * 2f);
        Result = Length - MathF.Abs(t - Length);
        return Task.CompletedTask;
    }
}

[Node(typeof(MathAdvancedCategory), nameof(TextNode.WrapNode), nameof(TextNode.WrapNodeDescription), typeof(TextNode))]
public class WrapNode : NodeLogic
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

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.WrapResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        var length = Max - Min;

        if (length <= 0.0001f)
        {
            Result = Min;
            return Task.CompletedTask;
        }

        Result = Value - MathF.Floor((Value - Min) / length) * length;
        return Task.CompletedTask;
    }
}

#endregion

#region Exponential

[Node(typeof(MathAdvancedCategory), nameof(TextNode.ExpNode), nameof(TextNode.ExpNodeDescription), typeof(TextNode))]
public class ExpNode : NodeLogic
{
    [InputPort(nameof(TextNode.Value), nameof(TextNode.ValueDescription), typeof(TextNode))]
    [NumberPortControl(Min = -80, Max = 80)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Value
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.ExpResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        Result = MathF.Exp(Value);
        return Task.CompletedTask;
    }
}

[Node(typeof(MathAdvancedCategory), nameof(TextNode.Exp2Node), nameof(TextNode.Exp2NodeDescription), typeof(TextNode))]
public class Exp2Node : NodeLogic
{
    [InputPort(nameof(TextNode.Value), nameof(TextNode.ValueDescription), typeof(TextNode))]
    [NumberPortControl(Min = -80, Max = 80)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Value
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.Exp2ResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        Result = MathF.Pow(2f, Value);
        return Task.CompletedTask;
    }
}

[Node(typeof(MathAdvancedCategory), nameof(TextNode.LogNode), nameof(TextNode.LogNodeDescription), typeof(TextNode))]
public class LogNode : NodeLogic
{
    [InputPort(nameof(TextNode.Value), nameof(TextNode.ValueDescription), typeof(TextNode))]
    [NumberPortControl(Min = 0.000001f, Max = 40000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Value
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.LogResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        Result = Value <= 0 ? 0 : MathF.Log(Value);
        return Task.CompletedTask;
    }
}

[Node(typeof(MathAdvancedCategory), nameof(TextNode.Log2Node), nameof(TextNode.Log2NodeDescription), typeof(TextNode))]
public class Log2Node : NodeLogic
{
    [InputPort(nameof(TextNode.Value), nameof(TextNode.ValueDescription), typeof(TextNode))]
    [NumberPortControl(Min = 0.000001f, Max = 40000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Value
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.Log2ResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        Result = Value <= 0f ? 0f : MathF.Log2(Value);
        return Task.CompletedTask;
    }
}

[Node(typeof(MathAdvancedCategory), nameof(TextNode.Log10Node), nameof(TextNode.Log10NodeDescription),
    typeof(TextNode))]
public class Log10Node : NodeLogic
{
    [InputPort(nameof(TextNode.Value), nameof(TextNode.ValueDescription), typeof(TextNode))]
    [NumberPortControl(Min = 0.000001f, Max = 40000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Value
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.Log10ResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        Result = Value <= 0f ? 0f : MathF.Log10(Value);
        return Task.CompletedTask;
    }
}

#endregion

#region Inverse Trigonometric

[Node(typeof(MathAdvancedCategory), nameof(TextNode.AsinNode), nameof(TextNode.AsinNodeDescription), typeof(TextNode))]
public class AsinNode : NodeLogic
{
    [InputPort(nameof(TextNode.Value), nameof(TextNode.ValueDescription), typeof(TextNode))]
    [NumberPortControl(Min = -1f, Max = 1f)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Value
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.AsinResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        Result = MathF.Asin(MathF.Min(MathF.Max(Value, -1f), 1f));
        return Task.CompletedTask;
    }
}

[Node(typeof(MathAdvancedCategory), nameof(TextNode.AcosNode), nameof(TextNode.AcosNodeDescription), typeof(TextNode))]
public class AcosNode : NodeLogic
{
    [InputPort(nameof(TextNode.Value), nameof(TextNode.ValueDescription), typeof(TextNode))]
    [NumberPortControl(Min = -1f, Max = 1f)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Value
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.AcosResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        Result = MathF.Acos(MathF.Min(MathF.Max(Value, -1f), 1f));
        return Task.CompletedTask;
    }
}

[Node(typeof(MathAdvancedCategory), nameof(TextNode.AtanNode), nameof(TextNode.AtanNodeDescription), typeof(TextNode))]
public class AtanNode : NodeLogic
{
    [InputPort(nameof(TextNode.Value), nameof(TextNode.ValueDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Value
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.AtanResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        Result = MathF.Atan(Value);
        return Task.CompletedTask;
    }
}

#endregion

#region Comparison

public abstract class ComparisonNodeBase : NodeLogic
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

    [InputPort(nameof(TextNode.Tolerance), nameof(TextNode.ToleranceDescription), typeof(TextNode))]
    [NumberPortControl(Min = 0, Max = 1, Default = 0.0001f)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Tolerance
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.ComparisonResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.Green))]
    public bool Result
    {
        get => GetOutput<bool>();
        set => SetOutput(value);
    }
}

[Node(typeof(MathAdvancedCategory), nameof(TextNode.EqualNode), nameof(TextNode.EqualNodeDescription),
    typeof(TextNode))]
public class EqualNode : ComparisonNodeBase
{
    protected override Task Calculate()
    {
        Result = MathF.Abs(Left - Right) <= Tolerance;
        return Task.CompletedTask;
    }
}

[Node(typeof(MathAdvancedCategory), nameof(TextNode.NotEqualNode), nameof(TextNode.NotEqualNodeDescription),
    typeof(TextNode))]
public class NotEqualNode : ComparisonNodeBase
{
    protected override Task Calculate()
    {
        Result = MathF.Abs(Left - Right) > Tolerance;
        return Task.CompletedTask;
    }
}

[Node(typeof(MathAdvancedCategory), nameof(TextNode.GreaterNode), nameof(TextNode.GreaterNodeDescription),
    typeof(TextNode))]
public class GreaterNode : ComparisonNodeBase
{
    protected override Task Calculate()
    {
        Result = Left > Right + Tolerance;
        return Task.CompletedTask;
    }
}

[Node(typeof(MathAdvancedCategory), nameof(TextNode.GreaterEqualNode), nameof(TextNode.GreaterEqualNodeDescription),
    typeof(TextNode))]
public class GreaterEqualNode : ComparisonNodeBase
{
    protected override Task Calculate()
    {
        Result = Left >= Right - Tolerance;
        return Task.CompletedTask;
    }
}

[Node(typeof(MathAdvancedCategory), nameof(TextNode.LessNode), nameof(TextNode.LessNodeDescription), typeof(TextNode))]
public class LessNode : ComparisonNodeBase
{
    protected override Task Calculate()
    {
        Result = Left < Right - Tolerance;
        return Task.CompletedTask;
    }
}

[Node(typeof(MathAdvancedCategory), nameof(TextNode.LessEqualNode), nameof(TextNode.LessEqualNodeDescription),
    typeof(TextNode))]
public class LessEqualNode : ComparisonNodeBase
{
    protected override Task Calculate()
    {
        Result = Left <= Right + Tolerance;
        return Task.CompletedTask;
    }
}

#endregion

#region Selection

[Node(typeof(MathAdvancedCategory), nameof(TextNode.SelectNode), nameof(TextNode.SelectNodeDescription),
    typeof(TextNode))]
public class SelectNode : NodeLogic
{
    [InputPort(nameof(TextNode.Condition), nameof(TextNode.ConditionDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.Green))]
    public bool Condition
    {
        get => GetInput<bool>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.TrueValue), nameof(TextNode.TrueValueDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float TrueValue
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.FalseValue), nameof(TextNode.FalseValueDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float FalseValue
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.SelectResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        Result = Condition ? TrueValue : FalseValue;
        return Task.CompletedTask;
    }
}

#endregion