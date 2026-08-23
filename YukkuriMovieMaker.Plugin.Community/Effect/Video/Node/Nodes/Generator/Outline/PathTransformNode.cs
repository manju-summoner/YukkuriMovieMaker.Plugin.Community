using System.Windows.Media;
using SkiaSharp;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Localize;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.ValueTypes;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Generator.Outline;

[Node(
    typeof(OutlineCategory),
    nameof(TextNode.PathTransformNode),
    nameof(TextNode.PathTransformNodeDescription), typeof(TextNode))]
public class PathTransformNode : NodeLogic
{
    private SKPath? _path;

    [InputPort(nameof(TextNode.OutlinePortLabel), nameof(TextNode.PathTransformInputDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.MediumPurple))]
    public OutlineWrapper? Input
    {
        get => GetInput<OutlineWrapper>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.PathMoveXLabel), nameof(TextNode.PathMoveXDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000, Default = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float TranslateX
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.PathMoveYLabel), nameof(TextNode.PathMoveYDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000, Default = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float TranslateY
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.PathScaleXLabel), nameof(TextNode.PathScaleXDescription), typeof(TextNode))]
    [NumberPortControl(Min = -100, Max = 100, Default = 1)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float ScaleX
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.PathScaleYLabel), nameof(TextNode.PathScaleYDescription), typeof(TextNode))]
    [NumberPortControl(Min = -100, Max = 100, Default = 1)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float ScaleY
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.RotationPortLabel), nameof(TextNode.PathRotationDescription), typeof(TextNode))]
    [NumberPortControl(Min = -3600, Max = 3600, Default = 0, Unit = "deg")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Rotation
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.SkewXLabel), nameof(TextNode.SkewXDescription), typeof(TextNode))]
    [NumberPortControl(Min = -10, Max = 10, Default = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float SkewX
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.SkewYLabel), nameof(TextNode.SkewYDescription), typeof(TextNode))]
    [NumberPortControl(Min = -10, Max = 10, Default = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float SkewY
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.OriginXLabel), nameof(TextNode.OriginXDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000, Default = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float OriginX
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.OriginYLabel), nameof(TextNode.OriginYDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000, Default = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float OriginY
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.OutlinePortLabel), nameof(TextNode.PathTransformOutputDescription), typeof(TextNode))]
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

        if (Input?.Path is null)
        {
            Output = null;
            return Task.CompletedTask;
        }

        var path = new SKPath(Input.Path);
        var matrix = SKMatrix.CreateTranslation(-OriginX, -OriginY);

        // 拡大縮小
        matrix = SKMatrix.Concat(
            matrix,
            SKMatrix.CreateScale(ScaleX, ScaleY));

        // せん断
        matrix = SKMatrix.Concat(
            matrix,
            SKMatrix.CreateSkew(SkewX, SkewY));

        // 回転
        matrix = SKMatrix.Concat(
            matrix,
            SKMatrix.CreateRotationDegrees(Rotation));

        // 基準点を元の位置へ戻す
        matrix = SKMatrix.Concat(
            matrix,
            SKMatrix.CreateTranslation(OriginX, OriginY));

        // 平行移動
        matrix = SKMatrix.Concat(
            matrix,
            SKMatrix.CreateTranslation(TranslateX, TranslateY));

        path.Transform(matrix);

        _path = path;
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