using System.Windows.Media;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.ValueTypes;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Effect.Composition;

[Node(typeof(CompositionCategory), "不透明度", "画像の不透明度を設定します。")]
public class Alpha : NodeLogic
{
    private ID2D1Image? _effectOutput;
    private Opacity? _opacityEffect;

    [InputPort("入力画像", "不透明度の変更対象画像")]
    [PortColorSetting(nameof(Colors.CornflowerBlue))]
    public ImageWrapper? InputImage
    {
        get => GetInput<ImageWrapper>();
        set => SetInput(value);
    }

    [InputPort("不透明度", "画像の透過度")]
    [NumberPortControl(Min = 0f, Max = 100f, Default = 100f, Unit = "%")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Opacity
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort("出力画像", "透過結果")]
    [PortColorSetting(nameof(Colors.MediumPurple))]
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

        _opacityEffect ??= new Opacity(EvaluationContext.Devices.DeviceContext);
        _opacityEffect.Value = Opacity / 100f;
        _opacityEffect.SetInput(0, InputImage.Image, false);
        _effectOutput?.Dispose();
        _effectOutput = _opacityEffect.Output;
        Output = new ImageWrapper { Image = _effectOutput };

        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _effectOutput?.Dispose();
        _effectOutput = null;
        _opacityEffect?.Dispose();
        _opacityEffect = null;
        base.Dispose();
    }
}