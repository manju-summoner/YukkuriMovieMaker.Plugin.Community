using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using SkiaSharp;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Localize;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.ValueTypes;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Generator.Outline;

public enum PathCombineKind
{
    [Display(Name = nameof(TextNode.PathOpUnion), ResourceType = typeof(TextNode))]
    Union,

    [Display(Name = nameof(TextNode.PathOpIntersect), ResourceType = typeof(TextNode))]
    Intersect,

    [Display(Name = nameof(TextNode.PathOpDifference), ResourceType = typeof(TextNode))]
    Difference,

    [Display(Name = nameof(TextNode.PathOpReverseDifference), ResourceType = typeof(TextNode))]
    ReverseDifference,

    [Display(Name = nameof(TextNode.PathOpXor), ResourceType = typeof(TextNode))]
    Xor
}

[Node(
    typeof(OutlineCategory),
    nameof(TextNode.PathCombineNode),
    nameof(TextNode.PathCombineNodeDescription), typeof(TextNode))]
public class PathCombineNode : NodeLogic
{
    private SKPath? _path;

    [InputPort(nameof(TextNode.PathInput1Label), nameof(TextNode.PathInput1Description), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.MediumPurple))]
    public OutlineWrapper? Input1
    {
        get => GetInput<OutlineWrapper>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.PathInput2Label), nameof(TextNode.PathInput2Description), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.MediumPurple))]
    public OutlineWrapper? Input2
    {
        get => GetInput<OutlineWrapper>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.PathCombineOperationLabel), nameof(TextNode.PathCombineOperationDescription),
        typeof(TextNode))]
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

    [OutputPort(nameof(TextNode.OutlinePortLabel), nameof(TextNode.PathCombineOutputDescription), typeof(TextNode))]
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