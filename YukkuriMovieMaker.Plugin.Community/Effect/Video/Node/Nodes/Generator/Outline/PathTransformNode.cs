using System.Windows.Media;
using SkiaSharp;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.ValueTypes;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Generator.Outline;

[Node(
    typeof(OutlineCategory),
    "パス座標変換",
    "アウトラインに平行移動、拡大縮小、回転、せん断などの座標変換を適用します。")]
public class PathTransformNode : NodeLogic
{
    private SKPath? _path;

    [InputPort("アウトライン", "座標変換するアウトライン")]
    [PortColorSetting(nameof(Colors.MediumPurple))]
    public OutlineWrapper? Input
    {
        get => GetInput<OutlineWrapper>();
        set => SetInput(value);
    }

    [InputPort("移動X", "X方向への移動量")]
    [NumberPortControl(Min = -40000, Max = 40000, Default = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float TranslateX
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("移動Y", "Y方向への移動量")]
    [NumberPortControl(Min = -40000, Max = 40000, Default = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float TranslateY
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("拡大率X", "X方向の拡大率")]
    [NumberPortControl(Min = -100, Max = 100, Default = 1)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float ScaleX
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("拡大率Y", "Y方向の拡大率")]
    [NumberPortControl(Min = -100, Max = 100, Default = 1)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float ScaleY
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("回転", "回転角度")]
    [NumberPortControl(Min = -3600, Max = 3600, Default = 0, Unit = "deg")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Rotation
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("せん断X", "X方向のせん断量")]
    [NumberPortControl(Min = -10, Max = 10, Default = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float SkewX
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("せん断Y", "Y方向のせん断量")]
    [NumberPortControl(Min = -10, Max = 10, Default = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float SkewY
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("基準X", "拡大縮小・回転・せん断の基準となるX座標")]
    [NumberPortControl(Min = -40000, Max = 40000, Default = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float OriginX
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("基準Y", "拡大縮小・回転・せん断の基準となるY座標")]
    [NumberPortControl(Min = -40000, Max = 40000, Default = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float OriginY
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort("アウトライン", "座標変換後のアウトライン")]
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