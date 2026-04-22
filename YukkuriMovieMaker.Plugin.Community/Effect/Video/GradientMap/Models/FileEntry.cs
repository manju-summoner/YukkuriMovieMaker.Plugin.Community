using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Core;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Models;

public sealed class FileEntry : INotifyPropertyChanged
{
    public static readonly FileEntry None = new(string.Empty, isNone: true);

    public FileEntry(string filePath)
    {
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);
        Extension = Path.GetExtension(filePath).ToLowerInvariant();
        IsNone = false;
    }

    private FileEntry(string filePath, bool isNone)
    {
        FilePath = filePath;
        FileName = string.Empty;
        Extension = string.Empty;
        IsNone = isNone;
    }

    public string FilePath { get; }
    public string FileName { get; }
    public string Extension { get; }
    public bool IsGrd => Extension == ".grd";
    public bool IsNone { get; }

    public bool IsFavorite
    {
        get;
        set
        {
            if (IsNone || field == value) return;
            field = value;
            Notify();
        }
    }

    public BitmapSource? Thumbnail
    {
        get
        {
            if (!_thumbnailLoaded && !_thumbnailLoading)
                _ = LoadThumbnailAsync();
            return field;
        }
        private set
        {
            field = value;
            Notify();
        }
    }

    private bool _thumbnailLoaded;
    private bool _thumbnailLoading;

    private async Task LoadThumbnailAsync()
    {
        _thumbnailLoading = true;
        var bitmap = await Services.ThumbnailService.CreateThumbnailAsync(FilePath)
            .ConfigureAwait(false);
        _thumbnailLoaded = true;
        _thumbnailLoading = false;
        Thumbnail = bitmap;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, PropertyChangedEventArgsCache.Get(name!));

    public override bool Equals(object? obj) =>
        obj is FileEntry other &&
        IsNone == other.IsNone &&
        string.Equals(FilePath, other.FilePath, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() =>
        IsNone ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(FilePath);
}
