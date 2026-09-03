using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Localize;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Utility;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.ValueTypes;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Effect.Composition;

public enum MaskMode
{
    [Display(Name = nameof(TextNode.ChannelHue), ResourceType = typeof(TextNode))]
    Hue,

    [Display(Name = nameof(TextNode.ChannelSaturation), ResourceType = typeof(TextNode))]
    Saturation,

    [Display(Name = nameof(TextNode.ChannelValue), ResourceType = typeof(TextNode))]
    Value,

    [Display(Name = nameof(TextNode.ChannelRed), ResourceType = typeof(TextNode))]
    Red,

    [Display(Name = nameof(TextNode.ChannelGreen), ResourceType = typeof(TextNode))]
    Green,

    [Display(Name = nameof(TextNode.ChannelBlue), ResourceType = typeof(TextNode))]
    Blue,

    [Display(Name = nameof(TextNode.ChannelAlpha), ResourceType = typeof(TextNode))]
    Alpha
}

[Node(typeof(CompositionCategory), nameof(TextNode.CreateMaskNode), nameof(TextNode.CreateMaskNodeDescription),
    typeof(TextNode))]
public class CreateMaskNode : NodeLogic
{
    private Guid _shaderId = Guid.Empty;
    private VideoEffectsLoader? _videoEffect;

    [InputPort(nameof(TextNode.InputImagePortLabel), nameof(TextNode.MaskInputImageDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.CornflowerBlue))]
    public ImageWrapper? InputImage
    {
        get => GetInput<ImageWrapper>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.MaskModeLabel), nameof(TextNode.MaskModeDescription), typeof(TextNode))]
    [EnumPortControl(Default = 0, IsEditable = false, Items = typeof(MaskMode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public int Mode
    {
        get => GetInput<int>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.MaskOffsetLabel), nameof(TextNode.MaskOffsetDescription), typeof(TextNode))]
    [NumberPortControl(Min = -40000, Max = 40000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Offset
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.InvertPortLabel), nameof(TextNode.MaskInvertDescription), typeof(TextNode))]
    [BoolPortControl]
    public bool Invert
    {
        get => GetInput<bool>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.MaskPortLabel), nameof(TextNode.MaskOutputDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.MediumPurple))]
    public MaskWrapper? Mask
    {
        get => GetOutput<MaskWrapper>();
        set => SetOutput(value);
    }

    public override void Dispose()
    {
        _videoEffect?.Dispose();
        _videoEffect = null;
        // _shaderId をリセットして次回 Calculate 時にシェーダーを再登録させる。
        _shaderId = Guid.Empty;
        base.Dispose();
    }

    protected override Task Calculate()
    {
        if (EvaluationContext is null) return Task.FromException(new NullReferenceException(nameof(EvaluationContext)));
        if (InputImage?.Image is null || InputImage.Image.NativePointer == nint.Zero)
            return Task.FromException(new NullReferenceException(nameof(InputImage)));

        if (_shaderId == Guid.Empty)
        {
            _shaderId = VideoEffectsLoader.RegisterShader("MaskCreate");
        }

        _videoEffect ??= VideoEffectsLoader.LoadEffectSync([
            (typeof(int), "Mode"),
            (typeof(float), "Offset"),
            (typeof(int), "Invert")
        ], _shaderId, EvaluationContext);

        _videoEffect
            .SetValue(
                Mode,
                Offset,
                Invert ? 1 : 0);
        Mask = _videoEffect.Update(out var output, EvaluationContext, InputImage?.Image)
            ? new MaskWrapper { Mask = output }
            : null;

        return Task.CompletedTask;
    }
}

[Node(typeof(CompositionCategory), nameof(TextNode.MaskClipNode), nameof(TextNode.MaskClipNodeDescription),
    typeof(TextNode))]
public class MaskClipNode : NodeLogic
{
    private Guid _shaderId = Guid.Empty;
    private VideoEffectsLoader? _videoEffect;

