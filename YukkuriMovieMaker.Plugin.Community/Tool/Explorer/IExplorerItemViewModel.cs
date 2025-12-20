using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Explorer
{
    public interface IExplorerItemViewModel : INotifyPropertyChanged
    {
        public string Path { get; }
        public string Name { get; }
        public ImageSource? Icon { get; }
        public ImageSource? Thumbnail { get; }
        public bool IsSelected { get; set; }

        public ICommand ClearCacheCommand { get; }

        public void SetImageSize(int icon, int thumbnail);
    }
}
