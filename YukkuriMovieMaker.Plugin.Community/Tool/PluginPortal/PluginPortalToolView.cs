using System.ComponentModel;

namespace YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal
{
    public class OpenYMM4PluginPortalView : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
