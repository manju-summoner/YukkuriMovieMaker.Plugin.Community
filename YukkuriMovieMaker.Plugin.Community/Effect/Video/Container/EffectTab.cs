namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

public sealed class EffectTab
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string SerializedEffects { get; set; } = string.Empty;
}
