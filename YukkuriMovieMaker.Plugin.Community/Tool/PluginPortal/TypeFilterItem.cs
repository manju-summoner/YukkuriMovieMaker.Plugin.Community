using System.ComponentModel;

namespace YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal
{
    public class TypeFilterItem(string name, bool isChecked, Action filterAction) : INotifyPropertyChanged
    {

        public string Name { get; } = name;
        public bool IsChecked
        {
            get => isChecked;
            set
            {
                if (isChecked != value)
                {
                    isChecked = value;
                    OnPropertyChanged(nameof(IsChecked));
                    filterAction?.Invoke();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