    [InputPort(nameof(TextNode.InputImagePortLabel), nameof(TextNode.MaskClipInputImageDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.CornflowerBlue))]
    public ImageWrapper? InputImage
    {
        get => GetInput<ImageWrapper>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.MaskPortLabel), nameof(TextNode.MaskClipMaskDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.MediumPurple))]
    public MaskWrapper? Mask
    {
        get => GetInput<MaskWrapper>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.InvertPortLabel), nameof(TextNode.MaskInvertDescription), typeof(TextNode))]
    [BoolPortControl]
    public bool Invert
    {
        get => GetInput<bool>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.OutputImagePortLabel), nameof(TextNode.MaskClipOutputDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.CornflowerBlue))]
    public ImageWrapper? Output
    {
        get => GetOutput<ImageWrapper>();
        set => SetOutput(value);
    }

    public override void Dispose()
    {
        _videoEffect?.Dispose();
        _videoEffect = null;
        _shaderId = Guid.Empty;
        base.Dispose();
    }

    protected override Task Calculate()
    {
        if (EvaluationContext is null) return Task.FromException(new NullReferenceException(nameof(EvaluationContext)));
        if (InputImage?.Image is null || InputImage.Image.NativePointer == nint.Zero)
            return Task.FromException(new NullReferenceException(nameof(InputImage)));
        if (Mask?.Mask is null || Mask.Mask.NativePointer == nint.Zero)
            return Task.FromException(new NullReferenceException(nameof(Mask)));

        if (_shaderId == Guid.Empty)
        {
            _shaderId = VideoEffectsLoader.RegisterShader("MaskClip");
        }

        _videoEffect ??= VideoEffectsLoader.LoadEffectSync([
            (typeof(int), "Invert")
        ], _shaderId, EvaluationContext, 2);

        _videoEffect
            .SetValue(
                Invert ? 1 : 0);
        Output = _videoEffect.Update(out var output, EvaluationContext, InputImage?.Image, Mask?.Mask)
            ? new ImageWrapper { Image = output }
            : null;

        return Task.CompletedTask;
    }
}

[Node(typeof(CompositionCategory), nameof(TextNode.MaskThresholdNode), nameof(TextNode.MaskThresholdNodeDescription),
    typeof(TextNode))]
public class MaskThresholdNode : NodeLogic
{
    private Guid _shaderId = Guid.Empty;
    private VideoEffectsLoader? _videoEffect;

    [InputPort(nameof(TextNode.MaskThresholdInputLabel), nameof(TextNode.MaskThresholdInputDescription),
        typeof(TextNode))]
    [PortColorSetting(nameof(Colors.MediumPurple))]
    public MaskWrapper? InputMask
    {
        get => GetInput<MaskWrapper>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.MaskMinLabel), nameof(TextNode.MaskMinDescription), typeof(TextNode))]
    [NumberPortControl(Min = 0, Max = 100, Digits = 1, Unit = "%")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Min
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.MaskMaxLabel), nameof(TextNode.MaskMaxDescription), typeof(TextNode))]
    [NumberPortControl(Min = 0, Max = 100, Digits = 1, Unit = "%", Default = 100f)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Max
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.InvertPortLabel), nameof(TextNode.MaskInvertDescription), typeof(TextNode))]
    [BoolPortControl]
    public bool Invert
    {
        get => GetInput<bool>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.MaskThresholdOutputLabel), nameof(TextNode.MaskThresholdOutputDescription),
        typeof(TextNode))]
    [PortColorSetting(nameof(Colors.MediumPurple))]
    public MaskWrapper? OutputMask
    {
        get => GetOutput<MaskWrapper>();
        set => SetOutput(value);
    }

    public override void Dispose()
    {
        _videoEffect?.Dispose();
        _videoEffect = null;
        _shaderId = Guid.Empty;
        base.Dispose();
    }

    protected override Task Calculate()
    {
        if (EvaluationContext is null) return Task.FromException(new NullReferenceException(nameof(EvaluationContext)));
        if (InputMask?.Mask is null || InputMask.Mask.NativePointer == nint.Zero)
            return Task.FromException(new NullReferenceException(nameof(InputMask)));

        if (_shaderId == Guid.Empty)
        {
            _shaderId = VideoEffectsLoader.RegisterShader("MaskThreshold");
        }

        _videoEffect ??= VideoEffectsLoader.LoadEffectSync([
            (typeof(float), "Min"),
            (typeof(float), "Max"),
            (typeof(int), "Invert")
        ], _shaderId, EvaluationContext);

        _videoEffect
            .SetValue(
                Min / 100f,
                Max / 100f,
                Invert ? 1 : 0);
        OutputMask = _videoEffect.Update(out var output, EvaluationContext, InputMask?.Mask)
            ? new MaskWrapper { Mask = output }
            : null;

        return Task.CompletedTask;
    }
}

[Node(typeof(CompositionCategory), nameof(TextNode.MaskToImageNode), nameof(TextNode.MaskToImageNodeDescription),
    typeof(TextNode))]
public class MaskToImageNode : NodeLogic
{
    [InputPort(nameof(TextNode.MaskPortLabel), nameof(TextNode.MaskDevelopTargetDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.MediumPurple))]
    public MaskWrapper? Mask
    {
        get => GetInput<MaskWrapper>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.MaskImageOutputLabel), nameof(TextNode.MaskImageOutputDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.CornflowerBlue))]
    public ImageWrapper? Output
    {
        get => GetOutput<ImageWrapper>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        if (Mask?.Mask is null)
            return Task.FromException(new NullReferenceException(nameof(Mask)));

        Output = new ImageWrapper { Image = Mask.Mask };
        return Task.CompletedTask;
    }
}