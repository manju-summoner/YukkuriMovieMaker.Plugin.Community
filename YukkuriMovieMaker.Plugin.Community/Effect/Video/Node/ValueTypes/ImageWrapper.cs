using Newtonsoft.Json;
using Vortice.Direct2D1;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.ValueTypes;

public class ImageWrapper
{
    [JsonIgnore] public ID2D1Image? Image { get; set; }

    public override string ToString()
    {
        return Image?.NativePointer.ToString("x8") ?? "NULL";
    }
}