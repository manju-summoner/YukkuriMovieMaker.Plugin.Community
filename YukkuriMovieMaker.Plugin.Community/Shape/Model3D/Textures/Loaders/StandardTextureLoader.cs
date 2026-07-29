using System.IO;
using System.Windows.Media.Imaging;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Textures.Loaders;

internal sealed class StandardTextureLoader : ITextureLoader
{
    private const long MaxFileBytes = 256L * 1024 * 1024;
    private const long MaxDecodedBytes = 512L * 1024 * 1024;

    public int Priority => 0;

    public bool CanLoad(string path) => true;

    public bool CanLoadRaw(string path) => false;

    public BitmapSource Load(string path)
    {
        if (new FileInfo(path).Length > MaxFileBytes)
        {
            throw new InvalidOperationException("Texture file too large");
        }

        var bytes = File.ReadAllBytes(path);

        using (var probe = new MemoryStream(bytes, false))
        {
            var decoder = BitmapDecoder.Create(probe, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            var frame = decoder.Frames[0];
            if ((long)frame.PixelWidth * frame.PixelHeight * 4 > MaxDecodedBytes)
            {
                throw new InvalidOperationException("Image dimensions too large");
            }
        }

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
