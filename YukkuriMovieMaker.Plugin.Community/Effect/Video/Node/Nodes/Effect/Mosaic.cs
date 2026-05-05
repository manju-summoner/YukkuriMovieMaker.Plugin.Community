using System.Windows.Media;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Localize;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Utility;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.ValueTypes;
using YukkuriMovieMaker.Project.Effects;
using YukkuriMovieMaker.Resources.Localization;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Effect;

[Node("YMM4Key_EffectCategoryFilteringName", "MosaicEffectName", "MosaicEffectName", typeof(Texts))]
public class Mosaic : NodeLogic
{
    private VideoEffectsLoader _videoEffect = null!;

    [InputPort(nameof(TextUi.Input), "", typeof(TextUi))]
    [PortColorSetting(nameof(Colors.CornflowerBlue))]
    public ImageWrapper? InputImage
    {
        get => GetInput<ImageWrapper>();
        set => SetInput(value);
    }

    [InputPort("MosaicEffectMosaicTypeName", "MosaicEffectMosaicTypeDesc", typeof(Texts))]
    [EnumPortControl(Default = 0, IsEditable = false, Items = typeof(MosaicType))]
    public int Mode
    {
        get => GetInput<int>();
        set => SetInput(value);
    }

    [InputPort("SizeMosaicParameterBaseSizeName", "SizeMosaicParameterBaseSizeDesc", typeof(Texts))]
    [NumberPortControl(Min = 1.0f, Max = 2000.0f, Digits = 1, Unit = "px")]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Size
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextUi.Output), "", typeof(TextUi))]
    [PortColorSetting(nameof(Colors.CornflowerBlue))]
    public ImageWrapper? Output
    {
        get => GetOutput<ImageWrapper>();
        set => SetOutput(value);
    }

    protected override async Task Calculate()
    {
        if (EvaluationContext is null)
        {
            await Task.FromException(new NullReferenceException(nameof(EvaluationContext)));
            return;
        }

        if (InputImage?.Image is null || InputImage.Image.NativePointer == nint.Zero)
        {
            await Task.FromException(new NullReferenceException(nameof(InputImage)));
            return;
        }

        if (_videoEffect == null!) _videoEffect = await VideoEffectsLoader.LoadEffect("MosaicEffect");

        await _videoEffect.SetValue("MosaicType", (MosaicType)Mode);
        await _videoEffect.SetValue("Size", Size);
        if (_videoEffect.Update(out var output, EvaluationContext, InputImage?.Image))
            Output = new ImageWrapper { Image = output };
    }
}