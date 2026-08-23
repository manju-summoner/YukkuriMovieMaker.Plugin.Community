using System.Numerics;
using System.Windows.Media;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Localize;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.ValueTypes;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Effect.Transform;

[Node(typeof(TransformCategory), nameof(TextNode.TranslateNode), nameof(TextNode.TranslateNodeDescription),
    typeof(TextNode))]
public class TranslateNode : NodeLogic
{
    private AffineTransform2D? _effect;
    private ID2D1Image? _effectOutput;

    [InputPort(nameof(TextNode.InputImagePortLabel), nameof(TextNode.TransformInputImageDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.CornflowerBlue))]
    public ImageWrapper? Input
    {
        get => GetInput<ImageWrapper>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.MoveAmountX), nameof(TextNode.MoveAmountXDescription), typeof(TextNode))]
    [NumberPortControl(Default = 0f, Digits = 2, Unit = "px")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float X
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.MoveAmountY), nameof(TextNode.MoveAmountYDescription), typeof(TextNode))]
    [NumberPortControl(Default = 0f, Digits = 2, Unit = "px")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Y
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.OutputImagePortLabel), nameof(TextNode.TransformOutputImageDescription),
        typeof(TextNode))]
    [PortColorSetting(nameof(Colors.CornflowerBlue))]
    public ImageWrapper? Output
    {
        get => GetOutput<ImageWrapper>();
        set => SetOutput(value);
    }

    public override void Dispose()
    {
        _effectOutput?.Dispose();
        _effectOutput = null;
        _effect?.Dispose();
        _effect = null;
        base.Dispose();
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

        _effectOutput?.Dispose();
        _effectOutput = _effect.Output;
        Output = new ImageWrapper { Image = _effectOutput };

        return Task.CompletedTask;
    }
}

[Node(typeof(TransformCategory), nameof(TextNode.ScaleNode), nameof(TextNode.ScaleNodeDescription), typeof(TextNode))]
public class ScaleNode : NodeLogic
{
    private AffineTransform2D? _effect;
    private ID2D1Image? _effectOutput;

    [InputPort(nameof(TextNode.InputImagePortLabel), nameof(TextNode.TransformInputImageDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.CornflowerBlue))]
    public ImageWrapper? Input
    {
        get => GetInput<ImageWrapper>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.ScaleRatioX), nameof(TextNode.ScaleRatioXDescription), typeof(TextNode))]
    [NumberPortControl(Min = 0f, Default = 1, Digits = 3, Unit = "x")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float ScaleX
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.ScaleRatioY), nameof(TextNode.ScaleRatioYDescription), typeof(TextNode))]
    [NumberPortControl(Min = 0f, Default = 1, Digits = 3, Unit = "x")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float ScaleY
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.OutputImagePortLabel), nameof(TextNode.TransformOutputImageDescription),
        typeof(TextNode))]
    [PortColorSetting(nameof(Colors.CornflowerBlue))]
    public ImageWrapper? Output
    {
        get => GetOutput<ImageWrapper>();
        set => SetOutput(value);
    }

    public override void Dispose()
    {
        _effectOutput?.Dispose();
        _effectOutput = null;
        _effect?.Dispose();
        _effect = null;
        base.Dispose();
    }

    protected override Task Calculate()
    {
        if (EvaluationContext is null)
            return Task.FromException(new NullReferenceException(nameof(EvaluationContext)));

        if (Input?.Image is null || Input.Image.NativePointer == nint.Zero)
            return Task.FromException(new NullReferenceException(nameof(Input)));

        _effect ??= new AffineTransform2D(EvaluationContext.Devices.DeviceContext);

        _effect.SetInput(0, Input.Image, false);
        _effect.TransformMatrix = Matrix3x2.CreateScale(ScaleX, ScaleY);

        _effectOutput?.Dispose();
        _effectOutput = _effect.Output;
        Output = new ImageWrapper { Image = _effectOutput };

        return Task.CompletedTask;
    }
}

[Node(typeof(TransformCategory), nameof(TextNode.RotateNode), nameof(TextNode.RotateNodeDescription), typeof(TextNode))]
public class RotateNode : NodeLogic
{
    private AffineTransform2D? _effect;
    private ID2D1Image? _effectOutput;

    [InputPort(nameof(TextNode.InputImagePortLabel), nameof(TextNode.TransformInputImageDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.CornflowerBlue))]
    public ImageWrapper? Input
    {
        get => GetInput<ImageWrapper>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.Angle), nameof(TextNode.AngleDescription), typeof(TextNode))]
    [NumberPortControl(Default = 0f, Digits = 2, Unit = "°")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Angle
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.OutputImagePortLabel), nameof(TextNode.TransformOutputImageDescription),
        typeof(TextNode))]
    [PortColorSetting(nameof(Colors.CornflowerBlue))]
    public ImageWrapper? Output
    {
        get => GetOutput<ImageWrapper>();
        set => SetOutput(value);
    }

    public override void Dispose()
    {
        _effectOutput?.Dispose();
        _effectOutput = null;
        _effect?.Dispose();
        _effect = null;
        base.Dispose();
    }

    protected override Task Calculate()
    {
        if (EvaluationContext is null)
            return Task.FromException(new NullReferenceException(nameof(EvaluationContext)));

        if (Input?.Image is null || Input.Image.NativePointer == nint.Zero)
            return Task.FromException(new NullReferenceException(nameof(Input)));

        _effect ??= new AffineTransform2D(EvaluationContext.Devices.DeviceContext);

        _effect.SetInput(0, Input.Image, false);
        _effect.TransformMatrix = Matrix3x2.CreateRotation(MathF.PI / 180 * Angle);

        _effectOutput?.Dispose();
        _effectOutput = _effect.Output;
        Output = new ImageWrapper { Image = _effectOutput };

        return Task.CompletedTask;
    }
}