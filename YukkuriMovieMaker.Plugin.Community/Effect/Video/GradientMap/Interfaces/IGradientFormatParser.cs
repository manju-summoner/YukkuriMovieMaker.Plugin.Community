using Vortice.Direct2D1;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Models;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Interfaces;

public interface IGradientFormatParser
{
    bool CanParse(string filePath);

    ID2D1Bitmap? CreateBitmap(
        ID2D1DeviceContext deviceContext,
        string filePath,
        int gradientIndex);

    GrdManifest ReadManifest(string filePath);
}
