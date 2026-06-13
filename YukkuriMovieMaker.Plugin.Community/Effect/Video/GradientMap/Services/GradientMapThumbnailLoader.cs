using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using YukkuriMovieMaker.Controls;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Services;

/// <summary>
/// FileSelector 用の .grd（Photoshop グラデーション）ファイル向けカスタムサムネイルローダー。
/// 画像系（.png, .jpg 等）は本ローダーでは扱わず、FileSelector 既定の <see cref="YukkuriMovieMaker.Commons.ShellThumbnail"/> にフォールバックさせる。
/// </summary>
public sealed class GradientMapThumbnailLoader : IFileSelectorThumbnailLoader
{
    public bool CanLoad(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return false;
        return string.Equals(Path.GetExtension(filePath), ".grd", System.StringComparison.OrdinalIgnoreCase);
    }

    public Task<BitmapSource?> LoadThumbnailAsync(string filePath)
    {
        return Task.Run<BitmapSource?>(() =>
        {
            try
            {
                //.grd の先頭グラデーション（index 0）を代表サムネイルとする
                var pixels = GrdParser.ParseToPixels(filePath, 0);
                if (pixels is null)
                    return null;

                //256x1 の横1pxの帯をそのまま縦方向に伸ばした画像を返す。
                //FileSelector 側で ShellThumbnail.ApplyThumbnailEffect によるCrop/Resize/MemoryOptimizeが適用される。
                const int width = GrdParser.Resolution;
                const int height = 64;
                var buffer = new byte[width * height * 4];
                var rowStride = width * 4;
                for (var y = 0; y < height; y++)
                    System.Buffer.BlockCopy(pixels, 0, buffer, y * rowStride, rowStride);

                var bitmap = BitmapSource.Create(
                    width, height, 96, 96,
                    PixelFormats.Pbgra32, null,
                    buffer, rowStride);
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        });
    }
}
