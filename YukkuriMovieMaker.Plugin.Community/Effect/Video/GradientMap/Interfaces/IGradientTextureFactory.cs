using Vortice.Direct2D1;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Interfaces;

public interface IGradientTextureFactory
{
    ID2D1Bitmap? CreateGradientBitmap(ID2D1DeviceContext deviceContext, string filePath, int gradientIndex);
    ID2D1Bitmap? CreateGradientBitmapFromJson(ID2D1DeviceContext deviceContext, string json);
}
