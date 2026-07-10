using System.Windows.Media.Imaging;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Textures;

internal interface ITextureLoader
{
    int Priority { get; }
    bool CanLoad(string path);
    BitmapSource Load(string path);
    bool CanLoadRaw(string path);
    TextureRawData LoadRaw(string path);
}
