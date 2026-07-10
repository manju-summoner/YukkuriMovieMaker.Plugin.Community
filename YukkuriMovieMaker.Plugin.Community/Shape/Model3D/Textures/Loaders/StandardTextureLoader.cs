using System.IO;
using System.Windows.Media.Imaging;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Textures.Loaders;

internal sealed class StandardTextureLoader : ITextureLoader
{
    public int Priority => 0;

    public bool CanLoad(string path) => true;

    public bool CanLoadRaw(string path) => false;

    public BitmapSource Load(string path)
    {
        var bytes = File.ReadAllBytes(path);
        using var ms = new MemoryStream(bytes);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.StreamSource = ms;
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    public TextureRawData LoadRaw(string path) => throw new NotSupportedException();
}
