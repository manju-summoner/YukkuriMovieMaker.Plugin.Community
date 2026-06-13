using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Core;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Services;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Models;

public sealed class GrdGradientEntry(int index, string name, string filePath) : INotifyPropertyChanged
{
    private bool _thumbnailLoaded;
    private bool _thumbnailLoading;

    public int Index { get; } = index;
    public string Name { get; } = name;
    public string FilePath { get; } = filePath;

    public BitmapSource? Thumbnail
    {
        get
        {
            if (!_thumbnailLoaded && !_thumbnailLoading)
                _ = LoadAsync();
            return field;
        }
        private set
        {
            field = value;
            Notify();
        }
    }

    private async Task LoadAsync()
    {
        _thumbnailLoading = true;
        var bmp = await ThumbnailService.CreateThumbnailAsync(FilePath, Index)
            .ConfigureAwait(false);
        _thumbnailLoaded = true;
        _thumbnailLoading = false;
        Thumbnail = bmp;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, PropertyChangedEventArgsCache.Get(name!));
}
