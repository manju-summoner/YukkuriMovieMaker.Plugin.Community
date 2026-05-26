using System.Collections.Immutable;
using System.ComponentModel;
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
            // タブ名が null/empty の場合は「タブ 1」表記にフォールバックする。
            var rawName = selectedTab?.Name;
            var selectedName = string.IsNullOrEmpty(rawName)
                ? string.Format(Texts.EffectTab_NumberedName, 1)
                : rawName;
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
        get;
        set
        {
            var oldTabs = field;
            if (Set(ref field, value, nameof(Tabs), nameof(Label)))
                ReplaceTabsSubscription(oldTabs, value);
        }
    } = [new EffectTab() { Id = _initialGuid }];

    public Guid? SelectedTabId
    {
        get;
        set => Set(ref field, value, nameof(SelectedTabId), nameof(Label));
    } = _initialGuid;

    public ContainerEffect()
    {
        // プロパティ初期化子で構築済みのデフォルトタブの PropertyChanged を購読する。
        // 以降は Tabs setter で購読を付け替える。
        foreach (var tab in Tabs)
            SubscribeTab(tab);
    }

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
        return selectedTab?.Effects ?? [];
    }

    private void ReplaceTabsSubscription(ImmutableList<EffectTab> oldTabs, ImmutableList<EffectTab> newTabs)
    {
        foreach (var tab in oldTabs)
            UnsubscribeTab(tab);
        foreach (var tab in newTabs)
            SubscribeTab(tab);
    }

    private void SubscribeTab(EffectTab tab) => tab.PropertyChanged += OnTabPropertyChanged;

    private void UnsubscribeTab(EffectTab tab) => tab.PropertyChanged -= OnTabPropertyChanged;

    /// <summary>
    /// 配下の EffectTab の Name / Effects が変わったら Label を再評価する。
    /// Tabs リスト自体は変わらないので setter 経由の Label 通知は出ない。ここで補完する。
    /// </summary>
    private void OnTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(EffectTab.Name) or nameof(EffectTab.Effects))
            OnPropertyChanged(nameof(Label));
    }
}
