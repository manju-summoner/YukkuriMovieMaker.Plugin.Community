using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using SkiaSharp;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.ValueTypes;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Generator.Outline;

public enum PathCombineKind
{
    [Display(Name = "結合")] Union,

    [Display(Name = "交差")] Intersect,

    [Display(Name = "差分")] Difference,

    [Display(Name = "逆差分")] ReverseDifference,

    [Display(Name = "排他的論理和")] Xor
}

[Node(
    typeof(OutlineCategory),
    "パス結合",
    "2つのアウトラインを指定した演算方法で結合します。")]
public class PathCombineNode : NodeLogic
{
    private SKPath? _path;

    [InputPort("パス1", "結合する1つ目のアウトライン")]
    [PortColorSetting(nameof(Colors.MediumPurple))]
    public OutlineWrapper? Input1
    {
        get => GetInput<OutlineWrapper>();
        set => SetInput(value);
    }

    [InputPort("パス2", "結合する2つ目のアウトライン")]
    [PortColorSetting(nameof(Colors.MediumPurple))]
    public OutlineWrapper? Input2
    {
        get => GetInput<OutlineWrapper>();
        set => SetInput(value);
    }

    [InputPort("演算", "2つのパスに適用するブール演算")]
    [EnumPortControl(
        Default = 0,
        IsEditable = false,
        Items = typeof(PathCombineKind))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public int Kind
    {
        get => GetInput<int>();
        set => SetInput(value);
    }

    [OutputPort("アウトライン", "結合結果のアウトラインデータ")]
    [PortColorSetting(nameof(Colors.MediumPurple))]
    public OutlineWrapper? Output
    {
        get => GetOutput<OutlineWrapper>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        _path?.Dispose();
        _path = null;

        if (Input1?.Path is null || Input2?.Path is null)
        {
            Output = null;
            return Task.CompletedTask;
        }

        var op = (PathCombineKind)Kind switch
        {
            PathCombineKind.Union => SKPathOp.Union,
            PathCombineKind.Intersect => SKPathOp.Intersect,
            PathCombineKind.Difference => SKPathOp.Difference,
            PathCombineKind.ReverseDifference => SKPathOp.ReverseDifference,
            PathCombineKind.Xor => SKPathOp.Xor,
            _ => SKPathOp.Union
        };

        var result = new SKPath();
        if (!Input1.Path.Op(Input2.Path, op, result))
        {
            result.Dispose();
            Output = null;
            return Task.CompletedTask;
        }

        _path = result;
        Output = new OutlineWrapper
        {
            Path = _path
        };

        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _path?.Dispose();
        _path = null;
        base.Dispose();
    }
}