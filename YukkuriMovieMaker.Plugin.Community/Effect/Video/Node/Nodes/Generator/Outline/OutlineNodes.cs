using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using SkiaSharp;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Port;
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

public class ShapeCommonInputs : InputsContainer
{
    [InputPort("中心X", "図形の中心のX座標（直線の場合は始点）")]
    [NumberPortControl(Min = -40000, Max = 40000, Default = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float CenterX
    {
        get;
        set => Set(ref field, value);
    }

    [InputPort("中心Y", "図形の中心のY座標（直線の場合は始点）")]
    [NumberPortControl(Min = -40000, Max = 40000, Default = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float CenterY
    {
        get;
        set => Set(ref field, value);
    }

    [InputPort("幅", "図形の幅（直線の場合は終点までのX方向距離）")]
    [NumberPortControl(Min = -40000, Max = 40000, Default = 200)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Width
    {
        get;
        set => Set(ref field, value);
    }

    [InputPort("高さ", "図形の高さ（直線の場合は終点までのY方向距離）")]
    [NumberPortControl(Min = -40000, Max = 40000, Default = 200)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Height
    {
        get;
        set => Set(ref field, value);
    }

    [InputPort("回転", "図形を中心（始点）周りに回転させる角度")]
    [NumberPortControl(Min = -3600, Max = 3600, Default = 0, Unit = "deg")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Rotation
    {
        get;
        set => Set(ref field, value);
    }
}

public class RectInputs : ShapeCommonInputs
{
}

public class RoundRectInputs : ShapeCommonInputs
{
    private float _cornerRadius;

    [InputPort("角丸半径", "角丸矩形の角の半径")]
    [NumberPortControl(Min = 0, Max = 4000, Default = 20)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float CornerRadius
    {
        get => _cornerRadius;
        set => Set(ref _cornerRadius, value);
    }
}

public class OvalInputs : ShapeCommonInputs
{
}

public class PolygonInputs : ShapeCommonInputs
{
    private float _sides;

    [InputPort("頂点数", "多角形の頂点数")]
    [NumberPortControl(Min = 3, Max = 64, Digits = 0, Default = 5)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Sides
    {
        get => _sides;
        set => Set(ref _sides, value);
    }
}

public class StarInputs : ShapeCommonInputs
{
    private float _innerRadiusRatio;
    private float _sides;

    [InputPort("頂点数", "星形の外側の頂点数")]
    [NumberPortControl(Min = 3, Max = 64, Digits = 0, Default = 5)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Sides
    {
        get => _sides;
        set => Set(ref _sides, value);
    }

    [InputPort("内径比", "星形の内側の頂点半径（外側に対する割合）")]
    [NumberPortControl(Min = 0, Max = 100, Default = 50, Unit = "%")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float InnerRadiusRatio
    {
        get => _innerRadiusRatio;
        set => Set(ref _innerRadiusRatio, value);
    }
}

public class LineInputs : ShapeCommonInputs
{
}

[Node(typeof(OutlineCategory), "図形アウトライン生成", "指定した種類の図形のアウトラインデータを生成します。")]
public class ShapeOutlineNode : NodeLogic
{
    private readonly LineInputs _lineInputs = new();
    private readonly OvalInputs _ovalInputs = new();
    private readonly PolygonInputs _polygonInputs = new();
    private readonly RectInputs _rectInputs = new();
    private readonly RoundRectInputs _roundRectInputs = new();
    private readonly StarInputs _starInputs = new();

    private ShapeKind _kind = ShapeKind.Rect;
    private SKPath? _path;

    [InputPort("種類", "生成する図形の種類")]
    [EnumPortControl(Default = 0, IsEditable = false, Items = typeof(ShapeKind))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public ShapeKind Kind
    {
        get => GetInput<ShapeKind>();
        set => SetInput(value);
    }


    [InputPort("パラメータ", "図形の種類に応じた入力群", isDynamic: true)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public InputsContainer Params
    {
        get => GetCurrentInputs();
        set => SetDynamicContainer(value);
    }

    [OutputPort("アウトライン", "生成された図形のアウトラインデータ")]
    [PortColorSetting(nameof(Colors.MediumPurple))]
    public OutlineWrapper? Output
    {
        get => GetOutput<OutlineWrapper>();
        set => SetOutput(value);
    }

    protected internal override void OnInputValueChanged(string portName, object? value)
    {
        if (portName != nameof(Kind))
            return;

        var kind = value is ShapeKind shapeKind
            ? shapeKind
            : (ShapeKind)Convert.ToInt32(value);

        if (kind == _kind)
            return;

        _kind = kind;

        SetDynamicContainer(GetCurrentInputs(), nameof(Params));
    }

    private InputsContainer GetCurrentInputs()
    {
        return _kind switch
        {
            ShapeKind.Rect => _rectInputs,
            ShapeKind.RoundRect => _roundRectInputs,
            ShapeKind.Oval => _ovalInputs,
            ShapeKind.Polygon => _polygonInputs,
            ShapeKind.Star => _starInputs,
            ShapeKind.Line => _lineInputs,
            _ => _rectInputs
        };
    }

    protected override Task Calculate()
    {
        var centerX = GetDynamicValue<float>("CenterX");
        var centerY = GetDynamicValue<float>("CenterY");
        var width = GetDynamicValue<float>("Width");
        var height = GetDynamicValue<float>("Height");
        var rotation = GetDynamicValue<float>("Rotation");

        var builder = new SKPathBuilder();

        switch (_kind)
        {
            case ShapeKind.Rect:
            {
                builder.AddRect(
                    new SKRect(centerX - width / 2f, centerY - height / 2f, centerX + width / 2f,
                        centerY + height / 2f));

                break;
            }

            case ShapeKind.RoundRect:
            {
                var cornerRadius = GetDynamicValue<float>("CornerRadius");

                builder.AddRoundRect(
                    new SKRect(centerX - width / 2f, centerY - height / 2f, centerX + width / 2f,
                        centerY + height / 2f),
                    System.Math.Max(0, cornerRadius), System.Math.Max(0, cornerRadius));

                break;
            }

            case ShapeKind.Oval:
            {
                builder.AddOval(
                    new SKRect(centerX - width / 2f, centerY - height / 2f, centerX + width / 2f,
                        centerY + height / 2f));

                break;
            }

            case ShapeKind.Polygon:
            {
                var sides = System.Math.Max(3, (int)GetDynamicValue<float>("Sides"));

                builder.AddPoly(
                    BuildStarPoints(centerX, centerY, width / 2f, height / 2f,
                        width / 2f, height / 2f, sides));

                break;
            }
            case ShapeKind.Star:
            {
                var sides = System.Math.Max(3, (int)GetDynamicValue<float>("Sides"));
                var innerRadiusRatio = System.Math.Clamp(
                    GetDynamicValue<float>("InnerRadiusRatio") / 100f, 0f, 1f);

                builder.AddPoly(
                    BuildStarPoints(centerX, centerY, width / 2f, height / 2f,
                        width / 2f * innerRadiusRatio, height / 2f * innerRadiusRatio, sides * 2));

                break;
            }
            case ShapeKind.Line:
            {
                builder.MoveTo(centerX, centerY);
                builder.LineTo(centerX + width, centerY + height);

                break;
            }
        }

        var path = builder.Detach();

        if (rotation != 0)
            path.Transform(SKMatrix.CreateRotationDegrees(rotation, centerX, centerY));

        _path?.Dispose();
        _path = path;
        Output = new OutlineWrapper { Path = _path };

        return Task.CompletedTask;
    }

    private T GetDynamicValue<T>(string propertyName)
    {
        var portName = $"Params.{propertyName}";

        if (!Inputs.TryGetValue(portName, out var port))
            return default!;

        var value = port
            .GetValue(EvaluationContext)
            .GetAwaiter()
            .GetResult();

        if (value is null)
            return default!;
        if (value is T typed)
            return typed;

        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return default!;
        }
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