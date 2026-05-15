using System.Windows.Input;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Notepad
{
    /// <summary>
    /// ClearNotepadCacheView.xaml の相互作用ロジック
    /// </summary>
    internal class ClearNotepadCacheViewModel : Bindable
    {
        public long CacheSizeInBytes { get; }
        public string CacheSizeDisplay { get; }

        public ICommand ClearCommand { get; }

        public ClearNotepadCacheViewModel(Action<ClearNotepadCacheViewModel> onClear)
        {
            CacheSizeInBytes = NotepadImageCache.GetCacheSizeInBytes();
            CacheSizeDisplay = FormatSize(CacheSizeInBytes);
            ClearCommand = new ActionCommand(
                _ => CacheSizeInBytes > 0,
                _ => onClear(this));
        }

        private static string FormatSize(long bytes)
        {
            const long KB = 1024;
            const long MB = KB * 1024;
            const long GB = MB * 1024;
            return bytes switch
            {
                >= GB => $"{(double)bytes / GB:0.##} GB",
                >= MB => $"{(double)bytes / MB:0.##} MB",
                >= KB => $"{(double)bytes / KB:0.##} KB",
                _ => $"{bytes} B",
            };
        }
    }
}
