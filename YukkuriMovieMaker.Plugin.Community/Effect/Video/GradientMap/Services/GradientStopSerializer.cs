using System.Text.Json;
using System.Text.Json.Serialization;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Models;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Services;

internal static class GradientStopSerializer
{
    private static readonly JsonSerializerOptions Options = new();

    public static GradientColorStop[] Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            var dtos = JsonSerializer.Deserialize<StopDto[]>(json, Options);
            if (dtos is null || dtos.Length == 0) return [];
            var result = new GradientColorStop[dtos.Length];
            for (var i = 0; i < dtos.Length; i++)
            {
                var d = dtos[i];
                result[i] = new GradientColorStop(d.P, d.R, d.G, d.B, d.A);
            }
            return result;
        }
        catch { return []; }
    }

    public static string Serialize(IReadOnlyList<GradientColorStop> stops)
    {
        var dtos = new StopDto[stops.Count];
        for (var i = 0; i < stops.Count; i++)
        {
            var s = stops[i];
            dtos[i] = new StopDto { P = s.Position, R = s.R, G = s.G, B = s.B, A = s.A };
        }
        return JsonSerializer.Serialize(dtos, Options);
    }

    private sealed class StopDto
    {
        [JsonPropertyName("p")] public float P { get; set; }
        [JsonPropertyName("r")] public byte R { get; set; }
        [JsonPropertyName("g")] public byte G { get; set; }
        [JsonPropertyName("b")] public byte B { get; set; }
        [JsonPropertyName("a")] public byte A { get; set; }
    }
}
