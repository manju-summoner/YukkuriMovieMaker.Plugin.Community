using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus;

internal sealed class PiperPlusSettings : SettingsBase<PiperPlusSettings>
{
    public override SettingsCategory Category => SettingsCategory.Voice;
    public override string Name => "Piper Plus";
    public override bool HasSettingView => true;
    public override object? SettingView => new PiperPlusSettingsView();

    public override void Initialize()
    {
    }
}
