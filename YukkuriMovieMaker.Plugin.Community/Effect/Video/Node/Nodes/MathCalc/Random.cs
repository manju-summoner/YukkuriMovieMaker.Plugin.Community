using Vortice.Mathematics;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Localize;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.MathCalc;

internal static class NoiseUtility
{
    private static uint Hash(uint x)
    {
        x ^= x >> 16;
        x *= 0x7feb352d;
        x ^= x >> 15;
        x *= 0x846ca68b;
        x ^= x >> 16;
        return x;
    }

    public static float Random01(int seed, int index)
    {
        var h = Hash((uint)seed ^ Hash((uint)index));
        return (h & 0x00FFFFFF) / 16777215f;
    }

    public static float Noise01(int seed, float parameter)
    {
        var i = (int)MathF.Floor(parameter);
        var t = parameter - i;

        var a = Random01(seed, i);
        var b = Random01(seed, i + 1);

        t = t * t * (3f - 2f * t);

        return a + (b - a) * t;
    }
}

[Node(typeof(MathAdvancedCategory), nameof(TextNode.RandomNode), nameof(TextNode.RandomNodeDescription),
    typeof(TextNode))]
public class RandomNode : NodeLogic
{
    [InputPort(nameof(TextNode.Seed), nameof(TextNode.SeedDescription), typeof(TextNode))]
    [NumberPortControl(Min = int.MinValue, Max = int.MaxValue, Digits = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public int Seed
    {
        get => GetInput<int>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.Index), nameof(TextNode.IndexDescription), typeof(TextNode))]
    [NumberPortControl(Min = int.MinValue, Max = int.MaxValue, Digits = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public int Index
    {
        get => GetInput<int>();
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
        Result = NoiseUtility.Random01(Seed, Index);
        return Task.CompletedTask;
    }
}

[Node(typeof(MathAdvancedCategory), nameof(TextNode.NoiseNode), nameof(TextNode.NoiseNodeDescription),
    typeof(TextNode))]
public class NoiseNode : NodeLogic
{
    [InputPort(nameof(TextNode.Seed), nameof(TextNode.SeedDescription), typeof(TextNode))]
    [NumberPortControl(Min = int.MinValue, Max = int.MaxValue, Digits = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public int Seed
    {
        get => GetInput<int>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.Parameter), nameof(TextNode.ParameterDescription), typeof(TextNode))]
    [NumberPortControl(Min = -100000, Max = 100000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Parameter
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
        Result = NoiseUtility.Noise01(Seed, Parameter);
        return Task.CompletedTask;
    }
}