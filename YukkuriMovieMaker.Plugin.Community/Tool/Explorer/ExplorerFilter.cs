using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
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
        public bool IsAudioVisible { get => field; set => Set(ref field, value); } = true;
        public bool IsImageVisible { get => field; set => Set(ref field, value); } = true;
        public bool IsTextVisible { get => field; set => Set(ref field, value); } = true;
        public bool IsOtherVisible { get => field; set => Set(ref field, value); } = true;
        public bool IsDirectoryVisible { get => field; set => Set(ref field, value); } = true;

        public string SearchText { get => field; set => Set(ref field, value); } = string.Empty;


        public bool IsMatch(string path)
        {
            if (File.GetAttributes(path).HasFlag(FileAttributes.Directory))
            {
                if (!IsDirectoryVisible)
                    return false;
            }
            else
            {
                var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
                var type = FileSettings.Default.FileExtensions.GetFileType(path);

                if (type.HasFlag(FileType.動画) && !IsVideoVisible
                    || type is FileType.音声 && !IsAudioVisible
                    || type.HasFlag(FileType.画像) && !IsImageVisible
                    || type is FileType.None && ext == ".txt" && !IsTextVisible
                    || type is FileType.None && !IsOtherVisible)
                    return false;
            }

            if (!string.IsNullOrEmpty(SearchText))
            {
                var fileName = System.IO.Path.GetFileName(path);
                if (!fileName.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

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
        }
        protected override bool Set<T>(ref T storage, T value, [CallerMemberName] string name = "", params string[] etcChangedPropertyNames)
        {
            var result = base.Set(ref storage, value, name, etcChangedPropertyNames);
            if (result)
            {
                FilterChanged?.Invoke(this, EventArgs.Empty);
                OnPropertyChanged(nameof(IsFiltered));
                OnPropertyChanged(nameof(IsFilteredByExtension));
            }
            return result;
        }
    }
}
