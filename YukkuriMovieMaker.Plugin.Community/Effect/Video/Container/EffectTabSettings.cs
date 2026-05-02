using System.Collections.ObjectModel;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

internal class EffectTabSettings : SettingsBase<EffectTabSettings>
{
    public override SettingsCategory Category => SettingsCategory.None;

    public override string Name => Texts.Container_DisplayName;

    public override bool HasSettingView => false;

    public override object? SettingView => null;

    public ObservableCollection<EffectTab> PinnedTabs { get; } = new();

    public override void Initialize()
    {
    }
}
