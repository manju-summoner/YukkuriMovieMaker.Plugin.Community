using System.Collections.Immutable;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

internal static class EffectTabStateService
{
    /// <summary>
    /// Tabs が空、Id 重複、未割当の SelectedTabId などを正しく整形した
    /// (tabs, selectedTabId) を返す。プロジェクトファイルの読み込み直後の検証用。
    /// </summary>
    public static (ImmutableList<EffectTab> Tabs, Guid SelectedTabId) Normalize(
        IReadOnlyList<EffectTab> rawTabs,
        Guid? rawSelectedTabId,
        ImmutableList<IVideoEffect> fallbackEffects,
        string defaultTabName)
    {
        var source = rawTabs is { Count: > 0 }
            ? rawTabs
            : (IReadOnlyList<EffectTab>)
            [
                new EffectTab
                {
                    Name = string.IsNullOrWhiteSpace(defaultTabName) ? "Tab" : defaultTabName,
                    Effects = fallbackEffects,
                }
            ];

        var normalizedTabs = source.Select((tab, i) => new EffectTab
        {
            Id = tab.Id == Guid.Empty ? Guid.NewGuid() : tab.Id,
            Name = string.IsNullOrWhiteSpace(tab.Name)
                ? (i == 0 ? defaultTabName : $"{defaultTabName} {i + 1}")
                : tab.Name,
            Effects = tab.Effects ?? ImmutableList<IVideoEffect>.Empty,
        }).ToImmutableList();

        var resolvedSelectedId = rawSelectedTabId is { } sid && normalizedTabs.Any(t => t.Id == sid)
            ? sid
            : normalizedTabs[0].Id;

        return (normalizedTabs, resolvedSelectedId);
    }
}
