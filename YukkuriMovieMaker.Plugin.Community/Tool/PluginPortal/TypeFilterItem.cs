using System.ComponentModel;

namespace YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal
{
    public class TypeFilterItem : INotifyPropertyChanged
    {
        private bool _isChecked;
        private readonly Action _filterAction;

        public string Name { get; }
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    OnPropertyChanged(nameof(IsChecked));
                    _filterAction?.Invoke();
                }
            }
        }

        public TypeFilterItem(string name, bool isChecked, Action filterAction)
        {
            Name = name;
            _isChecked = isChecked;
            _filterAction = filterAction;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
