using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

public class ExtractEffectViewModel
{
    public string Name { get; }
    public IVideoEffect Effect { get; }

    public ExtractEffectViewModel(IVideoEffect effect)
    {
        Name = effect.Label;
        Effect = effect;
    }
}
