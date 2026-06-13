namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

internal sealed record MoveTabToIndexParameter(
    EffectTabItemViewModel Tab,
    int TargetIndex);
