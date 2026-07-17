using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.ValueTypes;
using Blend = Vortice.Direct2D1.Effects.Blend;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Effect.Composition;

public enum BlendMode
{
    [Display(Name = "通常")] Normal,
    [Display(Name = "乗算")] Multiply,
    [Display(Name = "スクリーン")] Screen,
    [Display(Name = "比較（暗）")] Darken,
    [Display(Name = "比較（明）")] Lighten,
    [Display(Name = "ディザ")] Dissolve,
    [Display(Name = "焼き込みカラー")] ColorBurn,
    [Display(Name = "焼き込み（リニア）")] LinearBurn,
    [Display(Name = "暗いカラー")] DarkerColor,
    [Display(Name = "明るいカラー")] LighterColor,
    [Display(Name = "覆い焼きカラー")] ColorDodge,
    [Display(Name = "覆い焼き（リニア）")] LinearDodge,
    [Display(Name = "オーバーレイ")] Overlay,
    [Display(Name = "ソフトライト")] SoftLight,
    [Display(Name = "ハードライト")] HardLight,
    [Display(Name = "ビビッドライト")] VividLight,
    [Display(Name = "リニアライト")] LinearLight,
    [Display(Name = "ピンライト")] PinLight,
    [Display(Name = "ハードミックス")] HardMix,
    [Display(Name = "差の絶対値")] Difference,
    [Display(Name = "除外")] Exclusion,
    [Display(Name = "色相")] Hue,
    [Display(Name = "彩度")] Saturation,
    [Display(Name = "カラー")] Color,
    [Display(Name = "輝度")] Luminosity,
    [Display(Name = "減算")] Subtract,
    [Display(Name = "除算")] Division
}

[Node(typeof(CompositionCategory), "合成", "2枚の画像を指定したモードで合成します。")]
public class BlendNode : NodeLogic
{
    private Blend? _blendEffect;
    private Composite? _compositeEffect;

    [InputPort("入力画像1", "1つめの合成する画像")]
    [PortColorSetting(nameof(Colors.CornflowerBlue))]
    public ImageWrapper? InputImage1
    {
        get => GetInput<ImageWrapper>();
        set => SetInput(value);
    }

    [InputPort("入力画像2", "2つめの合成する画像")]
    [PortColorSetting(nameof(Colors.CornflowerBlue))]
    public ImageWrapper? InputImage2
    {
        get => GetInput<ImageWrapper>();
        set => SetInput(value);
    }

    [InputPort("モード", "合成モード")]
    [EnumPortControl(Default = 0, IsEditable = false, Items = typeof(BlendMode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public int Mode
    {
        get => GetInput<int>();
        set => SetInput(value);
    }

    [OutputPort("出力画像", "合成結果")]
    [PortColorSetting(nameof(Colors.MediumPurple))]
    public ImageWrapper? Output
    {
        get => GetOutput<ImageWrapper>();
        set => SetOutput(value);
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

            Output = new ImageWrapper
            {
                Image = _compositeEffect.Output
            };

            return Task.CompletedTask;
        }

        _blendEffect ??= new Blend(EvaluationContext.Devices.DeviceContext);
        _blendEffect.Mode = (Vortice.Direct2D1.BlendMode)(Mode - 1);
        _blendEffect.SetInput(0, InputImage1.Image, false);
        _blendEffect.SetInput(1, InputImage2.Image, true);
        Output = new ImageWrapper { Image = _blendEffect.Output };

        return Task.CompletedTask;
    }
}