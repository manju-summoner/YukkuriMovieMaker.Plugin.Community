using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Math;

[Node(typeof(MathBasicCategory), "加算", "Add two numbers")]
public class AddNode : NodeLogic
{
    [InputPort("左項", "Left operand")]
    [NumberPortControl(Min = -40000, Max = 40000)]
    public float Left
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("右項", "Right operand")]
    [NumberPortControl(Min = -40000, Max = 40000)]
    public float Right
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort("結果", "Sum")]
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

[Node(typeof(MathBasicCategory), "減算", "Subtract B from A")]
public class SubtractNode : NodeLogic
{
    [InputPort("左項", "Left operand")]
    [NumberPortControl(Min = -40000, Max = 40000)]
    public float Left
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("右項", "Right operand")]
    [NumberPortControl(Min = -40000, Max = 40000)]
    public float Right
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort("結果", "A minus B")] public float Result { get; set; }

    protected override Task Calculate()
    {
        Result = Left - Right;
        return Task.CompletedTask;
    }
}

[Node(typeof(MathBasicCategory), "乗算", "Multiply two numbers")]
public class MultiplyNode : NodeLogic
{
    [InputPort("左項", "Left operand")]
    [NumberPortControl(Min = -40000, Max = 40000)]
    public float Left
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("右項", "Right operand")]
    [NumberPortControl(Min = -40000, Max = 40000)]
    public float Right
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort("結果", "Product")]
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

[Node(typeof(MathBasicCategory), "除算", "Divide A by B")]
public class DivideNode : NodeLogic
{
    [InputPort("左項", "Left operand")]
    [NumberPortControl(Min = -40000, Max = 40000)]
    public float Left
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    [InputPort("右項", "Right operand")]
    [NumberPortControl(Min = -40000, Max = 40000)]
    public float Right
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort("結果", "Quotient of A divided by B")]
    public float Result { get; set; }

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