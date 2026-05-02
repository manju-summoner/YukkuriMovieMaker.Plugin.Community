using System.Collections.ObjectModel;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

public class EffectTabStashSettings : SettingsBase<EffectTabStashSettings>
{
    public override SettingsCategory Category => SettingsCategory.None;

    public override string Name => string.Empty;

    public override bool HasSettingView => false;

    public override object? SettingView => null;

    public ObservableCollection<EffectTab> Stashes { get; } = [];

    public override void Initialize()
    {

    }
}
