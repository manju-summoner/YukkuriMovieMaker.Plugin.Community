using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Lut;

[VideoEffect(nameof(Texts.EffectName), [VideoEffectCategories.Filtering], ["lut", "cube", "LUT適用", "look up table", "ルックアップテーブル", "色查找表", "色彩查找表", "룩업 테이블"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
public sealed class LutEffect : VideoEffectBase
{
    public override string Label => Texts.EffectName;

    [Display(
        GroupName = nameof(Texts.GroupName),
        Name = nameof(Texts.FilePathName),
        Description = nameof(Texts.FilePathDesc),
        ResourceType = typeof(Texts),
        Order = 0)]
    [LutFileSelector]
    public string FilePath
    {
        get => _filePath;
        set => Set(ref _filePath, value);
    }
    private string _filePath = string.Empty;

    [Display(
        GroupName = nameof(Texts.GroupName),
        Name = nameof(Texts.OpacityName),
        Description = nameof(Texts.OpacityDesc),
        ResourceType = typeof(Texts),
        Order = 1)]
    [AnimationSlider("F1", "%", 0d, 100d)]
    public Animation Opacity { get; } = new Animation(100, 0, 100);

    [Display(
        GroupName = nameof(Texts.GroupName),
        Name = nameof(Texts.BlendModeName),
        Description = nameof(Texts.BlendModeDesc),
        ResourceType = typeof(Texts),
        Order = 2)]
    [EnumComboBox]
    public Blend BlendMode
    {
        get => _blendMode;
        set => Set(ref _blendMode, value);
    }
    private Blend _blendMode = Blend.Normal;

    [Display(
        GroupName = nameof(Texts.GroupName),
        Name = nameof(Texts.InterpolationName),
        Description = nameof(Texts.InterpolationDesc),
        ResourceType = typeof(Texts),
        Order = 3)]
    [EnumComboBox]
    public LutInterpolationMode Interpolation
    {
        get => _interpolation;
        set => Set(ref _interpolation, value);
    }
    private LutInterpolationMode _interpolation = LutInterpolationMode.Tetrahedral;

    public override IEnumerable<string> CreateExoVideoFilters(
        int keyFrameIndex,
        ExoOutputDescription exoOutputDescription) => [];

    public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        => new LutEffectProcessor(devices, this);

    protected override IEnumerable<IAnimatable> GetAnimatables() => [Opacity];
}
