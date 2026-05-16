using System.Windows.Input;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Notepad
{
    internal class ClearNotepadCacheViewModel : Bindable
    {
        public long CacheSizeInBytes { get; }
        public string CacheSizeDisplay { get; }

        public ICommand ClearCommand { get; }

        public ClearNotepadCacheViewModel(Func<ClearNotepadCacheViewModel, Task> onClearAsync)
        {
            CacheSizeInBytes = NotepadImageCache.GetCacheSizeInBytes();
            CacheSizeDisplay = FormatSize(CacheSizeInBytes);
            ClearCommand = new ActionCommand(
                _ => CacheSizeInBytes > 0,
                async _ => await onClearAsync(this));
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
