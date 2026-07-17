using System.Numerics;
using System.Windows.Media;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.ValueTypes;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Effect.Transform;

[Node(typeof(TransformCategory), "平行移動", "画像を指定した距離だけ移動します。")]
public class TranslateNode : NodeLogic
{
    private AffineTransform2D? _effect;

    [InputPort("入力画像", "変換する画像")]
    [PortColorSetting(nameof(Colors.CornflowerBlue))]
    public ImageWrapper? Input
    {
        get => GetInput<ImageWrapper>();
        set => SetInput(value);
    }

    [InputPort("X", "X方向の移動量")]
    [NumberPortControl(Default = 0f, Digits = 2, Unit = "px")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float X
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("Y", "Y方向の移動量")]
    [NumberPortControl(Default = 0f, Digits = 2, Unit = "px")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Y
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort("出力画像", "変換結果")]
    [PortColorSetting(nameof(Colors.CornflowerBlue))]
    public ImageWrapper? Output
    {
        get => GetOutput<ImageWrapper>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        if (EvaluationContext is null)
            return Task.FromException(new NullReferenceException(nameof(EvaluationContext)));

        if (Input?.Image is null || Input.Image.NativePointer == nint.Zero)
            return Task.FromException(new NullReferenceException(nameof(Input)));

        _effect ??= new AffineTransform2D(EvaluationContext.Devices.DeviceContext);

        _effect.SetInput(0, Input.Image, false);
        _effect.TransformMatrix = Matrix3x2.CreateTranslation(X, Y);

        Output = new ImageWrapper
        {
            Image = _effect.Output
        };

        return Task.CompletedTask;
    }
}

[Node(typeof(TransformCategory), "拡大縮小", "画像を指定倍率で拡大縮小します。")]
public class ScaleNode : NodeLogic
{
    private AffineTransform2D? _effect;

    [InputPort("入力画像", "変換する画像")]
    [PortColorSetting(nameof(Colors.CornflowerBlue))]
    public ImageWrapper? Input
    {
        get => GetInput<ImageWrapper>();
        set => SetInput(value);
    }

    [InputPort("倍率X", "X方向倍率")]
    [NumberPortControl(Min = 0f, Default = 1, Digits = 3, Unit = "x")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float ScaleX
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("倍率Y", "Y方向倍率")]
    [NumberPortControl(Min = 0f, Default = 1, Digits = 3, Unit = "x")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float ScaleY
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort("出力画像", "変換結果")]
    [PortColorSetting(nameof(Colors.CornflowerBlue))]
    public ImageWrapper? Output
    {
        get => GetOutput<ImageWrapper>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        if (EvaluationContext is null)
            return Task.FromException(new NullReferenceException(nameof(EvaluationContext)));

        if (Input?.Image is null || Input.Image.NativePointer == nint.Zero)
            return Task.FromException(new NullReferenceException(nameof(Input)));

        _effect ??= new AffineTransform2D(EvaluationContext.Devices.DeviceContext);

        _effect.SetInput(0, Input.Image, false);
        _effect.TransformMatrix = Matrix3x2.CreateScale(
            ScaleX,
            ScaleY
        );

        Output = new ImageWrapper
        {
            Image = _effect.Output
        };

        return Task.CompletedTask;
    }
}

[Node(typeof(TransformCategory), "回転", "画像を指定角度回転します。")]
public class RotateNode : NodeLogic
{
    private AffineTransform2D? _effect;

    [InputPort("入力画像", "変換する画像")]
    [PortColorSetting(nameof(Colors.CornflowerBlue))]
    public ImageWrapper? Input
    {
        get => GetInput<ImageWrapper>();
        set => SetInput(value);
    }

    [InputPort("角度", "回転角度")]
    [NumberPortControl(Default = 0f, Digits = 2, Unit = "°")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Angle
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort("出力画像", "変換結果")]
    [PortColorSetting(nameof(Colors.CornflowerBlue))]
    public ImageWrapper? Output
    {
        get => GetOutput<ImageWrapper>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        if (EvaluationContext is null)
            return Task.FromException(new NullReferenceException(nameof(EvaluationContext)));

        if (Input?.Image is null || Input.Image.NativePointer == nint.Zero)
            return Task.FromException(new NullReferenceException(nameof(Input)));

        _effect ??= new AffineTransform2D(EvaluationContext.Devices.DeviceContext);

        _effect.SetInput(0, Input.Image, false);

        _effect.TransformMatrix =
            Matrix3x2.CreateRotation(
                MathF.PI / 180 * Angle
            );

        Output = new ImageWrapper
        {
            Image = _effect.Output
        };

        return Task.CompletedTask;
    }
}