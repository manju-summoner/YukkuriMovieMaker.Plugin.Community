using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Localize;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.ValueTypes;
using Blend = Vortice.Direct2D1.Effects.Blend;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Effect.Composition;

public enum BlendMode
{
    [Display(Name = nameof(TextNode.BlendNormal), ResourceType = typeof(TextNode))]
    Normal,

    [Display(Name = nameof(TextNode.BlendMultiply), ResourceType = typeof(TextNode))]
    Multiply,

    [Display(Name = nameof(TextNode.BlendScreen), ResourceType = typeof(TextNode))]
    Screen,

    [Display(Name = nameof(TextNode.BlendDarken), ResourceType = typeof(TextNode))]
    Darken,

    [Display(Name = nameof(TextNode.BlendLighten), ResourceType = typeof(TextNode))]
    Lighten,

    [Display(Name = nameof(TextNode.BlendDissolve), ResourceType = typeof(TextNode))]
    Dissolve,

    [Display(Name = nameof(TextNode.BlendColorBurn), ResourceType = typeof(TextNode))]
    ColorBurn,

    [Display(Name = nameof(TextNode.BlendLinearBurn), ResourceType = typeof(TextNode))]
    LinearBurn,

    [Display(Name = nameof(TextNode.BlendDarkerColor), ResourceType = typeof(TextNode))]
    DarkerColor,

    [Display(Name = nameof(TextNode.BlendLighterColor), ResourceType = typeof(TextNode))]
    LighterColor,

    [Display(Name = nameof(TextNode.BlendColorDodge), ResourceType = typeof(TextNode))]
    ColorDodge,

    [Display(Name = nameof(TextNode.BlendLinearDodge), ResourceType = typeof(TextNode))]
    LinearDodge,

    [Display(Name = nameof(TextNode.BlendOverlay), ResourceType = typeof(TextNode))]
    Overlay,

    [Display(Name = nameof(TextNode.BlendSoftLight), ResourceType = typeof(TextNode))]
    SoftLight,

    [Display(Name = nameof(TextNode.BlendHardLight), ResourceType = typeof(TextNode))]
    HardLight,

    [Display(Name = nameof(TextNode.BlendVividLight), ResourceType = typeof(TextNode))]
    VividLight,

    [Display(Name = nameof(TextNode.BlendLinearLight), ResourceType = typeof(TextNode))]
    LinearLight,

    [Display(Name = nameof(TextNode.BlendPinLight), ResourceType = typeof(TextNode))]
    PinLight,

    [Display(Name = nameof(TextNode.BlendHardMix), ResourceType = typeof(TextNode))]
    HardMix,

    [Display(Name = nameof(TextNode.BlendDifferenceAbs), ResourceType = typeof(TextNode))]
    Difference,

    [Display(Name = nameof(TextNode.BlendExclusion), ResourceType = typeof(TextNode))]
    Exclusion,

    [Display(Name = nameof(TextNode.ChannelHue), ResourceType = typeof(TextNode))]
    Hue,

    [Display(Name = nameof(TextNode.ChannelSaturation), ResourceType = typeof(TextNode))]
    Saturation,

    [Display(Name = nameof(TextNode.BlendColor), ResourceType = typeof(TextNode))]
    Color,

    [Display(Name = nameof(TextNode.BlendLuminosity), ResourceType = typeof(TextNode))]
    Luminosity,

    [Display(Name = nameof(TextNode.BlendSubtract), ResourceType = typeof(TextNode))]
    Subtract,

    [Display(Name = nameof(TextNode.BlendDivision), ResourceType = typeof(TextNode))]
    Division
}

[Node(typeof(CompositionCategory), nameof(TextNode.BlendNode), nameof(TextNode.BlendNodeDescription), typeof(TextNode))]
public class BlendNode : NodeLogic
{
    private Blend? _blendEffect;
    private Composite? _compositeEffect;
    private ID2D1Image? _effectOutput;

    [InputPort(nameof(TextNode.InputImagePortLabel), nameof(TextNode.BlendInputImage1Description), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.CornflowerBlue))]
    public ImageWrapper? InputImage1
    {
        get => GetInput<ImageWrapper>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.InputImagePortLabel), nameof(TextNode.BlendInputImage2Description), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.CornflowerBlue))]
    public ImageWrapper? InputImage2
    {
        get => GetInput<ImageWrapper>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.MaskModeLabel), nameof(TextNode.BlendModeDescription), typeof(TextNode))]
    [EnumPortControl(Default = 0, IsEditable = false, Items = typeof(BlendMode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public int Mode
    {
        get => GetInput<int>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.OutputImagePortLabel), nameof(TextNode.BlendOutputDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.MediumPurple))]
    public ImageWrapper? Output
    {
        get => GetOutput<ImageWrapper>();
        set => SetOutput(value);
    }

    public override void Dispose()
    {
        _effectOutput?.Dispose();
        _effectOutput = null;
        _blendEffect?.Dispose();
        _blendEffect = null;
        _compositeEffect?.Dispose();
        _compositeEffect = null;
        base.Dispose();
    }

    protected override Task Calculate()
    {
        if (EvaluationContext is null) return Task.FromException(new NullReferenceException(nameof(EvaluationContext)));
        if (InputImage1?.Image is null || InputImage1.Image.NativePointer == nint.Zero)
            return Task.FromException(new NullReferenceException(nameof(InputImage1)));
        if (InputImage2?.Image is null || InputImage2.Image.NativePointer == nint.Zero)
            return Task.FromException(new NullReferenceException(nameof(InputImage2)));
        if (Mode == (int)BlendMode.Normal)
        {
            _compositeEffect ??= new Composite(EvaluationContext.Devices.DeviceContext);

            _compositeEffect.Mode = CompositeMode.SourceOver;
            _compositeEffect.SetInput(0, InputImage1.Image, false);
            _compositeEffect.SetInput(1, InputImage2.Image, true);

            _effectOutput?.Dispose();
            _effectOutput = _compositeEffect.Output;
            Output = new ImageWrapper { Image = _effectOutput };

            return Task.CompletedTask;
        }

        _blendEffect ??= new Blend(EvaluationContext.Devices.DeviceContext);
        _blendEffect.Mode = (Vortice.Direct2D1.BlendMode)(Mode - 1);
        _blendEffect.SetInput(0, InputImage1.Image, false);
        _blendEffect.SetInput(1, InputImage2.Image, true);
        _effectOutput?.Dispose();
        _effectOutput = _blendEffect.Output;
        Output = new ImageWrapper { Image = _effectOutput };

        return Task.CompletedTask;
    }
}