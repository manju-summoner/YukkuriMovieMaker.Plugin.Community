using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

[VideoEffect(nameof(Texts.VideoEffect_Name), [VideoEffectCategories.Decoration], [nameof(Texts.VideoEffect_Tag_Container), nameof(Texts.VideoEffect_Tag_Preset), nameof(Texts.VideoEffect_Tag_Group)], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
public sealed class ContainerEffect : VideoEffectBase
{
    public string PresetName
    {
        get => _presetName;
        set
        {
            if (Set(ref _presetName, value))
                OnPropertyChanged(nameof(Label));
        }
    }
    private string _presetName = string.Empty;

    public override string Label
    {
        get
        {
            var count = Effects.Count;
            return string.IsNullOrEmpty(PresetName)
                ? string.Format(Texts.Container_ActiveEffectsCount, Texts.Container_DisplayName, count)
                : string.Format(Texts.Container_ActiveEffectsCountWithPreset, Texts.Container_DisplayName, count, PresetName);
        }
    }

    [Display(GroupName = nameof(Texts.Container_PresetGroup), Name = nameof(Texts.Container_EmptyLabel), ResourceType = typeof(Texts))]
    [PresetManagerControl]
    [Newtonsoft.Json.JsonIgnore]
    public bool PresetManagerVisible { get; set; } = true;

    [Display(GroupName = nameof(Texts.Container_EffectGroup), Name = nameof(Texts.Container_EmptyLabel), ResourceType = typeof(Texts))]
    [EffectTabManagerControl]
    [Newtonsoft.Json.JsonIgnore]
    public bool EffectTabManagerVisible { get; set; } = true;

    [Display(GroupName = nameof(Texts.Container_EffectGroup), Name = nameof(Texts.Container_EmptyLabel), ResourceType = typeof(Texts))]
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

    public string? SelectedPresetJson
    {
        get => _selectedPresetJson;
        set => Set(ref _selectedPresetJson, value);
    }
    private string? _selectedPresetJson;

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
