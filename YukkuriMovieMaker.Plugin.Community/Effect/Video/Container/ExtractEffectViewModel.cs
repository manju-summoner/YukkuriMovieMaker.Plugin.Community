using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

public class ExtractEffectViewModel(IVideoEffect effect)
{
    public string Name { get; } = effect.Label;
    public IVideoEffect Effect { get; } = effect;
}
