using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Localization;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Services;

public sealed class GradientMapSettings : SettingsBase<GradientMapSettings>
{
    public static GradientMapSettings Instance => Default;

    public override string Name => Texts.EffectName;
    public override SettingsCategory Category => SettingsCategory.VideoEffect;
    public override bool HasSettingView => false;
    public override object? SettingView => null;

    public List<string> FavoritePaths
    {
        get;
        set => Set(ref field, value ?? []);
    } = [];

    public override void Initialize()
    {
    }
}
