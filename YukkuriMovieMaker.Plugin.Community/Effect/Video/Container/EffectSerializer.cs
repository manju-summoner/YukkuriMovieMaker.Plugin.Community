using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.Immutable;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

internal static class EffectSerializer
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        TypeNameHandling = TypeNameHandling.Auto,
        NullValueHandling = NullValueHandling.Ignore,
    };

    public static string Serialize(ImmutableList<IVideoEffect> effects)
    {
        return JsonConvert.SerializeObject(effects, Formatting.None, Settings);
    }

    public static ImmutableList<IVideoEffect> Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return ImmutableList<IVideoEffect>.Empty;
        try
        {
            var list = JsonConvert.DeserializeObject<List<IVideoEffect>>(json, Settings);
            return list == null ? ImmutableList<IVideoEffect>.Empty : list.ToImmutableList();
        }
        catch
        {
            return ImmutableList<IVideoEffect>.Empty;
        }
    }
}
