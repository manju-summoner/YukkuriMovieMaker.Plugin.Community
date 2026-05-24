using System.Collections.Immutable;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

public sealed class EffectTab : Animatable
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name
    {
        get => _name;
        set => Set(ref _name, value);
    }
    private string _name = string.Empty;

    public ImmutableList<IVideoEffect> Effects
    {
        get => _effects;
        set => Set(ref _effects, value);
    }
    private ImmutableList<IVideoEffect> _effects = ImmutableList<IVideoEffect>.Empty;

    protected override IEnumerable<IAnimatable> GetAnimatables() => Effects;
}
