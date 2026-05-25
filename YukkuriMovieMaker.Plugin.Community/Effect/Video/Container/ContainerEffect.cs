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
    static readonly Guid _initialGuid = Guid.NewGuid();
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

    public ImmutableList<EffectTab> Tabs
    {
        get => _tabs;
        set
        {
            if (Set(ref _tabs, value))
                OnPropertyChanged(nameof(Label));
        }
    }
    private ImmutableList<EffectTab> _tabs = [new EffectTab() { Id = _initialGuid }];

    public Guid? SelectedTabId
    {
        get => _selectedTabId;
        set
        {
            if (Set(ref _selectedTabId, value))
                OnPropertyChanged(nameof(Label));
        }
    }
    private Guid? _selectedTabId = _initialGuid;

    public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices) =>
        new ContainerEffectProcessor(this, devices);

    public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) =>
        [];

    protected override IEnumerable<IAnimatable> GetAnimatables() => Tabs;

    /// <summary>
    /// 現在選択中のタブの Effects を返す。レンダリングパイプラインや UI バインディング元として使用。
    /// 該当タブが無いときは空リストを返す。
    /// </summary>
    internal ImmutableList<IVideoEffect> GetSelectedTabEffects()
    {
        var selectedTab = Tabs.FirstOrDefault(t => t.Id == SelectedTabId);
        return selectedTab?.Effects ?? ImmutableList<IVideoEffect>.Empty;
    }
}
