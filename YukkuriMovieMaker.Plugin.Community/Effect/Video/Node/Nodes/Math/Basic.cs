using Vortice.Mathematics;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Localize;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Math;

[Node(typeof(MathBasicCategory), nameof(TextNode.AddNode), nameof(TextNode.AddNodeDescription), typeof(TextNode))]
public class AddNode : NodeLogic
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

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.AddResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        Result = Left + Right;
        return Task.CompletedTask;
    }
}

[Node(typeof(MathBasicCategory), nameof(TextNode.SubtractNode), nameof(TextNode.SubtractNodeDescription),
    typeof(TextNode))]
public class SubtractNode : NodeLogic
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

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.SubtractResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        Result = Left - Right;
        return Task.CompletedTask;
    }
}

[Node(typeof(MathBasicCategory), nameof(TextNode.MultiplyNode), nameof(TextNode.MultiplyNodeDescription),
    typeof(TextNode))]
public class MultiplyNode : NodeLogic
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
    [NumberPortControl(Min = -40000, Max = 40000, Default = 1f)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Right
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.MultiplyResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        Result = Left * Right;
        return Task.CompletedTask;
    }
}

[Node(typeof(MathBasicCategory), nameof(TextNode.DivideNode), nameof(TextNode.DivideNodeDescription), typeof(TextNode))]
public class DivideNode : NodeLogic
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
    [NumberPortControl(Min = -40000, Max = 40000, Default = 1f)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Right
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.DivideResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        if (MathF.Abs(Right) < 0.0001f)
        {
            Result = 0;
            return Task.CompletedTask;
        }

        Result = Left / Right;
        return Task.CompletedTask;
    }
}