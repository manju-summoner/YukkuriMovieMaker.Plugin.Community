using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Core;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Models;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Services;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.ViewModels;

public sealed class FileSelectorViewModel : INotifyPropertyChanged
{
    private readonly string[] _extensions;
    private readonly string _filter;
    private string _currentDirectory = string.Empty;
    private bool _suppressSync;

    public FileSelectorViewModel(string extensions, string filter)
    {
        var parts = extensions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        _extensions = new string[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            var ext = parts[i];
            _extensions[i] = ext.StartsWith('.') ? ext.ToLowerInvariant() : $".{ext.ToLowerInvariant()}";
        }
        _filter = filter.Replace(',', '|');
        ToggleFavoriteCommand = new ActionCommand(_ => true, OnToggleFavorite);
        BrowseCommand = new ActionCommand(_ => true, _ => Browse());
        RefreshFiles();
        SyncSelection();
    }

    public ObservableCollection<FileEntry> Files { get; } = [];

    public FileEntry? SelectedFile
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
            if (!_suppressSync && value is not null)
                FilePath = value.IsNone ? string.Empty : value.FilePath;
        }
    }

    public bool HasFavorites
    {
        get;
        private set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public string FilePath
    {
        get => field;
        set
        {
            if (string.Equals(field, value, StringComparison.OrdinalIgnoreCase)) return;
            field = value;
            OnPropertyChanged();
            OnFilePathChanged();
        }
    } = string.Empty;

    public ICommand ToggleFavoriteCommand { get; }
    public ICommand BrowseCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnFilePathChanged()
    {
        var dir = string.IsNullOrWhiteSpace(FilePath)
            ? string.Empty
            : Path.GetDirectoryName(FilePath) ?? string.Empty;

        if (!string.Equals(dir, _currentDirectory, StringComparison.OrdinalIgnoreCase))
        {
            _currentDirectory = dir;
            RefreshFiles();
        }

        SyncSelection();
    }

    private void SyncSelection()
    {
        _suppressSync = true;
        try
        {
            if (string.IsNullOrWhiteSpace(FilePath))
            {
                SelectedFile = FileEntry.None;
                return;
            }

            FileEntry? match = null;
            for (var i = 0; i < Files.Count; i++)
            {
                if (Files[i].IsNone) continue;
                if (string.Equals(Files[i].FilePath, FilePath, StringComparison.OrdinalIgnoreCase))
                {
                    match = Files[i];
                    break;
                }
            }

            if (match is null && File.Exists(FilePath))
            {
                var entry = new FileEntry(FilePath);
                Files.Add(entry);
                match = entry;
            }

            SelectedFile = match;
        }
        finally
        {
            _suppressSync = false;
        }
    }

    private void RefreshFiles()
    {
        Files.Clear();

        if (string.IsNullOrWhiteSpace(FilePath))
            Files.Add(FileEntry.None);

        var settings = GradientMapSettings.Instance;
        var favoritePaths = settings.FavoritePaths;
        var favoriteCount = 0;

        for (var i = 0; i < favoritePaths.Count; i++)
        {
            var p = favoritePaths[i];
            if (!File.Exists(p) || !IsSupported(p)) continue;
            Files.Add(new FileEntry(p) { IsFavorite = true });
            favoriteCount++;
        }

        HasFavorites = favoriteCount > 0;

        if (!string.IsNullOrWhiteSpace(_currentDirectory) && Directory.Exists(_currentDirectory))
        {
            var dirFiles = Directory.GetFiles(_currentDirectory);
            Array.Sort(dirFiles, StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < dirFiles.Length; i++)
            {
                var path = dirFiles[i];
                if (!IsSupported(path)) continue;

                var alreadyExists = false;
                for (var j = 0; j < Files.Count; j++)
                {
                    if (Files[j].IsNone) continue;
                    if (string.Equals(Files[j].FilePath, path, StringComparison.OrdinalIgnoreCase))
                    {
                        alreadyExists = true;
                        break;
                    }
                }
                if (alreadyExists) continue;

                var entry = new FileEntry(path);
                for (var j = 0; j < favoritePaths.Count; j++)
                {
                    if (string.Equals(favoritePaths[j], path, StringComparison.OrdinalIgnoreCase))
                    {
                        entry.IsFavorite = true;
                        break;
                    }
                }
                Files.Add(entry);
            }
        }
    }

    private bool IsSupported(string path)
    {
        var ext = Path.GetExtension(path);
        for (var i = 0; i < _extensions.Length; i++)
        {
            if (string.Equals(_extensions[i], ext, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private void OnToggleFavorite(object? parameter)
    {
        if (parameter is not FileEntry entry || entry.IsNone) return;
        entry.IsFavorite = !entry.IsFavorite;

        var settings = GradientMapSettings.Instance;
        var updated = new List<string>(settings.FavoritePaths);

        if (entry.IsFavorite)
        {
            var alreadyExists = false;
            for (var i = 0; i < updated.Count; i++)
            {
                if (string.Equals(updated[i], entry.FilePath, StringComparison.OrdinalIgnoreCase))
                {
                    alreadyExists = true;
                    break;
                }
            }
            if (!alreadyExists)
                updated.Add(entry.FilePath);
        }
        else
        {
            for (var i = updated.Count - 1; i >= 0; i--)
            {
                if (string.Equals(updated[i], entry.FilePath, StringComparison.OrdinalIgnoreCase))
                    updated.RemoveAt(i);
            }
        }

        settings.FavoritePaths = updated;
        settings.Save();
        RefreshFiles();
        SyncSelection();
    }

    private void Browse()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = _filter,
            InitialDirectory = string.IsNullOrWhiteSpace(_currentDirectory)
                ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                : _currentDirectory
        };
        if (dialog.ShowDialog() == true)
            FilePath = dialog.FileName;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, PropertyChangedEventArgsCache.Get(name!));
}
