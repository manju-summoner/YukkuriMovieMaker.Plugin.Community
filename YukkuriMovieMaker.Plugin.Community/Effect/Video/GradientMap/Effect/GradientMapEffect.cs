using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Localization;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Brush;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Models;
using YukkuriMovieMaker.Plugin.Effects;
using GradientStop = YukkuriMovieMaker.Brush.GradientStop;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Effect;

[VideoEffect(nameof(Texts.EffectName), [VideoEffectCategories.Filtering], ["gradient map", "グラデーションマップ", "渐变映射", "漸層對應", "그라디언트 맵"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
public sealed class GradientMapEffect : VideoEffectBase
{
    public override string Label => Texts.EffectName;

    [Display(
        GroupName = nameof(Texts.GroupName),
        Name = nameof(Texts.CustomGradientName),
        Description = nameof(Texts.CustomGradientDesc),
        ResourceType = typeof(Texts),
        Order = -1)]
    [GradientEditor]
    [CustomGradientStopsVisible]
    public ImmutableList<GradientStop> CustomGradientStops
    {
        get => _customGradientStops;
        set => Set(ref _customGradientStops, value);
    }
    private ImmutableList<GradientStop> _customGradientStops = [
        new GradientStop(0, Colors.Black),
        new GradientStop(1, Colors.White),
    ];

    [Display(
        GroupName = nameof(Texts.GroupName),
        Name = nameof(Texts.GradientFilePathName),
        Description = nameof(Texts.GradientFilePathDesc),
        ResourceType = typeof(Texts),
        Order = 0)]
    [GradientMapFileSelector]
    public string GradientFilePath
    {
        get => _gradientFilePath;
        set => Set(ref _gradientFilePath, value);
    }
    private string _gradientFilePath = string.Empty;

    [Display(
        GroupName = nameof(Texts.GroupName),
        Name = nameof(Texts.GradientIndexName),
        Description = nameof(Texts.GradientIndexDesc),
        ResourceType = typeof(Texts),
        Order = 1)]
    [GrdIndexSelector]
    [GradientIndexVisible]
    public int GradientIndex
    {
        get => _gradientIndex;
        set => Set(ref _gradientIndex, Math.Max(0, value));
    }
    private int _gradientIndex;

    [Display(
        GroupName = nameof(Texts.GroupName),
        Name = nameof(Texts.OpacityName),
        Description = nameof(Texts.OpacityDesc),
        ResourceType = typeof(Texts),
        Order = 2)]
    [AnimationSlider("F1", "%", 0d, 100d)]
    public Animation Opacity { get; } = new Animation(100, 0, 100);

    [Display(
        GroupName = nameof(Texts.GroupName),
        Name = nameof(Texts.BlendModeName),
        Description = nameof(Texts.BlendModeDesc),
        ResourceType = typeof(Texts),
        Order = 3)]
    [EnumComboBox]
    public GradientBlendMode BlendMode
    {
        get => _blendMode;
        set => Set(ref _blendMode, value);
    }
    private GradientBlendMode _blendMode = GradientBlendMode.Normal;

    [Display(
        GroupName = nameof(Texts.GroupName),
        Name = nameof(Texts.IsHorizontalName),
        Description = nameof(Texts.IsHorizontalDesc),
        ResourceType = typeof(Texts),
        Order = 4)]
    [ToggleSlider]
    [IsHorizontalVisible]
    public bool IsHorizontal
    {
        get => _isHorizontal;
        set => Set(ref _isHorizontal, value);
    }
    private bool _isHorizontal = true;

    public GradientMapEffect()
    {
        SubscribeChildUndoRedoable(CustomGradientStops);
    }

    public override IEnumerable<string> CreateExoVideoFilters(
        int keyFrameIndex,
        ExoOutputDescription exoOutputDescription) => [];

    public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        => new GradientMapEffectProcessor(devices, this);

    protected override IEnumerable<IAnimatable> GetAnimatables() => [Opacity];
}
