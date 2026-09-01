using System.Windows.Media;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Localize;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Value;

[Node(typeof(ValueCategory), nameof(TextNode.NumberNode), nameof(TextNode.NumberNodeDescription), typeof(TextNode))]
public class NumberNode : NodeLogic
{
    [InputPort(nameof(TextNode.Value), nameof(TextNode.ValueDescription), typeof(TextNode))]
    [NumberPortControl(Min = -1000000, Max = 1000000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Value
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.ResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Result
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        Result = Value;
        return Task.CompletedTask;
    }
}

[Node(typeof(ValueCategory), nameof(TextNode.StringNode), nameof(TextNode.StringNodeDescription), typeof(TextNode))]
public class StringNode : NodeLogic
{
    [InputPort(nameof(TextNode.Value), nameof(TextNode.ValueDescription), typeof(TextNode))]
    [TextPortControl(Default = "")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public string Value
    {
        get => GetInput<string>() ?? "";
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.ResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public string Result
    {
        get => GetOutput<string>() ?? "";
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        Result = Value;
        return Task.CompletedTask;
    }
}

[Node(typeof(ValueCategory), nameof(TextNode.BooleanNode), nameof(TextNode.BooleanNodeDescription),
    typeof(TextNode))]
public class BooleanNode : NodeLogic
{
    [InputPort(nameof(TextNode.Value), nameof(TextNode.ValueDescription), typeof(TextNode))]
    [BoolPortControl(Default = false)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public bool Value
    {
        get => GetInput<bool>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.ResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.Green))]
    public bool Result
    {
        get => GetOutput<bool>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        Result = Value;
        return Task.CompletedTask;
    }
}

[Node(typeof(ValueCategory), nameof(TextNode.ColorNode), nameof(TextNode.ColorNodeDescription), typeof(TextNode))]
public class ColorNode : NodeLogic
{
    [InputPort(nameof(TextNode.Value), nameof(TextNode.ValueDescription), typeof(TextNode))]
    [ColorPortControl(DefaultColor = "#FFFFFFFF")]
    [PortColorSetting(nameof(Colors.Gold))]
    public Color Value
    {
        get => GetInput<Color>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Result), nameof(TextNode.ResultDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.Gold))]
    public Color Result
    {
        get => GetOutput<Color>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        Result = Value;
        return Task.CompletedTask;
    }
}