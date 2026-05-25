using System.Collections.Immutable;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

public sealed class EffectTab : Animatable
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name
    {
        get;
        set => Set(ref field, value, nameof(Name));
    } = string.Empty;

    public ImmutableList<IVideoEffect> Effects
    {
        get;
        set => Set(ref field, value, nameof(Effects));
    } = [];

    protected override IEnumerable<IAnimatable> GetAnimatables() => Effects;
}
