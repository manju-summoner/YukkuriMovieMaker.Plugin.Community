using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Tritone;

[VideoEffect(nameof(Texts.TritoneEffectName), [VideoEffectCategories.Filtering], ["tritone", "tri-tone", "トライトーン", "三色调", "三色調", "트라이톤"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
public sealed class TritoneEffect : VideoEffectBase
{
    public override string Label => Texts.TritoneEffectName;

    [Display(GroupName = nameof(Texts.TritoneEffectName), Name = nameof(Texts.ShadowColorName), Description = nameof(Texts.ShadowColorDesc), Order = 100, ResourceType = typeof(Texts))]
    [ColorPicker]
    public Color ShadowColor { get => _shadowColor; set => Set(ref _shadowColor, value); }
    private Color _shadowColor = Color.FromArgb(255, 32, 24, 64);

    [Display(GroupName = nameof(Texts.TritoneEffectName), Name = nameof(Texts.MidtoneColorName), Description = nameof(Texts.MidtoneColorDesc), Order = 101, ResourceType = typeof(Texts))]
    [ColorPicker]
    public Color MidtoneColor { get => _midtoneColor; set => Set(ref _midtoneColor, value); }
    private Color _midtoneColor = Color.FromArgb(255, 192, 96, 96);

    [Display(GroupName = nameof(Texts.TritoneEffectName), Name = nameof(Texts.HighlightColorName), Description = nameof(Texts.HighlightColorDesc), Order = 102, ResourceType = typeof(Texts))]
    [ColorPicker]
    public Color HighlightColor { get => _highlightColor; set => Set(ref _highlightColor, value); }
    private Color _highlightColor = Color.FromArgb(255, 255, 240, 200);

    [Display(GroupName = nameof(Texts.TritoneEffectName), Name = nameof(Texts.MidPositionName), Description = nameof(Texts.MidPositionDesc), Order = 103, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", 0d, 100d)]
    public Animation MidPosition { get; } = new Animation(50, 1, 99);

    [Display(GroupName = nameof(Texts.TritoneEffectName), Name = nameof(Texts.OpacityName), Description = nameof(Texts.OpacityDesc), Order = 104, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", 0d, 100d)]
    public Animation Opacity { get; } = new Animation(100, 0, 100);

    [Display(GroupName = nameof(Texts.TritoneEffectName), Name = nameof(Texts.BlendModeName), Description = nameof(Texts.BlendModeDesc), Order = 105, ResourceType = typeof(Texts))]
    [EnumComboBox]
    public Blend BlendMode { get => _blendMode; set => Set(ref _blendMode, value); }
    private Blend _blendMode = Blend.Normal;

    public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

    public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        => new TritoneEffectProcessor(devices, this);

    protected override IEnumerable<IAnimatable> GetAnimatables() => [MidPosition, Opacity];
}
