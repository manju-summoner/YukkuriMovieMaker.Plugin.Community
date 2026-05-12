using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

public class ExtractEffectViewModel
{
    public string Name { get; }
    public string SerializedEffect { get; }

    public ExtractEffectViewModel(IVideoEffect effect)
    {
        Name = effect.Label;
        SerializedEffect = EffectSerializer.Serialize([effect]);
    }
}
