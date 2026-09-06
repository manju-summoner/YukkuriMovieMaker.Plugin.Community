using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Settings;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Explorer
{
    internal class ExplorerFilter : Bindable
    {
        public event EventHandler? FilterChanged;

        public bool IsFiltered => IsFilteredByExtension || !string.IsNullOrEmpty(SearchText);
        public bool IsFilteredByExtension =>
            !IsVideoVisible ||
            !IsAudioVisible ||
            !IsImageVisible ||
            !IsTextVisible ||
            !IsOtherVisible ||
            !IsDirectoryVisible;

        public bool IsVideoVisible { get; set => Set(ref field, value); } = true;
        public bool IsAudioVisible { get; set => Set(ref field, value); } = true;
        public bool IsImageVisible { get; set => Set(ref field, value); } = true;
        public bool IsTextVisible { get; set => Set(ref field, value); } = true;
        public bool IsOtherVisible { get; set => Set(ref field, value); } = true;
        public bool IsDirectoryVisible { get; set => Set(ref field, value); } = true;

        public string SearchText { get; set => Set(ref field, value); } = string.Empty;
        public bool IsRecursive { get; set => Set(ref field, value); } = false;

        public bool IsRecursiveSearch => IsRecursive && !string.IsNullOrEmpty(SearchText);

        static readonly IReadOnlyList<(Predicate<FileType> TypeMatch, Predicate<string> ExtensionMatch, Func<ExplorerFilter, bool> IsVisible)> fileTypeRules =
        [
            (t => t.HasFlag(FileType.動画), _ => true, f => f.IsVideoVisible),
            (t => t == FileType.音声, _ => true, f => f.IsAudioVisible),
            (t => t.HasFlag(FileType.画像), _ => true, f => f.IsImageVisible),
            (t => !t.HasFlag(FileType.動画) && t != FileType.音声 && !t.HasFlag(FileType.画像),
             ext => string.Equals(ext, "txt", StringComparison.OrdinalIgnoreCase), f => f.IsTextVisible),
            (t => !t.HasFlag(FileType.動画) && t != FileType.音声 && !t.HasFlag(FileType.画像),
             ext => !string.Equals(ext, "txt", StringComparison.OrdinalIgnoreCase), f => f.IsOtherVisible),
        ];

        public bool IsMatch(IExplorerItemViewModel item)
        {
            if (item.IsDirectory)
            {
                if (!IsDirectoryVisible)
                    return false;
            }
            else
            {
                var type = TryGetFileType(item.Path);
                var ext = item.Extension;

                foreach (var (typeMatch, extMatch, isVisible) in fileTypeRules)
                {
                    if (typeMatch(type) && extMatch(ext) && !isVisible(this))
                        return false;
                }
            }

            return IsNameMatch(item.Name, SearchText);
        }

        public static bool IsNameMatch(string name, string searchText) =>
            string.IsNullOrEmpty(searchText) || name.Contains(searchText, StringComparison.OrdinalIgnoreCase);

        public void CopyFrom(ExplorerFilter? other)
        {
            if (other == null)
                return;
            IsVideoVisible = other.IsVideoVisible;
            IsAudioVisible = other.IsAudioVisible;
            IsImageVisible = other.IsImageVisible;
            IsTextVisible = other.IsTextVisible;
            IsOtherVisible = other.IsOtherVisible;
            IsDirectoryVisible = other.IsDirectoryVisible;
            SearchText = other.SearchText;
            IsRecursive = other.IsRecursive;
        }

        protected override bool Set<T>(ref T storage, T value, [CallerMemberName] string name = "", params string[] etcChangedPropertyNames)
        {
            var result = base.Set(ref storage, value, name, etcChangedPropertyNames);
            if (result)
            {
                FilterChanged?.Invoke(this, EventArgs.Empty);
                OnPropertyChanged(nameof(IsFiltered));
                OnPropertyChanged(nameof(IsFilteredByExtension));
                OnPropertyChanged(nameof(IsRecursiveSearch));
            }
            return result;
        }

        static FileType TryGetFileType(string path)
        {
            try
            {
                return FileSettings.Default.FileExtensions.GetFileType(path);
            }
            catch
            {
                return FileType.None;
            }
        }
    }
}
