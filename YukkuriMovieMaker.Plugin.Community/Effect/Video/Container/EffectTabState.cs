namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

public sealed class EffectTabState
{
    public Guid? SelectedTabId { get; set; }
    public List<EffectTab> Tabs { get; set; } = [];
}
