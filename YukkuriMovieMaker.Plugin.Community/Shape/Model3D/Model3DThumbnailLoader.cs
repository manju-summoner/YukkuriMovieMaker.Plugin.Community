using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Parsers;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D;

internal sealed class Model3DThumbnailLoader : IFileSelectorThumbnailLoader
{
    public bool CanLoad(string filePath)
        => !string.IsNullOrEmpty(filePath) && Model3DLoader.IsSupported(filePath);

    public Task<BitmapSource?> LoadThumbnailAsync(string filePath)
    {
        return Task.Run<BitmapSource?>(() =>
        {
            try
            {
                var model = Model3DLoader.Load(filePath);
                return Model3DThumbnailUtil.CreateThumbnail(model);
            }
            catch
            {
                return null;
            }
        });
    }
}
