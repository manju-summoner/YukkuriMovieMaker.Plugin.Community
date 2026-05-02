using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

[VideoEffect(nameof(Texts.VideoEffect_Name), [VideoEffectCategories.Decoration], [nameof(Texts.VideoEffect_Tag_Container)], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
public sealed class ContainerEffect : VideoEffectBase
{
    public override string Label
    {
        get
        {
            var count = Effects.Count;
            return string.Format(Texts.Container_LabelFormat, Texts.Container_DisplayName, SelectedTabName ?? string.Empty, count);
        }
    }

    public string? SelectedTabName
    {
        get => _selectedTabName;
        set
        {
            if (Set(ref _selectedTabName, value))
                OnPropertyChanged(nameof(Label));
        }
    }
    private string? _selectedTabName;

    [Display(GroupName = nameof(Texts.Container_ManagementGroup), Name = nameof(Texts.Container_EmptyLabel), ResourceType = typeof(Texts))]
    [EffectTabManagerControl]
    [Newtonsoft.Json.JsonIgnore]
    public bool EffectTabManagerVisible { get; set; } = true;

    [Display(GroupName = nameof(Texts.Container_ManagementGroup), Name = nameof(Texts.Container_EmptyLabel), ResourceType = typeof(Texts))]
    [VideoEffectSelector(PropertyEditorSize = PropertyEditorSize.FullWidth)]
    public ImmutableList<IVideoEffect> Effects
    {
        get => _effects;
        set
        {
            if (Set(ref _effects, value))
            {
                OnPropertyChanged(nameof(Label));
            }
        }
    }
    private ImmutableList<IVideoEffect> _effects = ImmutableList<IVideoEffect>.Empty;

    public string? EffectTabsJson
    {
        get => _effectTabsJson;
        set => Set(ref _effectTabsJson, value);
    }
    private string? _effectTabsJson;

    public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices) =>
        new ContainerEffectProcessor(this, devices);

    public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) =>
        [];

    protected override IEnumerable<IAnimatable> GetAnimatables() => Effects;
}
