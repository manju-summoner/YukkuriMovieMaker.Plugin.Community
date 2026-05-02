using Newtonsoft.Json;
using System.Collections.Immutable;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

internal static class EffectTabStateService
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        Formatting = Formatting.None,
        NullValueHandling = NullValueHandling.Ignore,
    };

    public static string Serialize(EffectTabState state) =>
        JsonConvert.SerializeObject(state, Settings);

    public static bool TryDeserialize(string? json, out EffectTabState state)
    {
        state = new EffectTabState();
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            var parsed = JsonConvert.DeserializeObject<EffectTabState>(json, Settings);
            if (parsed is null)
                return false;
            state = parsed;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static EffectTabState CreateDefault(
        ImmutableList<IVideoEffect> effects,
        string defaultTabName)
    {
        var serialized = EffectSerializer.Serialize(effects);
        return CreateSingleTabState(serialized, defaultTabName);
    }

    public static EffectTabState CreateSingleTabState(string serializedEffects, string defaultTabName)
    {
        var tab = new EffectTab
        {
            Name = string.IsNullOrWhiteSpace(defaultTabName) ? "Tab" : defaultTabName,
            SerializedEffects = serializedEffects ?? string.Empty,
        };

        return new EffectTabState
        {
            SelectedTabId = tab.Id,
            Tabs = [tab],
        };
    }

    public static EffectTabState ResolveEffectState(
        string? serializedTabs,
        ImmutableList<IVideoEffect> currentEffects,
        string defaultTabName)
    {
        var state = ResolveState(serializedTabs, currentEffects, defaultTabName);
        GetSelectedTab(state).SerializedEffects = EffectSerializer.Serialize(currentEffects);
        return state;
    }

    public static EffectTabState Normalize(
        EffectTabState? state,
        ImmutableList<IVideoEffect> fallbackEffects,
        string defaultTabName)
    {
        if (state is null)
            return CreateDefault(fallbackEffects, defaultTabName);

        var tabs = state.Tabs is { Count: > 0 }
            ? state.Tabs
            : [new EffectTab
            {
                Name = string.IsNullOrWhiteSpace(defaultTabName) ? "Tab" : defaultTabName,
                SerializedEffects = EffectSerializer.Serialize(fallbackEffects),
            }];

        var normalizedTabs = tabs.Select((tab, i) => new EffectTab
        {
            Id = tab.Id == Guid.Empty ? Guid.NewGuid() : tab.Id,
            Name = string.IsNullOrWhiteSpace(tab.Name)
                ? (i == 0 ? defaultTabName : $"{defaultTabName} {i + 1}")
                : tab.Name,
            SerializedEffects = tab.SerializedEffects ?? string.Empty,
        }).ToList();

        var selectedId = state.SelectedTabId;
        var resolvedSelectedId = selectedId is not null && normalizedTabs.Any(t => t.Id == selectedId.Value)
            ? selectedId
            : normalizedTabs[0].Id;

        return new EffectTabState
        {
            SelectedTabId = resolvedSelectedId,
            Tabs = normalizedTabs,
        };
    }

    private static EffectTabState ResolveState(
        string? serializedTabs,
        ImmutableList<IVideoEffect> fallbackEffects,
        string defaultTabName)
    {
        EffectTabState? parsed = TryDeserialize(serializedTabs, out var state) ? state : null;
        return Normalize(parsed, fallbackEffects, defaultTabName);
    }

    public static EffectTab GetSelectedTab(EffectTabState state)
    {
        if (state.Tabs.Count == 0)
            throw new InvalidOperationException("Cannot get selected tab from a state with no tabs.");

        var selectedId = state.SelectedTabId;
        if (selectedId is not null)
        {
            var selected = state.Tabs.FirstOrDefault(t => t.Id == selectedId.Value);
            if (selected is not null)
                return selected;
        }

        return state.Tabs[0];
    }

    public static ImmutableList<IVideoEffect> GetSelectedEffects(EffectTabState state)
    {
        var selected = GetSelectedTab(state);
        return EffectSerializer.Deserialize(selected.SerializedEffects);
    }

    public static string GetSelectedEffectsJson(EffectTabState state)
    {
        var selected = GetSelectedTab(state);
        return selected.SerializedEffects ?? string.Empty;
    }

    public static EffectTabState DeepCopy(EffectTabState source)
    {
        var copiedTabs = source.Tabs.Select(t => new EffectTab
        {
            Id = t.Id,
            Name = t.Name,
            SerializedEffects = t.SerializedEffects,
        }).ToList();

        return new EffectTabState
        {
            SelectedTabId = source.SelectedTabId,
            Tabs = copiedTabs,
        };
    }
}
