using Newtonsoft.Json;
using SkiaSharp;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.ValueTypes;

public class OutlineWrapper
{
    [JsonIgnore] public SKPath? Path { get; set; }

    public override string ToString()
    {
        return Path is null ? "NULL" : $"{Path.PointCount}pts";
    }
}