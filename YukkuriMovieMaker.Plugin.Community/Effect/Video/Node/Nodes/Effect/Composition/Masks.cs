using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Utility;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.ValueTypes;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Effect.Composition;

public enum MaskMode
{
    [Display(Name = "色相")] Hue,
    [Display(Name = "彩度")] Saturation,
    [Display(Name = "明度")] Value,
    [Display(Name = "赤")] Red,
    [Display(Name = "緑")] Green,
    [Display(Name = "青")] Blue,
    [Display(Name = "透明度")] Alpha
}

[Node(typeof(CompositionCategory), "マスク生成", "指定した画像の要素から画像を生成します")]
public class CreateMaskNode : NodeLogic
{
    private Guid _shaderId = Guid.Empty;
    private VideoEffectsLoader _videoEffect = null!;

    [InputPort("入力画像", "マスク生成を行う基画像")]
    [PortColorSetting(nameof(Colors.CornflowerBlue))]
    public ImageWrapper? InputImage
    {
        get => GetInput<ImageWrapper>();
        set => SetInput(value);
    }

    [InputPort("モード", "Mode")]
    [EnumPortControl(Default = 0, IsEditable = false, Items = typeof(MaskMode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public int Mode
    {
        get => GetInput<int>();
        set => SetInput(value);
    }

    [InputPort("オフセット", "Offset")]
    [NumberPortControl(Min = -40000, Max = 40000)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Offset
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("反転", "Invert")]
    [BoolPortControl]
    public bool Invert
    {
        get => GetInput<bool>();
        set => SetInput(value);
    }

    [OutputPort("マスク", "Mask")]
    [PortColorSetting(nameof(Colors.MediumPurple))]
    public MaskWrapper? Mask
    {
        get => GetOutput<MaskWrapper>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        if (EvaluationContext is null) return Task.FromException(new NullReferenceException(nameof(EvaluationContext)));
        if (InputImage?.Image is null || InputImage.Image.NativePointer == nint.Zero)
            return Task.FromException(new NullReferenceException(nameof(InputImage)));

        if (_shaderId == Guid.Empty)
        {
            _shaderId = VideoEffectsLoader.RegisterShader("MaskCreate");
            _videoEffect = VideoEffectsLoader.LoadEffectSync([
                (typeof(int), "Mode"),
                (typeof(float), "Offset"),
                (typeof(int), "Invert")
            ], _shaderId, EvaluationContext);
        }

        _videoEffect
            .SetValue(
                Mode,
                Offset,
                Invert ? 1 : 0);
        if (_videoEffect.Update(out var output, EvaluationContext, InputImage?.Image))
            Mask = new MaskWrapper { Mask = output };

        return Task.CompletedTask;
    }
}

[Node(typeof(CompositionCategory), "マスククリップ", "マスクで画像をクリップします")]
public class MaskClipNode : NodeLogic
{
    private Guid _shaderId = Guid.Empty;
    private VideoEffectsLoader _videoEffect = null!;

    [InputPort("入力画像", "マスクによるクリップの対象画像")]
    [PortColorSetting(nameof(Colors.CornflowerBlue))]
    public ImageWrapper? InputImage
    {
        get => GetInput<ImageWrapper>();
        set => SetInput(value);
    }

    [InputPort("マスク", "クリップするマスク")]
    [PortColorSetting(nameof(Colors.MediumPurple))]
    public MaskWrapper? Mask
    {
        get => GetInput<MaskWrapper>();
        set => SetInput(value);
    }

    [InputPort("反転", "Invert")]
    [BoolPortControl]
    public bool Invert
    {
        get => GetInput<bool>();
        set => SetInput(value);
    }

    [OutputPort("出力画像", "マスクでクリップした結果")]
    [PortColorSetting(nameof(Colors.CornflowerBlue))]
    public ImageWrapper? Output
    {
        get => GetOutput<ImageWrapper>();
        set => SetOutput(value);
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
            _videoEffect = VideoEffectsLoader.LoadEffectSync([
                (typeof(int), "Invert")
            ], _shaderId, EvaluationContext, 2);
        }

        _videoEffect
            .SetValue(
                Invert ? 1 : 0);
        if (_videoEffect.Update(out var output, EvaluationContext, InputImage?.Image, Mask?.Mask))
            Output = new ImageWrapper { Image = output };

        return Task.CompletedTask;
    }
}

[Node(typeof(CompositionCategory), "マスクの閾値", "マスクの強度の範囲を設定します")]
public class MaskThresholdNode : NodeLogic
{
    private Guid _shaderId = Guid.Empty;
    private VideoEffectsLoader _videoEffect = null!;

    [InputPort("入力マスク", "閾値の設定対象のマスク")]
    [PortColorSetting(nameof(Colors.MediumPurple))]
    public MaskWrapper? InputMask
    {
        get => GetInput<MaskWrapper>();
        set => SetInput(value);
    }

    [InputPort("最小値", "Min")]
    [NumberPortControl(Min = 0, Max = 100, Digits = 1, Unit = "%")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Min
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("最大値", "Max")]
    [NumberPortControl(Min = 0, Max = 100, Digits = 1, Unit = "%")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Max
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort("反転", "Invert")]
    [BoolPortControl]
    public bool Invert
    {
        get => GetInput<bool>();
        set => SetInput(value);
    }

    [OutputPort("出力マスク", "Mask")]
    [PortColorSetting(nameof(Colors.MediumPurple))]
    public MaskWrapper? OutputMask
    {
        get => GetOutput<MaskWrapper>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        if (EvaluationContext is null) return Task.FromException(new NullReferenceException(nameof(EvaluationContext)));
        if (InputMask?.Mask is null || InputMask.Mask.NativePointer == nint.Zero)
            return Task.FromException(new NullReferenceException(nameof(InputMask)));

        if (_shaderId == Guid.Empty)
        {
            _shaderId = VideoEffectsLoader.RegisterShader("MaskThreshold");
            _videoEffect = VideoEffectsLoader.LoadEffectSync([
                (typeof(float), "Min"),
                (typeof(float), "Max"),
                (typeof(int), "Invert")
            ], _shaderId, EvaluationContext);
        }

        _videoEffect
            .SetValue(
                Min / 100f,
                Max / 100f,
                Invert ? 1 : 0);
        if (_videoEffect.Update(out var output, EvaluationContext, InputMask?.Mask))
            OutputMask = new MaskWrapper { Mask = output };

        return Task.CompletedTask;
    }
}

[Node(typeof(CompositionCategory), "マスク現像", "マスクの強度を白黒画像に落とし込みます")]
public class MaskToImageNode : NodeLogic
{
    [InputPort("マスク", "現像対象のマスク")]
    [PortColorSetting(nameof(Colors.MediumPurple))]
    public MaskWrapper? Mask
    {
        get => GetInput<MaskWrapper>();
        set => SetInput(value);
    }

    [OutputPort("画像", "マスクの現像画像")]
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