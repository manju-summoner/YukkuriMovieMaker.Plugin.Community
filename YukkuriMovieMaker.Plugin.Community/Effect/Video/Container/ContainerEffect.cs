using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
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
            var selectedTab = Tabs.FirstOrDefault(t => t.Id == SelectedTabId);
            var selectedName = selectedTab?.Name ?? string.Empty;
            var count = selectedTab?.Effects.Count ?? 0;
            return string.Format(Texts.Container_LabelFormat, Texts.Container_DisplayName, selectedName, count);
        }
    }

    [Display(GroupName = nameof(Texts.Container_ManagementGroup), Name = nameof(Texts.Container_EmptyLabel), ResourceType = typeof(Texts))]
    [EffectTabManagerControl]
    [Newtonsoft.Json.JsonIgnore]
    public bool EffectTabManagerVisible { get; set; } = true;

    [Newtonsoft.Json.JsonIgnore]
    public ImmutableList<IVideoEffect> Effects
    {
        get => _effects;
        set
        {
            if (Set(ref _effects, value))
                OnPropertyChanged(nameof(Label));
        }
    }
    private ImmutableList<IVideoEffect> _effects = ImmutableList<IVideoEffect>.Empty;

    public ImmutableList<EffectTab> Tabs
    {
        get => _tabs;
        set
        {
            if (Set(ref _tabs, value))
                OnPropertyChanged(nameof(Label));
        }
    }
    private ImmutableList<EffectTab> _tabs = ImmutableList<EffectTab>.Empty;

    public Guid? SelectedTabId
    {
        get => _selectedTabId;
        set
        {
            if (Set(ref _selectedTabId, value))
                OnPropertyChanged(nameof(Label));
        }
    }
    private Guid? _selectedTabId;

    public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices) =>
        new ContainerEffectProcessor(this, devices);

    public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) =>
        [];

    protected override IEnumerable<IAnimatable> GetAnimatables() => Effects;
}
