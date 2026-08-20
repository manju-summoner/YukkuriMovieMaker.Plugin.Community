using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using SkiaSharp;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.ValueTypes;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Generator.Outline;

public enum ShapeKind
{
    [Display(Name = "矩形")] Rect,
    [Display(Name = "角丸矩形")] RoundRect,
    [Display(Name = "楕円")] Oval,
    [Display(Name = "多角形")] Polygon,
    [Display(Name = "星形")] Star,
    [Display(Name = "直線")] Line
}

[Node(typeof(OutlineCategory), "図形アウトライン生成", "指定した種類の図形のアウトラインデータを生成します。")]
public class ShapeOutlineNode : NodeLogic
{
    private SKPath? _path;

    [InputPort("種類", "生成する図形の種類")]
    [EnumPortControl(Default = 0, IsEditable = false, Items = typeof(ShapeKind))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public int Kind
    {
        get => GetInput<int>();
        set => SetInput(value);
    }

    [InputPort("中心X", "図形の中心のX座標（直線の場合は始点）")]
    [NumberPortControl(Min = -40000, Max = 40000, Default = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float CenterX
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("中心Y", "図形の中心のY座標（直線の場合は始点）")]
    [NumberPortControl(Min = -40000, Max = 40000, Default = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float CenterY
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("幅", "図形の幅（直線の場合は終点までのX方向距離）")]
    [NumberPortControl(Min = -40000, Max = 40000, Default = 200)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Width
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("高さ", "図形の高さ（直線の場合は終点までのY方向距離）")]
    [NumberPortControl(Min = -40000, Max = 40000, Default = 200)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Height
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("角丸半径", "角丸矩形の角の半径")]
    [NumberPortControl(Min = 0, Max = 4000, Default = 20)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float CornerRadius
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("頂点数", "多角形・星形の頂点数")]
    [NumberPortControl(Min = 3, Max = 64, Digits = 0, Default = 5)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Sides
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("内径比", "星形の内側の頂点半径（外側に対する割合）")]
    [NumberPortControl(Min = 0, Max = 100, Default = 50, Unit = "%")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float InnerRadiusRatio
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("回転", "図形を中心（始点）周りに回転させる角度")]
    [NumberPortControl(Min = -3600, Max = 3600, Default = 0, Unit = "deg")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Rotation
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort("アウトライン", "生成された図形のアウトラインデータ")]
    [PortColorSetting(nameof(Colors.MediumPurple))]
    public OutlineWrapper? Output
    {
        get => GetOutput<OutlineWrapper>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        var builder = new SKPathBuilder();

        switch ((ShapeKind)Kind)
        {
            case ShapeKind.Rect:
                builder.AddRect(new SKRect(CenterX - Width / 2f, CenterY - Height / 2f, CenterX + Width / 2f,
                    CenterY + Height / 2f));
                break;
            case ShapeKind.RoundRect:
                builder.AddRoundRect(
                    new SKRect(CenterX - Width / 2f, CenterY - Height / 2f, CenterX + Width / 2f,
                        CenterY + Height / 2f),
                    System.Math.Max(0, CornerRadius), System.Math.Max(0, CornerRadius));
                break;
            case ShapeKind.Oval:
                builder.AddOval(new SKRect(CenterX - Width / 2f, CenterY - Height / 2f, CenterX + Width / 2f,
                    CenterY + Height / 2f));
                break;
            case ShapeKind.Polygon:
                builder.AddPoly(BuildStarPoints(CenterX, CenterY, Width / 2f, Height / 2f, Width / 2f, Height / 2f,
                    System.Math.Max(3, (int)Sides)));
                break;
            case ShapeKind.Star:
                builder.AddPoly(BuildStarPoints(CenterX, CenterY, Width / 2f, Height / 2f,
                    Width / 2f * System.Math.Clamp(InnerRadiusRatio / 100f, 0f, 1f),
                    Height / 2f * System.Math.Clamp(InnerRadiusRatio / 100f, 0f, 1f),
                    System.Math.Max(3, (int)Sides) * 2));
                break;
            case ShapeKind.Line:
                builder.MoveTo(CenterX, CenterY);
                builder.LineTo(CenterX + Width, CenterY + Height);
                break;
        }

        var path = builder.Detach();
        if (Rotation != 0)
            path.Transform(SKMatrix.CreateRotationDegrees(Rotation, CenterX, CenterY));

        _path?.Dispose();
        _path = path;
        Output = new OutlineWrapper { Path = _path };

        return Task.CompletedTask;
    }

    private static SKPoint[] BuildStarPoints(float cx, float cy, float outerRx, float outerRy, float innerRx,
        float innerRy, int vertexCount)
    {
        var points = new SKPoint[vertexCount];
        for (var i = 0; i < vertexCount; i++)
        {
            var angle = MathF.PI / 2f + i * (2f * MathF.PI / vertexCount);
            var useOuter = i % 2 == 0;
            var rx = useOuter ? outerRx : innerRx;
            var ry = useOuter ? outerRy : innerRy;
            points[i] = new SKPoint(cx + rx * MathF.Cos(angle), cy - ry * MathF.Sin(angle));
        }

        return points;
    }

    public override void Dispose()
    {
        _path?.Dispose();
        _path = null;
        base.Dispose();
    }
}

[Node(typeof(OutlineCategory), "文字アウトライン生成", "指定した文字列のアウトラインデータを生成します。")]
public class TextOutlineNode : NodeLogic
{
    private SKPath? _path;

    [InputPort("テキスト", "アウトライン化する文字列")]
    [TextPortControl(Default = "テキスト")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public string Text
    {
        get => GetInput<string>() ?? "";
        set => SetInput(value);
    }

    [InputPort("フォント名", "使用するフォントファミリー名（空欄の場合は既定のフォント）")]
    [FontComboBox]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public string FontFamily
    {
        get
        {
            var name = GetInput<string>();
            if (string.IsNullOrEmpty(name))
                name = "メイリオ";
            return name;
        }
        set => SetInput(value);
    }

    [InputPort("サイズ", "フォントサイズ")]
    [NumberPortControl(Min = 1, Max = 2000, Default = 64)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Size
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("太字", "太字にするかどうか")]
    [BoolPortControl(Default = false)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public bool Bold
    {
        get => GetInput<bool>();
        set => SetInput(value);
    }

    [InputPort("斜体", "斜体にするかどうか")]
    [BoolPortControl(Default = false)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public bool Italic
    {
        get => GetInput<bool>();
        set => SetInput(value);
    }

    [InputPort("原点X", "文字列のベースライン原点のX座標")]
    [NumberPortControl(Min = -40000, Max = 40000, Default = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float OriginX
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("原点Y", "文字列のベースライン原点のY座標")]
    [NumberPortControl(Min = -40000, Max = 40000, Default = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float OriginY
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort("アウトライン", "生成された文字列のアウトラインデータ")]
    [PortColorSetting(nameof(Colors.MediumPurple))]
    public OutlineWrapper? Output
    {
        get => GetOutput<OutlineWrapper>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        var weight = Bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
        var slant = Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;

        using var typeface = SKTypeface.FromFamilyName(
            string.IsNullOrWhiteSpace(FontFamily) ? null : FontFamily,
            weight, SKFontStyleWidth.Normal, slant);
        using var font = new SKFont(typeface, System.Math.Max(1f, Size));

        var path = font.GetTextPath(Text, new SKPoint(OriginX, OriginY));

        _path?.Dispose();
        _path = path;
        Output = new OutlineWrapper { Path = _path };

        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _path?.Dispose();
        _path = null;
        base.Dispose();
    }
}